using System;
using System.IO;

namespace Clipwell
{
    // Простой журнал событий для поиска ошибок: %LOCALAPPDATA%\Clipwell\log.txt
    public static class Log
    {
        private static readonly string FilePath = Path.Combine(HistoryStore.Folder, "log.txt");

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(HistoryStore.Folder);
                File.AppendAllText(FilePath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
