using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TimeTraking.Services
{
    public class RunningProcessInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? ExecutablePath { get; set; }
        public bool HasVisibleWindow { get; set; }
        public bool IsForeground { get; set; }
    }

    public static class WindowsProcessHelper
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;

        private static readonly Dictionary<string, Bitmap?> IconCache = new(StringComparer.OrdinalIgnoreCase);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFOHEADER lpbi, uint uUsage);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        public static int? GetForegroundProcessId()
        {
            if (!OperatingSystem.IsWindows())
                return null;

            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            _ = GetWindowThreadProcessId(hwnd, out uint pid);
            return pid > 0 ? (int)pid : null;
        }

        public static string? TryGetProcessExecutablePath(Process process)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            IntPtr hProcess = IntPtr.Zero;
            try
            {
                hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
                if (hProcess == IntPtr.Zero)
                    return null;

                var buffer = new StringBuilder(1024);
                int size = buffer.Capacity;
                if (QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                {
                    return buffer.ToString();
                }
            }
            catch
            {
                // Access denied or process exited
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                    CloseHandle(hProcess);
            }

            return null;
        }

        public static Dictionary<string, RunningProcessInfo> GetRunningProcessesSnapshot()
        {
            var result = new Dictionary<string, RunningProcessInfo>(StringComparer.OrdinalIgnoreCase);
            int? foregroundPid = GetForegroundProcessId();
            int currentPid = Environment.ProcessId;

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.Id == currentPid || proc.Id == 0)
                        continue;

                    string name = proc.ProcessName;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    bool hasWindow = proc.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(proc.MainWindowTitle);
                    bool isForeground = foregroundPid.HasValue && proc.Id == foregroundPid.Value;

                    if (!result.TryGetValue(name, out var info))
                    {
                        string? exePath = TryGetProcessExecutablePath(proc);
                        string displayName = GetFriendlyName(name, exePath);

                        info = new RunningProcessInfo
                        {
                            ProcessName = name,
                            DisplayName = displayName,
                            ExecutablePath = exePath,
                            HasVisibleWindow = hasWindow,
                            IsForeground = isForeground
                        };
                        result[name] = info;
                    }
                    else
                    {
                        if (hasWindow)
                            info.HasVisibleWindow = true;
                        if (isForeground)
                            info.IsForeground = true;
                        if (string.IsNullOrEmpty(info.ExecutablePath))
                        {
                            info.ExecutablePath = TryGetProcessExecutablePath(proc);
                            if (!string.IsNullOrEmpty(info.ExecutablePath))
                                info.DisplayName = GetFriendlyName(name, info.ExecutablePath);
                        }
                    }
                }
                catch
                {
                    // Process exited during enumeration
                }
                finally
                {
                    proc.Dispose();
                }
            }

            return result;
        }

        public static string GetFriendlyName(string processName, string? exePath)
        {
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                        return versionInfo.FileDescription.Trim();
                    if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
                        return versionInfo.ProductName.Trim();
                }
                catch
                {
                    // Fallback to process name
                }
            }

            return processName;
        }

        public static Bitmap? GetIconForExecutable(string? exePath)
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                return null;

            if (IconCache.TryGetValue(exePath, out var cached))
                return cached;

            Bitmap? bitmap = ExtractIconBitmap(exePath);
            IconCache[exePath] = bitmap;
            return bitmap;
        }

        private static Bitmap? ExtractIconBitmap(string filePath)
        {
            IntPtr hIcon = IntPtr.Zero;
            IntPtr hbmColor = IntPtr.Zero;
            IntPtr hbmMask = IntPtr.Zero;
            IntPtr hdc = IntPtr.Zero;

            try
            {
                var shinfo = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(
                    filePath,
                    0,
                    out shinfo,
                    (uint)Marshal.SizeOf<SHFILEINFO>(),
                    SHGFI_ICON | SHGFI_LARGEICON);

                if (res == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                    return null;

                hIcon = shinfo.hIcon;

                if (!GetIconInfo(hIcon, out var iconInfo))
                    return null;

                hbmColor = iconInfo.hbmColor;
                hbmMask = iconInfo.hbmMask;

                if (hbmColor == IntPtr.Zero)
                    return null;

                if (GetObject(hbmColor, Marshal.SizeOf<BITMAP>(), out var bmp) == 0)
                    return null;

                int width = bmp.bmWidth;
                int height = bmp.bmHeight;
                if (width <= 0 || height <= 0)
                    return null;

                var bmi = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // top-down DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB
                };

                byte[] pixels = new byte[width * height * 4];
                hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero)
                    return null;

                int scanLines = GetDIBits(hdc, hbmColor, 0, (uint)height, pixels, ref bmi, DIB_RGB_COLORS);
                if (scanLines == 0)
                    return null;

                // Check if alpha channel is completely empty (older non-alpha icons)
                bool hasAlpha = false;
                for (int i = 3; i < pixels.Length; i += 4)
                {
                    if (pixels[i] != 0)
                    {
                        hasAlpha = true;
                        break;
                    }
                }

                if (!hasAlpha && hbmMask != IntPtr.Zero)
                {
                    byte[] maskPixels = new byte[width * height * 4];
                    var maskBmi = bmi;
                    if (GetDIBits(hdc, hbmMask, 0, (uint)height, maskPixels, ref maskBmi, DIB_RGB_COLORS) > 0)
                    {
                        for (int i = 0; i < pixels.Length; i += 4)
                        {
                            // In AND mask, white (255) is transparent, black (0) is opaque
                            pixels[i + 3] = maskPixels[i] == 0 ? (byte)255 : (byte)0;
                        }
                    }
                    else
                    {
                        for (int i = 3; i < pixels.Length; i += 4)
                            pixels[i] = 255;
                    }
                }

                var writeable = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Unpremul);

                using (var fb = writeable.Lock())
                {
                    int rowBytes = width * 4;
                    if (fb.RowBytes == rowBytes)
                    {
                        Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
                    }
                    else
                    {
                        for (int y = 0; y < height; y++)
                        {
                            Marshal.Copy(pixels, y * rowBytes, fb.Address + y * fb.RowBytes, rowBytes);
                        }
                    }
                }

                return writeable;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hdc != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, hdc);
                if (hbmColor != IntPtr.Zero)
                    DeleteObject(hbmColor);
                if (hbmMask != IntPtr.Zero)
                    DeleteObject(hbmMask);
                if (hIcon != IntPtr.Zero)
                    DestroyIcon(hIcon);
            }
        }
    }
}

