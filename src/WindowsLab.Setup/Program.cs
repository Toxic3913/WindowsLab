using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

namespace WindowsLab.Setup;

internal static class Program
{
    private const string ResourceName = "payload.zip";

    [STAThread]
    public static int Main(string[] args)
    {
        if (HasFlag(args, "--update"))
        {
            return RunSilentUpdate(args);
        }

        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var uiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "es";
        using var form = new SetupWizardForm(LoadEula(uiLang), OpenPayload, uiLang);
        Application.Run(form);
        return form.ExitCode;
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static int RunSilentUpdate(string[] args)
    {
        try
        {
            var dir = GetOption(args, "--dir");
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsLab");
            }

            if (GetOption(args, "--wait-pid") is { } pidRaw
                && int.TryParse(pidRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
                && pid > 0)
            {
                try
                {
                    using var existing = System.Diagnostics.Process.GetProcessById(pid);
                    if (!existing.WaitForExit(120_000))
                    {
                        try { existing.Kill(entireProcessTree: true); } catch { /* best effort */ }
                        existing.WaitForExit(15_000);
                    }
                }
                catch (ArgumentException)
                {
                    // already exited
                }
            }

            // Brief settle so file locks release.
            Thread.Sleep(800);

            using var zip = OpenPayload();
            if (zip is null)
            {
                MessageBox.Show(
                    "No hay payload embebido en el Setup. Descarga WindowsLab-Setup.exe de GitHub Releases.",
                    "WindowsLab Update",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 2;
            }

            Directory.CreateDirectory(dir);
            ZipFile.ExtractToDirectory(zip, dir, overwriteFiles: true);

            var exe = Path.Combine(dir, "WindowsLab.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show("Falta WindowsLab.exe tras actualizar.", "WindowsLab Update",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 3;
            }

            RegisterInstallation(dir);

            if (HasFlag(args, "--launch"))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = dir,
                    UseShellExecute = true
                });
            }

            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "La actualización falló.\r\n\r\n" + ex.Message,
                "WindowsLab Update",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static string LoadEula(string lang)
    {
        var logical = lang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "eula-en.txt" : "eula-es.txt";
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().FirstOrDefault(n =>
            n.EndsWith(logical, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return lang.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                ? "WindowsLab 1.2 — accept to continue. See docs/legal/EULA-en.txt."
                : "WindowsLab 1.2 — acepte para continuar. Ver docs/legal/EULA-es.txt.";
        }

        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Stream? OpenPayload()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().FirstOrDefault(n =>
            n.EndsWith(ResourceName, StringComparison.OrdinalIgnoreCase) || n == ResourceName);
        return name is null ? null : asm.GetManifestResourceStream(name);
    }

    internal static void RegisterInstallation(string installDir)
    {
        var uninstall = Path.Combine(installDir, "WindowsLab-Uninstall.exe");
        if (!File.Exists(uninstall))
        {
            throw new InvalidOperationException(
                "Falta WindowsLab-Uninstall.exe en el paquete. Vuelva a publicar con eng/publish.ps1.");
        }

        var version = InstallRegistration.ReadProductVersion(installDir);
        InstallRegistration.Register(installDir, version, uninstall);
    }
}

internal sealed class SetupWizardForm : Form
{
    private readonly Func<Stream?> _openPayload;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly TextBox _eulaBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
    private readonly CheckBox _accept = new() { Text = "He leído y acepto el contrato de licencia (EULA)", AutoSize = true };
    private readonly TextBox _pathBox = new() { Dock = DockStyle.Top };
    private readonly CheckBox _desktopIcon = new() { Text = "Crear icono en el escritorio", Checked = true, AutoSize = true };
    private readonly CheckBox _startMenu = new() { Text = "Crear acceso en el menú Inicio", Checked = true, AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 8, 0, 0) };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly Button _back = new() { Text = "Atrás", Width = 100 };
    private readonly Button _next = new() { Text = "Siguiente", Width = 100 };
    private readonly Button _cancel = new() { Text = "Cancelar", Width = 100 };

    private int _step;

    public int ExitCode { get; private set; } = 1;

    public SetupWizardForm(string eula, Func<Stream?> openPayload, string uiLang)
    {
        _openPayload = openPayload;
        var es = !uiLang.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        Text = es ? "WindowsLab Setup — 1.0" : "WindowsLab Setup — 1.0";
        Width = 640;
        Height = 480;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _eulaBox.Text = eula;
        _accept.Text = es
            ? "He leído y acepto el contrato de licencia (EULA)"
            : "I have read and accept the license agreement (EULA)";
        _pathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsLab");
        _desktopIcon.Text = es ? "Crear icono en el escritorio" : "Create a desktop icon";
        _startMenu.Text = es ? "Crear acceso en el menú Inicio" : "Create a Start menu shortcut";
        _back.Text = es ? "Atrás" : "Back";
        _next.Text = es ? "Siguiente" : "Next";
        _cancel.Text = es ? "Cancelar" : "Cancel";

        var welcome = new TabPage(es ? "Bienvenida" : "Welcome");
        welcome.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = es
                ? "Asistente de instalación de WindowsLab\r\n\r\n• Windows 11 x64 (build ≥ 22000)\r\n• Self-contained (sin SDK)\r\n• Aparece en Aplicaciones instaladas + desinstalador\r\n\r\nFirma: puede faltar Authenticode → SmartScreen/UAC pueden avisar.\r\n\r\nPulse Siguiente."
                : "WindowsLab Setup Wizard\r\n\r\n• Windows 11 x64 (build ≥ 22000)\r\n• Self-contained (no SDK needed)\r\n• Shows in Installed apps + uninstaller\r\n\r\nSigning: Authenticode may be missing → SmartScreen/UAC may warn.\r\n\r\nClick Next.",
            Padding = new Padding(12)
        });

