#pragma warning disable IDE1006 // Naming rule violation
// ReSharper disable InconsistentNaming

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Orblit;
using Orblit.NativeInterop;
using Orblit.SourceGenerators.Internal;
using OrblitTools.IdeMessaging.Requests;

namespace OrblitTools.Internals
{
    [GenerateUnmanagedCallbacks(typeof(InternalUnmanagedCallbacks))]
    internal static partial class Internal
    {
        public const string CSharpLanguageType = "CSharpScript";
        public const string CSharpLanguageExtension = ".cs";

        public static string FullExportTemplatesDir
        {
            get
            {
                orblit_icall_Internal_FullExportTemplatesDir(out orblit_string dest);
                using (dest)
                    return Marshaling.ConvertStringToManaged(dest);
            }
        }

        public static string SimplifyOrblitPath(this string path) => Orblit.StringExtensions.SimplifyPath(path);

        public static bool IsMacOSAppBundleInstalled(string bundleId)
        {
            using orblit_string bundleIdIn = Marshaling.ConvertStringToNative(bundleId);
            return orblit_icall_Internal_IsMacOSAppBundleInstalled(bundleIdIn);
        }

        public static bool LipOCreateFile(string outputPath, string[] files)
        {
            using orblit_string outputPathIn = Marshaling.ConvertStringToNative(outputPath);
            using orblit_packed_string_array filesIn = Marshaling.ConvertSystemArrayToNativePackedStringArray(files);
            return orblit_icall_Internal_LipOCreateFile(outputPathIn, filesIn);
        }

        public static bool OrblitIs32Bits() => orblit_icall_Internal_OrblitIs32Bits();

        public static bool OrblitIsRealTDouble() => orblit_icall_Internal_OrblitIsRealTDouble();

        public static void OrblitMainIteration() => orblit_icall_Internal_OrblitMainIteration();

        public static bool IsAssembliesReloadingNeeded() => orblit_icall_Internal_IsAssembliesReloadingNeeded();

        public static void ReloadAssemblies() => orblit_icall_Internal_ReloadAssemblies();

        public static void EditorDebuggerNodeReloadScripts() => orblit_icall_Internal_EditorDebuggerNodeReloadScripts();

        public static bool ScriptEditorEdit(Resource resource, int line, int col, bool grabFocus = true) =>
            orblit_icall_Internal_ScriptEditorEdit(resource.NativeInstance, line, col, grabFocus);

        public static void EditorNodeShowScriptScreen() => orblit_icall_Internal_EditorNodeShowScriptScreen();

        public static void EditorRunPlay() => orblit_icall_Internal_EditorRunPlay();

        public static void EditorRunStop() => orblit_icall_Internal_EditorRunStop();

        public static void EditorPlugin_AddControlToEditorRunBar(Control control) =>
            orblit_icall_Internal_EditorPlugin_AddControlToEditorRunBar(control.NativeInstance);

        public static void ScriptEditorDebugger_ReloadScripts() =>
            orblit_icall_Internal_ScriptEditorDebugger_ReloadScripts();

        public static string[] CodeCompletionRequest(CodeCompletionRequest.CompletionKind kind,
            string scriptFile)
        {
            using orblit_string scriptFileIn = Marshaling.ConvertStringToNative(scriptFile);
            orblit_icall_Internal_CodeCompletionRequest((int)kind, scriptFileIn, out orblit_packed_string_array res);
            using (res)
                return Marshaling.ConvertNativePackedStringArrayToSystemArray(res);
        }

        #region Internal

        private static bool initialized = false;

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Global
        internal static unsafe void Initialize(IntPtr unmanagedCallbacks, int unmanagedCallbacksSize)
        {
            if (initialized)
                throw new InvalidOperationException("Already initialized.");
            initialized = true;

            if (unmanagedCallbacksSize != sizeof(InternalUnmanagedCallbacks))
                throw new ArgumentException("Unmanaged callbacks size mismatch.", nameof(unmanagedCallbacksSize));

            _unmanagedCallbacks = Unsafe.AsRef<InternalUnmanagedCallbacks>((void*)unmanagedCallbacks);
        }

