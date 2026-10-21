using System;
using Microsoft.Win32;

namespace Clipwell
{
    // Автозапуск вместе с Windows через раздел реестра «Run» текущего пользователя
    public static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Clipwell";

        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                return key?.GetValue(ValueName) != null;
            }
        }

        // hidden = true: при запуске с Windows программа сразу уходит в трей, без окна
        public static void Set(bool enabled, bool hidden)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null)
                {
                    return;
                }
                if (enabled)
                {
                    string command = "\"" + Environment.ProcessPath + "\"" + (hidden ? " --tray" : "");
                    key.SetValue(ValueName, command);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
