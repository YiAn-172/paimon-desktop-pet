using System;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace GenshinDesktopPet
{
    internal sealed class GlobalHotkeyWindow : WinForms.NativeWindow, IDisposable
    {
        private const int HotkeyId = 0x504D;
        private const int WmHotkey = 0x0312;
        private const uint ModControl = 0x0002;
        private const uint ModNoRepeat = 0x4000;
        private const uint VirtualKeyL = 0x4C;
        private bool disposed;

        public bool Registered { get; private set; }
        public event EventHandler ToggleQuickChatRequested;

        public GlobalHotkeyWindow()
        {
            WinForms.CreateParams parameters = new WinForms.CreateParams();
            parameters.Caption = "GenshinDesktopPetHotkey";
            parameters.Parent = new IntPtr(-3);
            CreateHandle(parameters);
        }

        public bool SetEnabled(bool enabled)
        {
            if (disposed || Handle == IntPtr.Zero) return false;
            if (enabled)
            {
                if (!Registered) Registered = RegisterHotKey(Handle, HotkeyId, ModControl | ModNoRepeat, VirtualKeyL);
            }
            else if (Registered)
            {
                UnregisterHotKey(Handle, HotkeyId);
                Registered = false;
            }
            return enabled ? Registered : !Registered;
        }

        public bool RunRegistrationSelfTest()
        {
            bool wasRegistered = Registered;
            bool registered = SetEnabled(true);
            if (!wasRegistered) SetEnabled(false);
            return registered;
        }

        protected override void WndProc(ref WinForms.Message message)
        {
            if (message.Msg == WmHotkey && message.WParam.ToInt32() == HotkeyId)
            {
                EventHandler handler = ToggleQuickChatRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            }
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Handle != IntPtr.Zero)
            {
                if (Registered) UnregisterHotKey(Handle, HotkeyId);
                DestroyHandle();
            }
            Registered = false;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr handle, int id);
    }
}
