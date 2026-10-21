using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace Clipwell
{
    // Читает текущее содержимое буфера обмена (текст или картинку)
    public static class ClipboardReader
    {
        public static ClipItem Read()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string text = System.Windows.Clipboard.GetText();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return null;
                    }

                    // Менеджеры паролей помечают такое копирование специальными форматами (как Win+V)
                    bool secret = System.Windows.Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")
                        || System.Windows.Clipboard.ContainsData("CanIncludeInClipboardHistory");

                    return new ClipItem
                    {
                        Kind = ClipKind.Text,
                        Text = text,
                        IsSecret = secret,
                        SecretLength = text.Length,
                        // Для пароля хеш не считаем: по нему можно было бы подобрать пароль
                        Hash = secret ? Guid.NewGuid().ToString("N") : Sha(Encoding.UTF8.GetBytes(text)),
                        CopiedAt = DateTime.Now
                    };
                }

                if (System.Windows.Clipboard.ContainsImage())
                {
                    BitmapSource image = System.Windows.Clipboard.GetImage();
                    if (image == null)
                    {
                        return null;
                    }
                    byte[] png = EncodePng(image);
                    return new ClipItem
                    {
                        Kind = ClipKind.Image,
                        ImageBytes = png,
                        Width = image.PixelWidth,
                        Height = image.PixelHeight,
                        Hash = Sha(png),
                        CopiedAt = DateTime.Now
                    };
                }
            }
            catch (COMException)
            {
                // Буфер занят другой программой, пропускаем это копирование
            }
            return null;
        }

        private static byte[] EncodePng(BitmapSource image)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        private static string Sha(byte[] data)
        {
            return Convert.ToHexString(SHA256.HashData(data));
        }
    }
}
