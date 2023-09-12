using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TwitchPlaysBot.Windows
{
    public abstract class ExternalWrappers
    {
        [DllImport("user32")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32")]
        public static extern int SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, WindowShowStyle nCmdShow);

        [DllImport("user32")]
        public static extern bool GetWindowRect(IntPtr hWnd, ref Rect rect);

        [DllImport("user32")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32")]
        public static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
    }
}
