using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Clipwell
{
    public static class NativeMethods
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint VK_V = 0x56;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public const byte VK_CONTROL = 0x11;
        public const byte VK_V_KEY = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // Нажимает Ctrl+V через системный ввод
        public static void PressCtrlV()
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V_KEY, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V_KEY, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static string GetTitle(IntPtr hwnd)
        {
            var builder = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, builder, builder.Capacity);
            return builder.ToString();
        }

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr dest, IntPtr src1, IntPtr src2, int mode);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        private const int RGN_OR = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public uint flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        // Есть ли в активном окне мигающий текстовый курсор (место для вставки)
        public static bool HasTextCaret(IntPtr foregroundWindow)
        {
            uint threadId = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
            if (!GetGUIThreadInfo(threadId, ref info))
            {
                return false;
            }
            return info.hwndCaret != IntPtr.Zero;
        }

        // Окно показывает только области-блоки со скруглёнными углами.
        // Всё остальное (зазоры между блоками, углы) системой не рисуется и не занимает место.
        public static void ApplyBlockRegion(Window window, IEnumerable<FrameworkElement> blocks, double radiusDip)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            double scale = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
            int ellipse = (int)Math.Round(radiusDip * 2 * scale);

            IntPtr combined = CreateRectRgn(0, 0, 0, 0);
            foreach (FrameworkElement block in blocks)
            {
                if (!block.IsVisible)
                {
                    continue;
                }

                Rect bounds = block.TransformToAncestor(window)
                    .TransformBounds(new Rect(0, 0, block.ActualWidth, block.ActualHeight));

                IntPtr part = CreateRoundRectRgn(
                    (int)Math.Round(bounds.Left * scale),
                    (int)Math.Round(bounds.Top * scale),
                    (int)Math.Round(bounds.Right * scale),
                    (int)Math.Round(bounds.Bottom * scale),
                    ellipse, ellipse);

                IntPtr result = CreateRectRgn(0, 0, 0, 0);
                CombineRgn(result, combined, part, RGN_OR);
                DeleteObject(combined);
                DeleteObject(part);
                combined = result;
            }

            SetWindowRgn(hwnd, combined, true);
        }
    }
}
