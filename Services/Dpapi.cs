using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Clipwell
{
    // Шифрование средствами Windows (DPAPI): данные может прочитать только тот пользователь Windows, который их сохранил
    public static class Dpapi
    {
        private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DATA_BLOB dataIn, string description, IntPtr entropy,
            IntPtr reserved, IntPtr promptStruct, int flags, out DATA_BLOB dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DATA_BLOB dataIn, IntPtr description, IntPtr entropy,
            IntPtr reserved, IntPtr promptStruct, int flags, out DATA_BLOB dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        public static string Protect(string text)
        {
            return Convert.ToBase64String(Run(Encoding.UTF8.GetBytes(text), true));
        }

        public static string Unprotect(string base64)
        {
            return Encoding.UTF8.GetString(Run(Convert.FromBase64String(base64), false));
        }

        private static byte[] Run(byte[] data, bool protect)
        {
            var input = new DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
            try
            {
                Marshal.Copy(data, 0, input.pbData, data.Length);
                DATA_BLOB output;
                bool ok = protect
                    ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out output);
                if (!ok)
                {
                    throw new InvalidOperationException("Ошибка шифрования Windows: " + Marshal.GetLastWin32Error());
                }

                var result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                LocalFree(output.pbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(input.pbData);
            }
        }
    }
}