        var license = new TabPage("Licencia");
        var licensePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        licensePanel.Controls.Add(_eulaBox);
        var acceptPanel = new Panel { Dock = DockStyle.Bottom, Height = 36 };
        acceptPanel.Controls.Add(_accept);
        license.Controls.Add(licensePanel);
        license.Controls.Add(acceptPanel);

        var folder = new TabPage("Carpeta");
        var folderPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12) };
        folderPanel.Controls.Add(new Label { Text = "Instalar en:", AutoSize = true });
        var pathRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        _pathBox.Width = 420;
        var browse = new Button { Text = "Examinar…", Width = 100 };
        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { SelectedPath = _pathBox.Text };
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                _pathBox.Text = d.SelectedPath;
            }
        };
        pathRow.Controls.Add(_pathBox);
        pathRow.Controls.Add(browse);
        folderPanel.Controls.Add(pathRow);
        folderPanel.Controls.Add(_startMenu);
        folderPanel.Controls.Add(_desktopIcon);
        folderPanel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Text = "Se requieren permisos de administrador para escribir en Archivos de programa.\r\nDatos de usuario: %LocalAppData%\\WindowsLab  ·  Runtime: %ProgramData%\\WindowsLab"
        });
        folder.Controls.Add(folderPanel);

        var install = new TabPage("Instalar");
        var installPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        installPanel.Controls.Add(_progress);
        installPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Pulse Instalar para copiar WindowsLab, registrar el desinstalador en Windows y crear accesos directos.\r\nNo se aplican tweaks automáticamente."
        });
        installPanel.Controls.Add(_status);
        install.Controls.Add(installPanel);

        var done = new TabPage("Listo");
        done.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Text = "Instalación completada.\r\n\r\nPuede abrir WindowsLab desde el menú Inicio.\r\nPara quitarlo: Configuración → Aplicaciones → WindowsLab, o «Desinstalar WindowsLab» en el menú Inicio."
        });

        _tabs.Appearance = TabAppearance.FlatButtons;
        _tabs.ItemSize = new Size(0, 1);
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.TabPages.AddRange([welcome, license, folder, install, done]);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(8)
        };
        _cancel.Click += (_, _) => { ExitCode = 1; Close(); };
        _back.Click += (_, _) => Go(-1);
        _next.Click += async (_, _) => await GoNextAsync();
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_next);
        buttons.Controls.Add(_back);

        Controls.Add(_tabs);
        Controls.Add(buttons);
        ShowStep(0);
    }

    private void Go(int delta) => ShowStep(_step + delta);

    private async Task GoNextAsync()
    {
        if (_step == 1 && !_accept.Checked)
        {
            MessageBox.Show(this, "Debe aceptar el EULA para continuar.", "Licencia", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_step == 3)
        {
            await InstallAsync();
            return;
        }

        if (_step >= _tabs.TabCount - 1)
        {
            ExitCode = 0;
            Close();
            return;
        }

        ShowStep(_step + 1);
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, _tabs.TabCount - 1);
        _tabs.SelectedIndex = _step;
        _back.Enabled = _step > 0 && _step < _tabs.TabCount - 1;
        _next.Text = _step switch
        {
            3 => "Instalar",
            4 => "Cerrar",
            _ => "Siguiente"
        };
        _cancel.Enabled = _step < _tabs.TabCount - 1;
    }

    private async Task InstallAsync()
    {
        _next.Enabled = false;
        _back.Enabled = false;
        _progress.Visible = true;
        _status.Text = "Extrayendo…";

        try
        {
            await Task.Run(() =>
            {
                using var zip = _openPayload();
                if (zip is null)
                {
                    throw new InvalidOperationException("No hay payload embebido. Ejecute eng/publish.ps1 en el PC de build.");
                }

                var dest = _pathBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(dest))
                {
                    throw new InvalidOperationException("Carpeta de instalación vacía.");
                }

                if (Directory.Exists(dest))
                {
                    Directory.Delete(dest, recursive: true);
                }

                Directory.CreateDirectory(dest);
                ZipFile.ExtractToDirectory(zip, dest);

                var exe = Path.Combine(dest, "WindowsLab.exe");
                if (!File.Exists(exe))
                {
                    throw new InvalidOperationException("Falta WindowsLab.exe en el paquete.");
                }

                WindowsLab.Setup.Program.RegisterInstallation(dest);

                if (_startMenu.Checked)
                {
                    InstallRegistration.CreateShortcut(
                        exe,
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                        "WindowsLab.lnk",
                        "WindowsLab");
                    var uninstall = Path.Combine(dest, "WindowsLab-Uninstall.exe");
                    if (File.Exists(uninstall))
                    {
                        InstallRegistration.CreateUninstallShortcut(uninstall, spanish: true);
                    }
                }

                if (_desktopIcon.Checked)
                {
                    InstallRegistration.CreateShortcut(
                        exe,
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                        "WindowsLab.lnk",
                        "WindowsLab");
                }
            });

            _status.Text = "Listo.";
            ShowStep(4);
        }
        catch (Exception ex)
        {
            _progress.Visible = false;
            _next.Enabled = true;
            _back.Enabled = true;
            _status.Text = "Error.";
            MessageBox.Show(this,
                "La instalación falló (¿faltan permisos de administrador?).\r\n\r\n" + ex.Message,
                "WindowsLab Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _progress.Visible = false;
            _next.Enabled = true;
        }
    }
}
