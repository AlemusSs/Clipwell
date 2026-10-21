using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace Clipwell
{
    // Настройки приложения, сохраняются в settings.json
    public class AppSettings
    {
        public const int MinLimit = 5;
        public const int MaxLimit = 50;

        public int HistoryLimit { get; set; } = 25;
        public uint HotkeyModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
        public uint HotkeyKey { get; set; } = NativeMethods.VK_V;
        public bool StartHidden { get; set; } = true;
        public bool OpenWithoutCaret { get; set; } = false;

        [JsonIgnore]
        public string HotkeyText
        {
            get
            {
                var parts = new List<string>();
                if ((HotkeyModifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
                if ((HotkeyModifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
                if ((HotkeyModifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
                if ((HotkeyModifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
                parts.Add(KeyInterop.KeyFromVirtualKey((int)HotkeyKey).ToString());
                return string.Join(" + ", parts);
            }
        }

        private static readonly string FilePath = Path.Combine(HistoryStore.Folder, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                }
            }
            catch
            {
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(HistoryStore.Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
            }
        }
    }
}
