using System.Runtime.InteropServices;
using WindowsLab.Core;

namespace WindowsLab.Audit;

internal static class DxgiGpuProbe
{
    private static readonly Guid Factory1Iid = new("770aae78-f26f-4dba-a829-253c83d1b387");

    public static IReadOnlyList<GpuInventory> Read()
    {
        try
        {
            return ReadCore();
        }
        catch (SEHException)
        {
            return [];
        }
        catch (DllNotFoundException)
        {
            return [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static List<GpuInventory> ReadCore()
    {
        var list = new List<GpuInventory>();
        var iid = Factory1Iid;
        var hr = CreateDXGIFactory1(ref iid, out var factoryPtr);
        if (hr < 0 || factoryPtr == IntPtr.Zero)
        {
            return list;
        }

        try
        {
            for (uint i = 0; i < 8; i++)
            {
                var enumHr = EnumAdapter1(factoryPtr, i, out var adapterPtr);
                if (enumHr < 0 || adapterPtr == IntPtr.Zero)
                {
                    break;
                }

                try
                {
                    if (!TryGetDescription(adapterPtr, out var name, out var vendorId, out var dedicated))
                    {
                        continue;
                    }

                    if (name.Contains("Microsoft Basic Render", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Microsoft Basic Display", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    list.Add(new GpuInventory(
                        name.Trim(),
                        VendorName(vendorId),
                        dedicated,
                        "dxgi",
                        ProbeStatus.Ok));
                }
                finally
                {
                    Marshal.Release(adapterPtr);
                }
            }
        }
        finally
        {
            Marshal.Release(factoryPtr);
        }

        return list;
    }

    private static string VendorName(uint vendorId) => vendorId switch
    {
        0x10DE => "NVIDIA",
        0x1002 => "AMD",
        0x8086 => "Intel",
        0x1414 => "Microsoft",
        _ => "Unknown"
    };

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1([In] ref Guid riid, out IntPtr ppFactory);

    private static int EnumAdapter1(IntPtr factory, uint index, out IntPtr adapter)
    {
        adapter = IntPtr.Zero;
        var vtable = Marshal.ReadIntPtr(factory);
        // IDXGIFactory1.EnumAdapters1 is slot 12
        // (IUnknown 3 + IDXGIObject 4 + IDXGIFactory 5 = 12).
        var fnPtr = Marshal.ReadIntPtr(vtable, 12 * IntPtr.Size);
        var invoke = Marshal.GetDelegateForFunctionPointer<EnumAdapters1Delegate>(fnPtr);
        return invoke(factory, index, out adapter);
    }

    private static bool TryGetDescription(IntPtr adapter, out string name, out uint vendorId, out long dedicated)
    {
        name = string.Empty;
        vendorId = 0;
        dedicated = 0;
        var vtable = Marshal.ReadIntPtr(adapter);
        // IDXGIAdapter.GetDesc is slot 8 (IUnknown 3 + IDXGIObject 4 + EnumOutputs 1).
        var fnPtr = Marshal.ReadIntPtr(vtable, 8 * IntPtr.Size);
        var invoke = Marshal.GetDelegateForFunctionPointer<GetDescDelegate>(fnPtr);
        var desc = new DxgiAdapterDesc();
        var hr = invoke(adapter, out desc);
        if (hr < 0)
        {
            return false;
        }

        name = desc.Description.TrimEnd('\0');
        vendorId = desc.VendorId;
        dedicated = (long)desc.DedicatedVideoMemory;
        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Delegate(IntPtr factory, uint adapter, out IntPtr ppAdapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDescDelegate(IntPtr adapter, out DxgiAdapterDesc desc);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDesc
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public long AdapterLuid;
    }
}