        private partial struct InternalUnmanagedCallbacks
        {
        }

        /*
         * IMPORTANT:
         * The order of the methods defined in NativeFuncs must match the order
         * in the array defined at the bottom of 'editor/editor_internal_calls.cpp'.
         */

        public static partial void orblit_icall_OrblitSharpDirs_ResMetadataDir(out orblit_string r_dest);

        public static partial void orblit_icall_OrblitSharpDirs_DotNetUserDir(out orblit_string r_dest);

        public static partial void orblit_icall_OrblitSharpDirs_BuildLogsDirs(out orblit_string r_dest);

        public static partial void orblit_icall_OrblitSharpDirs_DataEditorToolsDir(out orblit_string r_dest);

        public static partial void orblit_icall_OrblitSharpDirs_CSharpProjectName(out orblit_string r_dest);

        public static partial void orblit_icall_EditorProgress_Create(in orblit_string task, in orblit_string label,
            int amount, bool canCancel);

        public static partial void orblit_icall_EditorProgress_Dispose(in orblit_string task);

        public static partial bool orblit_icall_EditorProgress_Step(in orblit_string task, in orblit_string state,
            int step,
            bool forceRefresh);

        private static partial void orblit_icall_Internal_FullExportTemplatesDir(out orblit_string dest);

        private static partial bool orblit_icall_Internal_IsMacOSAppBundleInstalled(in orblit_string bundleId);

        private static partial bool orblit_icall_Internal_LipOCreateFile(in orblit_string outputPath, in orblit_packed_string_array files);

        private static partial bool orblit_icall_Internal_OrblitIs32Bits();

        private static partial bool orblit_icall_Internal_OrblitIsRealTDouble();

        private static partial void orblit_icall_Internal_OrblitMainIteration();

        private static partial bool orblit_icall_Internal_IsAssembliesReloadingNeeded();

        private static partial void orblit_icall_Internal_ReloadAssemblies();

        private static partial void orblit_icall_Internal_EditorDebuggerNodeReloadScripts();

        private static partial bool orblit_icall_Internal_ScriptEditorEdit(IntPtr resource, int line, int col,
            bool grabFocus);

        private static partial void orblit_icall_Internal_EditorNodeShowScriptScreen();

        private static partial void orblit_icall_Internal_EditorRunPlay();

        private static partial void orblit_icall_Internal_EditorRunStop();

        private static partial void orblit_icall_Internal_EditorPlugin_AddControlToEditorRunBar(IntPtr p_control);

        private static partial void orblit_icall_Internal_ScriptEditorDebugger_ReloadScripts();

        private static partial void orblit_icall_Internal_CodeCompletionRequest(int kind, in orblit_string scriptFile,
            out orblit_packed_string_array res);

        public static partial float orblit_icall_Globals_EditorScale();

        public static partial void orblit_icall_Globals_GlobalDef(in orblit_string setting, in orblit_variant defaultValue,
            bool restartIfChanged, out orblit_variant result);

        public static partial void orblit_icall_Globals_EditorDef(in orblit_string setting, in orblit_variant defaultValue,
            bool restartIfChanged, out orblit_variant result);

        public static partial void
            orblit_icall_Globals_EditorDefShortcut(in orblit_string setting, in orblit_string name, Key keycode, orblit_bool physical, out orblit_variant result);

        public static partial void
            orblit_icall_Globals_EditorGetShortcut(in orblit_string setting, out orblit_variant result);

        public static partial void
            orblit_icall_Globals_EditorShortcutOverride(in orblit_string setting, in orblit_string feature, Key keycode, orblit_bool physical);

        public static partial void orblit_icall_Globals_TTR(in orblit_string text, out orblit_string dest);

        public static partial void orblit_icall_Utils_OS_GetPlatformName(out orblit_string dest);

        public static partial bool orblit_icall_Utils_OS_UnixFileHasExecutableAccess(in orblit_string filePath);

        #endregion
    }
}
