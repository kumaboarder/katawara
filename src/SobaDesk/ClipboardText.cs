using System.Runtime.InteropServices;
using System.Text;

namespace SobaDesk;

internal static class ClipboardText
{
    public static bool TrySet(string text)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            WinClipboard.SetText(text ?? "");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static class WinClipboard
    {
        private const uint CfUnicodeText = 13;
        private const uint GmemMoveable = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        public static void SetText(string text)
        {
            var bytes = Encoding.Unicode.GetBytes(text + "\0");
            var handle = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
            if (handle == IntPtr.Zero) throw new InvalidOperationException("クリップボード用メモリを取れません");
            var locked = GlobalLock(handle);
            if (locked == IntPtr.Zero)
            {
                GlobalFree(handle);
                throw new InvalidOperationException("クリップボード用メモリをロックできません");
            }
            Marshal.Copy(bytes, 0, locked, bytes.Length);
            GlobalUnlock(handle);

            if (!OpenWithRetry())
            {
                GlobalFree(handle);
                throw new InvalidOperationException("クリップボードを開けません");
            }
            try
            {
                EmptyClipboard();
                if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
                {
                    GlobalFree(handle);
                    throw new InvalidOperationException("クリップボードに書けません");
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        private static bool OpenWithRetry()
        {
            for (var i = 0; i < 12; i++)
            {
                if (OpenClipboard(IntPtr.Zero)) return true;
                Thread.Sleep(15);
            }
            return false;
        }
    }
}
