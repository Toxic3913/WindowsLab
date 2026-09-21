using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public static class WorkerProtocol
{
    public const string PipeName = "WindowsLab.Worker";
    public const int ProtocolVersion = 1;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

public sealed record WorkerRequest(string Method, JsonElement? Params, int Version = WorkerProtocol.ProtocolVersion);

public sealed record WorkerResponse(bool Ok, string? Error = null, JsonElement? Result = null);

public sealed record WorkerApplyParams(IReadOnlyList<string> TweakIds, string CatalogDirectory, bool DryRun);

public sealed record WorkerApplyResultDto(
    string BackupId,
    IReadOnlyList<WorkerTweakOutcome> Outcomes);

public sealed record WorkerTweakOutcome(string TweakId, string Outcome, string Message);

public sealed record WorkerRollbackParams(string BackupId);

public sealed class WorkerClient
{
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static async Task<bool> TryHealthAsync(int timeoutMs = 800)
    {
        try
        {
            var resp = await SendAsync(new WorkerRequest("Health", null), timeoutMs).ConfigureAwait(false);
            return resp.Ok;
        }
        catch
        {
            return false;
        }
    }

    public static async Task EnsureRunningAsync(string? workerExePath = null, int waitMs = 15_000)
    {
        if (await TryHealthAsync().ConfigureAwait(false))
        {
            return;
        }

        var exe = workerExePath ?? ResolveWorkerPath();
        if (exe is null || !File.Exists(exe))
        {
            throw new FileNotFoundException("WindowsLab.Worker.exe not found next to the client.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
        };
        Process.Start(psi);

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < waitMs)
        {
            if (await TryHealthAsync().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(400).ConfigureAwait(false);
        }

        throw new TimeoutException("Elevated Worker did not become ready (UAC cancelled?).");
    }

    public static async Task<WorkerResponse> SendAsync(WorkerRequest request, int timeoutMs = 120_000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        await using var pipe = new NamedPipeClientStream(".", WorkerProtocol.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await pipe.ConnectAsync(Math.Min(timeoutMs, 5_000), cts.Token).ConfigureAwait(false);

        var payload = JsonSerializer.SerializeToUtf8Bytes(request, WorkerProtocol.JsonOptions);
        await WriteFrameAsync(pipe, payload, cts.Token).ConfigureAwait(false);
        var responseBytes = await ReadFrameAsync(pipe, cts.Token).ConfigureAwait(false);
        var resp = JsonSerializer.Deserialize<WorkerResponse>(responseBytes, WorkerProtocol.JsonOptions);
        return resp ?? new WorkerResponse(false, "Empty worker response");
    }

    public static string? ResolveWorkerPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "WindowsLab.Worker.exe"),
            Path.Combine(baseDir, "..", "WindowsLab.Worker", "WindowsLab.Worker.exe"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "WindowsLab.Worker", "bin", "Debug", "net10.0-windows", "WindowsLab.Worker.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "WindowsLab.Worker", "bin", "Release", "net10.0-windows", "WindowsLab.Worker.exe"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public static async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken ct)
    {
        var len = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(len, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var lenBuf = new byte[4];
        await ReadExactAsync(stream, lenBuf, ct).ConfigureAwait(false);
        var len = BitConverter.ToInt32(lenBuf);
        if (len is < 0 or > 16_000_000)
        {
            throw new InvalidDataException("Invalid worker frame length: " + len);
        }

        var buf = new byte[len];
        await ReadExactAsync(stream, buf, ct).ConfigureAwait(false);
        return buf;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct).ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException();
            }

            offset += n;
        }
    }
}
