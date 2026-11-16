using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LenovoController
{
    public static class Native
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFileW(
            [MarshalAs(UnmanagedType.LPWStr)] string filename,
            uint access,
            uint share,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            ref byte InBuffer,
            int nInBufferSize,
            out uint OutBuffer,
            int nOutBufferSize,
            out int pBytesReturned,
            IntPtr lpOverlapped);

        [StructLayout(LayoutKind.Sequential)]
        public struct Margins
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, IntPtr processId);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenuW(IntPtr menu, uint flags, uint itemId, string item);

        [DllImport("user32.dll")]
        public static extern int TrackPopupMenuEx(
            IntPtr menu,
            uint flags,
            int x,
            int y,
            IntPtr owner,
            IntPtr parameters);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    }
}