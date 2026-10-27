using System.Runtime.InteropServices;
using tahsinERP.Provisioning.Contracts;

namespace tahsinERP.ProvisioningAgent;

/// <summary>
/// Server resurslarini o'lchaydi (inventar bilan panelga yuboriladi): fizik RAM, mahalliy disklar, CPU, uptime.
/// CPU — GetSystemTimes ning oldingi o'lchovdan beri farqi, ya'ni inventarlar oralig'idagi o'rtacha yuklama (birinchi marta null).
/// Windows bo'lmagan muhitda (testlar) RAM/CPU 0/null qoladi.
/// </summary>
public sealed class ServerHealthProbe
{
    private (ulong Idle, ulong Total)? _prevCpu;

    public ServerHealthProbe()
    {
        if (OperatingSystem.IsWindows()) SampleCpu(); // birinchi inventarda ham agent ishga tushgandan beri o'rtacha bo'lsin
    }

    public ServerHealth Collect()
    {
        var h = new ServerHealth
        {
            MachineName = Environment.MachineName,
            Os = RuntimeInformation.OSDescription,
            CpuCount = Environment.ProcessorCount,
            UptimeSeconds = Environment.TickCount64 / 1000,
        };
        if (OperatingSystem.IsWindows())
        {
            var mem = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref mem))
            {
                h.RamTotalBytes = (long)mem.TotalPhys;
                h.RamAvailableBytes = (long)mem.AvailPhys;
            }
            h.CpuPercent = SampleCpu();
        }
        else h.RamTotalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                h.Disks.Add(new DiskHealth
                {
                    Name = d.Name, Label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? null : d.VolumeLabel,
                    TotalBytes = d.TotalSize, FreeBytes = d.AvailableFreeSpace,
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return h;
    }

    private double? SampleCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
        ulong idleT = idle.Value, total = kernel.Value + user.Value; // kernel vaqti idle'ni ham o'z ichiga oladi
        var prev = _prevCpu;
        _prevCpu = (idleT, total);
        if (prev is not { } p || total <= p.Total) return null;
        double busy = 1.0 - (double)(idleT - p.Idle) / (total - p.Total);
        return Math.Round(Math.Clamp(busy, 0, 1) * 100, 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low, High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
}
