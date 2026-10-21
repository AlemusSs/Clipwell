using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Clipwell
{
    // Хранит историю копирований, сохраняет её в файл и удаляет лишние записи
    public class HistoryStore
    {
        public static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clipwell");
        private static readonly string ImagesFolder = Path.Combine(Folder, "images");
        private static readonly string FilePath = Path.Combine(Folder, "history.json");

        public List<ClipItem> Items { get; private set; } = new List<ClipItem>();
        public int Limit { get; private set; } = 25;

        public void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    Items = JsonSerializer.Deserialize<List<ClipItem>>(File.ReadAllText(FilePath)) ?? new List<ClipItem>();
                }
            }
            catch
            {
                Items = new List<ClipItem>();
            }
            Trim();
        }

        public void SetLimit(int limit)
        {
            Limit = limit;
            Trim();
            Save();
        }

        public void Add(ClipItem incoming)
        {
            var existing = Items.FirstOrDefault(i => i.Hash == incoming.Hash);
            if (existing != null)
            {
                Items.Remove(existing);
                existing.CopiedAt = DateTime.Now;
                Items.Insert(0, existing);
                Save();
                return;
            }

            if (incoming.IsSecret && incoming.Text != null)
            {
                incoming.EncryptedText = Dpapi.Protect(incoming.Text);
                incoming.Text = null;
            }

            if (incoming.IsImage && incoming.ImageBytes != null)
            {
                Directory.CreateDirectory(ImagesFolder);
                incoming.ImagePath = Path.Combine(ImagesFolder, incoming.Hash + ".png");
                File.WriteAllBytes(incoming.ImagePath, incoming.ImageBytes);
            }
            incoming.ImageBytes = null;

            Items.Insert(0, incoming);
            Trim();
            Save();
        }

        public void TogglePin(ClipItem item)
        {
            item.Pinned = !item.Pinned;
            Trim();
            Save();
        }

        public void Clear()
        {
            foreach (var item in Items.Where(i => i.IsImage && !i.Pinned))
            {
                DeleteImageFile(item);
            }
            Items.RemoveAll(i => !i.Pinned);
            Save();
        }

        private void Trim()
        {
            var unpinned = Items.Where(i => !i.Pinned).ToList();
            while (unpinned.Count > Limit)
            {
                var oldest = unpinned[unpinned.Count - 1];
                unpinned.RemoveAt(unpinned.Count - 1);
                Items.Remove(oldest);
                DeleteImageFile(oldest);
            }
        }

        private static void DeleteImageFile(ClipItem item)
        {
            if (item.IsImage && !string.IsNullOrEmpty(item.ImagePath))
            {
                try { File.Delete(item.ImagePath); } catch { }
            }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
            }
        }
    }
}
