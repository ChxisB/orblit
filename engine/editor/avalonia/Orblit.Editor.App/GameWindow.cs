using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Orblit.Editor.App;

/// <summary>
/// Brings the window of a game the editor started to the front. The game is another app, so this asks the system
/// for it. macOS and Windows let the app in front hand that place to another. Linux desktops each have their own
/// rules, so there it does nothing.
/// </summary>
internal static partial class GameWindow
{
    const string Objc = "/usr/lib/libobjc.A.dylib";
    const string User32 = "user32.dll";

    // NSApplicationActivateAllWindows: every window of the game comes forward, not only its last one.
    const nuint AllWindows = 1;

    // SW_RESTORE: a game window that was made small comes back at its old size.
    const int Restore = 9;

    /// <summary>Whether this system lets the editor bring another app's window to the front.</summary>
    internal static bool CanRaise => OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    /// <summary>Brings the windows of the process with this id to the front. Does nothing where <see cref="CanRaise"/> is false.</summary>
    internal static void Raise(int processId)
    {
        if (OperatingSystem.IsMacOS())
        {
            RaiseOnMac(processId);
        }
        else if (OperatingSystem.IsWindows())
        {
            RaiseOnWindows(processId);
        }
    }

    // Since macOS 14 the app in front yields first, or the system may keep it in front.
    static void RaiseOnMac(int processId)
    {
        var game = SendInt(Class("NSRunningApplication"), Selector("runningApplicationWithProcessIdentifier:"), processId);
        if (game == 0)
        {
            return;
        }

        var editor = Send(Class("NSApplication"), Selector("sharedApplication"));
        var yieldTo = Selector("yieldActivationToApplication:");
        if (SendFlag(editor, Selector("respondsToSelector:"), yieldTo))
        {
            SendPointer(editor, yieldTo, game);
        }

        SendOptions(game, Selector("activateWithOptions:"), AllWindows);
    }

    static void RaiseOnWindows(int processId)
    {
        try
        {
            using var game = Process.GetProcessById(processId);
            var window = game.MainWindowHandle;
            if (window == 0)
            {
                return;
            }

            if (IsIconic(window))
            {
                ShowWindow(window, Restore);
            }

            SetForegroundWindow(window);
        }
        catch (ArgumentException)
        {
            // The game ended between the press and the call, so there is no window to raise.
        }
    }

    static nint Class(string name) => objc_getClass(name);

    static nint Selector(string name) => sel_registerName(name);

    [LibraryImport(Objc, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint objc_getClass(string name);

    [LibraryImport(Objc, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint sel_registerName(string name);

    [LibraryImport(Objc, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    [LibraryImport(Objc, EntryPoint = "objc_msgSend")]
    private static partial nint SendInt(nint receiver, nint selector, int value);

    [LibraryImport(Objc, EntryPoint = "objc_msgSend")]
    private static partial nint SendPointer(nint receiver, nint selector, nint value);

    [LibraryImport(Objc, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool SendFlag(nint receiver, nint selector, nint value);

    [LibraryImport(Objc, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool SendOptions(nint receiver, nint selector, nuint options);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint window);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint window, int command);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);
}
