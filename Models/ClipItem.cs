using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace Clipwell
{
    public enum ClipKind
    {
        Text,
        Image
    }

    public class ClipItem
    {
        public string Hash { get; set; }
        public ClipKind Kind { get; set; }

        // Обычный текст хранится открыто. Для паролей поле пустое, см. EncryptedText.
        public string Text { get; set; }

        // Пароль из менеджера паролей: текст зашифрован средствами Windows и показан звёздочками
        public bool IsSecret { get; set; }
        public string EncryptedText { get; set; }
        public int SecretLength { get; set; }

        public string ImagePath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime CopiedAt { get; set; }
        public bool Pinned { get; set; }

        [JsonIgnore]
        public byte[] ImageBytes { get; set; }

        private BitmapImage _thumbnail;

        [JsonIgnore]
        public bool IsText => Kind == ClipKind.Text;

        [JsonIgnore]
        public bool IsImage => Kind == ClipKind.Image;

        [JsonIgnore]
        public string PreviewText
        {
            get
            {
                if (!IsText)
                {
                    return string.Empty;
                }
                if (IsSecret)
                {
                    return new string('*', Math.Min(SecretLength, 40));
                }
                return (Text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            }
        }

        [JsonIgnore]
        public string ImageCaption => $"Картинка {Width}×{Height}";

        // Текст для вставки. Для паролей расшифровывается только в момент вставки.
        public string GetText()
        {
            if (!IsSecret)
            {
                return Text ?? string.Empty;
            }
            try
            {
                return Dpapi.Unprotect(EncryptedText);
            }
            catch
            {
                return string.Empty;
            }
        }

        [JsonIgnore]
        public BitmapImage Thumbnail
        {
            get
            {
                if (_thumbnail == null && IsImage && !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 88;
                    bitmap.UriSource = new Uri(ImagePath);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    _thumbnail = bitmap;
                }
                return _thumbnail;
            }
        }
    }
}
