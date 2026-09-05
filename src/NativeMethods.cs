using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace GenshinDesktopPet
{
    internal static class NativeMethods
    {
        private const int GwlExStyle = -20;
        private const long WsExTransparent = 0x00000020L;
        private const long WsExToolWindow = 0x00000080L;
        private const long WsExNoActivate = 0x08000000L;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int index, int newStyle);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr newStyle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        public static void SetClickThrough(IntPtr handle, bool enabled)
        {
            long style = IntPtr.Size == 8 ? GetWindowLongPtr64(handle, GwlExStyle).ToInt64() : GetWindowLong32(handle, GwlExStyle);
            style |= WsExToolWindow;
            // WS_EX_NOACTIVATE prevents a WPF popup from reliably receiving input on
            // some Windows builds. ShowActivated=false keeps startup unobtrusive while
            // allowing deliberate pet clicks and context-menu interaction.
            style &= ~WsExNoActivate;
            if (enabled)
            {
                style |= WsExTransparent;
            }
            else
            {
                style &= ~WsExTransparent;
            }
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(handle, GwlExStyle, new IntPtr(style));
            }
            else
            {
                SetWindowLong32(handle, GwlExStyle, unchecked((int)style));
            }
            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        public static bool IsPrimaryScreenFullscreen()
        {
            IntPtr handle = GetForegroundWindow();
            if (handle == IntPtr.Zero || !IsWindowVisible(handle))
            {
                return false;
            }
            StringBuilder className = new StringBuilder(128);
            GetClassName(handle, className, className.Capacity);
            string value = className.ToString();
            if (string.Equals(value, "Progman", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            Rect rect;
            if (!GetWindowRect(handle, out rect))
            {
                return false;
            }
            System.Drawing.Rectangle screen = WinForms.Screen.PrimaryScreen.Bounds;
            const int tolerance = 3;
            return rect.Left <= screen.Left + tolerance &&
                   rect.Top <= screen.Top + tolerance &&
                   rect.Right >= screen.Right - tolerance &&
                   rect.Bottom >= screen.Bottom - tolerance;
        }
    }
}
