using Orblit;
using Orblit.NativeInterop;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace OrblitTools.Internals
{
    public static class Globals
    {
        public static float EditorScale => Internal.orblit_icall_Globals_EditorScale();

        // ReSharper disable once UnusedMethodReturnValue.Global
        public static Variant GlobalDef(string setting, Variant defaultValue, bool restartIfChanged = false)
        {
            using orblit_string settingIn = Marshaling.ConvertStringToNative(setting);
            using orblit_variant defaultValueIn = defaultValue.CopyNativeVariant();
            Internal.orblit_icall_Globals_GlobalDef(settingIn, defaultValueIn, restartIfChanged,
                out orblit_variant result);
            return Variant.CreateTakingOwnershipOfDisposableValue(result);
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public static Variant EditorDef(string setting, Variant defaultValue, bool restartIfChanged = false)
        {
            using orblit_string settingIn = Marshaling.ConvertStringToNative(setting);
            using orblit_variant defaultValueIn = defaultValue.CopyNativeVariant();
            Internal.orblit_icall_Globals_EditorDef(settingIn, defaultValueIn, restartIfChanged,
                out orblit_variant result);
            return Variant.CreateTakingOwnershipOfDisposableValue(result);
        }

        public static Shortcut EditorDefShortcut(string setting, string name, Key keycode = Key.None, bool physical = false)
        {
            using orblit_string settingIn = Marshaling.ConvertStringToNative(setting);
            using orblit_string nameIn = Marshaling.ConvertStringToNative(name);
            Internal.orblit_icall_Globals_EditorDefShortcut(settingIn, nameIn, keycode, physical.ToOrblitBool(), out orblit_variant result);
            return (Shortcut)Variant.CreateTakingOwnershipOfDisposableValue(result);
        }

        public static Shortcut EditorGetShortcut(string setting)
        {
            using orblit_string settingIn = Marshaling.ConvertStringToNative(setting);
            Internal.orblit_icall_Globals_EditorGetShortcut(settingIn, out orblit_variant result);
            return (Shortcut)Variant.CreateTakingOwnershipOfDisposableValue(result);
        }

        public static void EditorShortcutOverride(string setting, string feature, Key keycode = Key.None, bool physical = false)
        {
            using orblit_string settingIn = Marshaling.ConvertStringToNative(setting);
            using orblit_string featureIn = Marshaling.ConvertStringToNative(feature);
            Internal.orblit_icall_Globals_EditorShortcutOverride(settingIn, featureIn, keycode, physical.ToOrblitBool());
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static string TTR(this string text)
        {
            using orblit_string textIn = Marshaling.ConvertStringToNative(text);
            Internal.orblit_icall_Globals_TTR(textIn, out orblit_string dest);
            using (dest)
                return Marshaling.ConvertStringToManaged(dest);
        }
    }
}
