using System;
using System.Windows.Interop;

namespace Clipwell
{
    // Невидимое окно: получает сообщения о копировании и о нажатии горячей клавиши
    public class HotkeyWindow : IDisposable
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int WM_HOTKEY = 0x0312;
        private const int HotkeyId = 1;

        private readonly HwndSource _source;

        public event Action ClipboardChanged;
        public event Action HotkeyPressed;

        public uint Modifiers { get; private set; }
        public uint Key { get; private set; }

        public HotkeyWindow()
        {
            var parameters = new HwndSourceParameters("ClipwellHost")
            {
                ParentWindow = new IntPtr(-3),
                WindowStyle = 0
            };
            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);
            NativeMethods.AddClipboardFormatListener(_source.Handle);
        }

        // Возвращает false, если сочетание занято другой программой
        public bool Register(uint modifiers, uint key)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, modifiers, key))
            {
                return false;
            }
            Modifiers = modifiers;
            Key = key;
            return true;
        }

        // Временно отключает горячую клавишу, пока пользователь выбирает новую
        public void Suspend()
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        }

        public void Resume()
        {
            NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, Modifiers, Key);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                ClipboardChanged?.Invoke();
            }
            else if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            NativeMethods.RemoveClipboardFormatListener(_source.Handle);
            _source.Dispose();
        }
    }
}
