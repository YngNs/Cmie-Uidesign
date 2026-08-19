using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Cmie.MotorTest.Wpf.Services;

internal static class WindowsPasswordProtector
{
    private const int CryptProtectUiForbidden = 0x1;

    public static string Protect(string value) => Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(value), protect: true));

    public static string Unprotect(string value) => Encoding.UTF8.GetString(Transform(Convert.FromBase64String(value), protect: false));

    private static byte[] Transform(byte[] input, bool protect)
    {
        var inputBlob = new DataBlob();
        var outputBlob = new DataBlob();
        try
        {
            inputBlob.Data = Marshal.AllocHGlobal(input.Length);
            inputBlob.Length = input.Length;
            Marshal.Copy(input, 0, inputBlob.Data, input.Length);
            var success = protect
                ? CryptProtectData(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outputBlob)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outputBlob);
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows 密码保护操作失败。");
            var result = new byte[outputBlob.Length];
            Marshal.Copy(outputBlob.Data, result, 0, outputBlob.Length);
            return result;
        }
        finally
        {
            if (inputBlob.Data != IntPtr.Zero) Marshal.FreeHGlobal(inputBlob.Data);
            if (outputBlob.Data != IntPtr.Zero) LocalFree(outputBlob.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Length;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn, string? description, IntPtr optionalEntropy, IntPtr reserved,
        IntPtr promptStruct, int flags, out DataBlob dataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn, IntPtr description, IntPtr optionalEntropy, IntPtr reserved,
        IntPtr promptStruct, int flags, out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
