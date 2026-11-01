using System.Runtime.InteropServices;

namespace Orblit.Input;

/// <summary>
/// The four calls and the constants a Linux pad needs. Four integers cross the boundary, so this
/// is the whole of the interface and no native library of ours is built for it.
/// </summary>
internal static partial class LibC
{
    // Measured, not assumed: a small C program printed each of these from the kernel headers on
    // aarch64 and on x86_64, and every value is the same on both. A constant wrong by one bit shows
    // as a device that reports nothing. Other architectures build the request codes differently.
    internal const int ReadOnly = 0;
    internal const int NonBlocking = 0x800;
    internal const int CloseOnExec = 0x80000;

    internal const uint GetId = 0x80084502;
    internal const uint GetName256 = 0x81004506;

    /// <summary><c>EVIOCGKEY(96)</c>. 96 bytes is 768 bits, enough for every key code.</summary>
    internal const uint GetKeys96 = 0x80604518;

    internal const int Interrupted = 4;
    internal const int WouldBlock = 11;
    internal const int NoDevice = 19;

    /// <summary>One <c>struct input_absinfo</c>: six 32-bit fields.</summary>
    internal const int AbsInfoBytes = 24;

    /// <summary><c>EVIOCGABS(code)</c>, one request per axis.</summary>
    internal static uint GetAbs(int code) => 0x80184540 + (uint)code;

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)]
    internal static partial nint Read(int fd, Span<byte> buffer, nint count);

    // The result is dropped on purpose. A close that fails leaves nothing to retry.
    [LibraryImport("libc", EntryPoint = "close")]
    internal static partial void Close(int fd);

    // ioctl is variadic in C. It is declared with the third argument fixed, which is how the x86-64
    // and AArch64 Linux ABIs pass a variadic argument in a register. Apple's AArch64 does not, so
    // this is Linux only and not merely Linux first.
    [LibraryImport("libc", EntryPoint = "ioctl")]
    internal static partial int Ioctl(int fd, nuint request, Span<byte> argument);
}
