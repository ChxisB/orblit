/**************************************************************************/
/*  editor_internal_calls.cpp                                             */

#include "editor_internal_calls.h"

#include "../interop_types.h"
#include "../orblitsharp_dirs.h"
#include "../utils/path_utils.h"
#include "code_completion.h"

#ifdef ORBLIT_CSHARP_HOT_RELOAD
#include "../csharp_script.h"
#endif

#ifdef MACOS_ENABLED
#include "../utils/macos_utils.h"
#endif

#include "core/config/project_settings.h"
#include "core/os/os.h"
#include "core/version.h"
#include "editor/debugger/editor_debugger_node.h"
#include "editor/editor_node.h"
#include "editor/export/lipo.h"
#include "editor/file_system/editor_paths.h"
#include "editor/run/editor_run_bar.h"
#include "editor/script/script_editor_plugin.h"
#include "editor/settings/editor_settings.h"
#include "editor/themes/editor_scale.h"
#include "startup/main.h"

#ifdef ORBLIT_CSHARP_HOT_RELOAD
#include "core/object/callable_mp.h"
#endif

#ifdef UNIX_ENABLED
#include <unistd.h> // access
#endif

#ifdef __cplusplus
extern "C" {
#endif

void orblit_icall_OrblitSharpDirs_ResMetadataDir(orblit_string *r_dest) {
	memnew_placement(r_dest, String(OrblitSharpDirs::get_res_metadata_dir()));
}

void orblit_icall_OrblitSharpDirs_DotNetUserDir(orblit_string *r_dest) {
	memnew_placement(r_dest, String(OrblitSharpDirs::get_dotnet_user_dir()));
}

void orblit_icall_OrblitSharpDirs_BuildLogsDirs(orblit_string *r_dest) {
	memnew_placement(r_dest, String(OrblitSharpDirs::get_build_logs_dir()));
}

void orblit_icall_OrblitSharpDirs_DataEditorToolsDir(orblit_string *r_dest) {
	memnew_placement(r_dest, String(OrblitSharpDirs::get_data_editor_tools_dir()));
}

void orblit_icall_OrblitSharpDirs_CSharpProjectName(orblit_string *r_dest) {
	memnew_placement(r_dest, String(Path::get_csharp_project_name()));
}

void orblit_icall_EditorProgress_Create(const orblit_string *p_task, const orblit_string *p_label, int32_t p_amount, bool p_can_cancel) {
	String task = *reinterpret_cast<const String *>(p_task);
	String label = *reinterpret_cast<const String *>(p_label);
	EditorNode::progress_add_task(task, label, p_amount, (bool)p_can_cancel);
}

void orblit_icall_EditorProgress_Dispose(const orblit_string *p_task) {
	String task = *reinterpret_cast<const String *>(p_task);
	EditorNode::progress_end_task(task);
}

bool orblit_icall_EditorProgress_Step(const orblit_string *p_task, const orblit_string *p_state, int32_t p_step, bool p_force_refresh) {
	String task = *reinterpret_cast<const String *>(p_task);
	String state = *reinterpret_cast<const String *>(p_state);
	return EditorNode::progress_task_step(task, state, p_step, (bool)p_force_refresh);
}

void orblit_icall_Internal_FullExportTemplatesDir(orblit_string *r_dest) {
	String full_templates_dir = EditorPaths::get_singleton()->get_export_templates_dir().path_join(ORBLIT_VERSION_FULL_CONFIG);
	memnew_placement(r_dest, String(full_templates_dir));
}

bool orblit_icall_Internal_IsMacOSAppBundleInstalled(const orblit_string *p_bundle_id) {
#ifdef MACOS_ENABLED
	String bundle_id = *reinterpret_cast<const String *>(p_bundle_id);
	return (bool)macos_is_app_bundle_installed(bundle_id);
#else
	(void)p_bundle_id; // UNUSED
	return (bool)false;
#endif
}

bool orblit_icall_Internal_LipOCreateFile(const orblit_string *p_output_path, const orblit_packed_array *p_files) {
	String output_path = *reinterpret_cast<const String *>(p_output_path);
	PackedStringArray files = *reinterpret_cast<const PackedStringArray *>(p_files);
	return LipO::create_file(output_path, files);
}

bool orblit_icall_Internal_OrblitIs32Bits() {
	return sizeof(void *) == 4;
}

bool orblit_icall_Internal_OrblitIsRealTDouble() {
#ifdef REAL_T_IS_DOUBLE
	return (bool)true;
#else
	return (bool)false;
#endif
}

void orblit_icall_Internal_OrblitMainIteration() {
	Main::iteration();
}

bool orblit_icall_Internal_IsAssembliesReloadingNeeded() {
#ifdef ORBLIT_CSHARP_HOT_RELOAD
	return (bool)CSharpLanguage::get_singleton()->is_assembly_reloading_needed();
#else
	return (bool)false;
#endif
}

void orblit_icall_Internal_ReloadAssemblies() {
#ifdef ORBLIT_CSHARP_HOT_RELOAD
	callable_mp(ManagedBind::OrblitSharp::get_singleton(), &ManagedBind::OrblitSharp::reload_assemblies).call_deferred();
#endif
}

void orblit_icall_Internal_EditorDebuggerNodeReloadScripts() {
	EditorDebuggerNode::get_singleton()->reload_all_scripts();
}

bool orblit_icall_Internal_ScriptEditorEdit(Resource *p_resource, int32_t p_line, int32_t p_col, bool p_grab_focus) {
	Ref<Resource> resource = p_resource;
	return (bool)ScriptEditor::get_singleton()->edit(resource, p_line, p_col, (bool)p_grab_focus);
}

void orblit_icall_Internal_EditorNodeShowScriptScreen() {
	ScriptEditor::get_singleton()->focus_editor();
}

void orblit_icall_Internal_EditorRunPlay() {
	EditorRunBar::get_singleton()->play_main_scene();
}

void orblit_icall_Internal_EditorRunStop() {
	EditorRunBar::get_singleton()->stop_playing();
}

void orblit_icall_Internal_EditorPlugin_AddControlToEditorRunBar(Control *p_control) {
	EditorRunBar::get_singleton()->get_buttons_container()->add_child(p_control);
}

void orblit_icall_Internal_ScriptEditorDebugger_ReloadScripts() {
	EditorDebuggerNode *ed = EditorDebuggerNode::get_singleton();
	if (ed) {
		ed->reload_all_scripts();
	}
}

void orblit_icall_Internal_CodeCompletionRequest(int32_t p_kind, const orblit_string *p_script_file, orblit_packed_array *r_ret) {
	String script_file = *reinterpret_cast<const String *>(p_script_file);
	PackedStringArray suggestions = dotnet_host::get_code_completion((dotnet_host::CompletionKind)p_kind, script_file);
	memnew_placement(r_ret, PackedStringArray(suggestions));
}

float orblit_icall_Globals_EditorScale() {
	return EDSCALE;
}

void orblit_icall_Globals_GlobalDef(const orblit_string *p_setting, const orblit_variant *p_default_value, bool p_restart_if_changed, orblit_variant *r_result) {
	String setting = *reinterpret_cast<const String *>(p_setting);
	Variant default_value = *reinterpret_cast<const Variant *>(p_default_value);
	Variant result = _GLOBAL_DEF(setting, default_value, (bool)p_restart_if_changed);
	memnew_placement(r_result, Variant(result));
}

void orblit_icall_Globals_EditorDef(const orblit_string *p_setting, const orblit_variant *p_default_value, bool p_restart_if_changed, orblit_variant *r_result) {
	String setting = *reinterpret_cast<const String *>(p_setting);
	Variant default_value = *reinterpret_cast<const Variant *>(p_default_value);
	Variant result = _EDITOR_DEF(setting, default_value, (bool)p_restart_if_changed);
	memnew_placement(r_result, Variant(result));
}

void orblit_icall_Globals_EditorDefShortcut(const orblit_string *p_setting, const orblit_string *p_name, Key p_keycode, bool p_physical, orblit_variant *r_result) {
	String setting = *reinterpret_cast<const String *>(p_setting);
	String name = *reinterpret_cast<const String *>(p_name);
	Ref<Shortcut> result = ED_SHORTCUT(setting, name, p_keycode, p_physical);
	memnew_placement(r_result, Variant(result));
}

void orblit_icall_Globals_EditorGetShortcut(const orblit_string *p_setting, Ref<Shortcut> *r_result) {
	String setting = *reinterpret_cast<const String *>(p_setting);
	Ref<Shortcut> result = ED_GET_SHORTCUT(setting);
	memnew_placement(r_result, Variant(result));
}

void orblit_icall_Globals_EditorShortcutOverride(const orblit_string *p_setting, const orblit_string *p_feature, Key p_keycode, bool p_physical) {
	String setting = *reinterpret_cast<const String *>(p_setting);
	String feature = *reinterpret_cast<const String *>(p_feature);
	ED_SHORTCUT_OVERRIDE(setting, feature, p_keycode, p_physical);
}

void orblit_icall_Globals_TTR(const orblit_string *p_text, orblit_string *r_dest) {
	String text = *reinterpret_cast<const String *>(p_text);
	memnew_placement(r_dest, String(TTR(text)));
}

void orblit_icall_Utils_OS_GetPlatformName(orblit_string *r_dest) {
	String os_name = OS::get_singleton()->get_name();
	memnew_placement(r_dest, String(os_name));
}

bool orblit_icall_Utils_OS_UnixFileHasExecutableAccess(const orblit_string *p_file_path) {
#ifdef UNIX_ENABLED
	String file_path = *reinterpret_cast<const String *>(p_file_path);
	return access(file_path.utf8().get_data(), X_OK) == 0;
#else
	ERR_FAIL_V(false);
#endif
}

#ifdef __cplusplus
}
#endif

// The order in this array must match the declaration order of
// the methods in 'OrblitTools/Internals/Internal.cs'.
static const void *unmanaged_callbacks[]{
	(void *)orblit_icall_OrblitSharpDirs_ResMetadataDir,
	(void *)orblit_icall_OrblitSharpDirs_DotNetUserDir,
	(void *)orblit_icall_OrblitSharpDirs_BuildLogsDirs,
	(void *)orblit_icall_OrblitSharpDirs_DataEditorToolsDir,
	(void *)orblit_icall_OrblitSharpDirs_CSharpProjectName,
	(void *)orblit_icall_EditorProgress_Create,
	(void *)orblit_icall_EditorProgress_Dispose,
	(void *)orblit_icall_EditorProgress_Step,
	(void *)orblit_icall_Internal_FullExportTemplatesDir,
	(void *)orblit_icall_Internal_IsMacOSAppBundleInstalled,
	(void *)orblit_icall_Internal_LipOCreateFile,
	(void *)orblit_icall_Internal_OrblitIs32Bits,
	(void *)orblit_icall_Internal_OrblitIsRealTDouble,
	(void *)orblit_icall_Internal_OrblitMainIteration,
	(void *)orblit_icall_Internal_IsAssembliesReloadingNeeded,
	(void *)orblit_icall_Internal_ReloadAssemblies,
	(void *)orblit_icall_Internal_EditorDebuggerNodeReloadScripts,
	(void *)orblit_icall_Internal_ScriptEditorEdit,
	(void *)orblit_icall_Internal_EditorNodeShowScriptScreen,
	(void *)orblit_icall_Internal_EditorRunPlay,
	(void *)orblit_icall_Internal_EditorRunStop,
	(void *)orblit_icall_Internal_EditorPlugin_AddControlToEditorRunBar,
	(void *)orblit_icall_Internal_ScriptEditorDebugger_ReloadScripts,
	(void *)orblit_icall_Internal_CodeCompletionRequest,
	(void *)orblit_icall_Globals_EditorScale,
	(void *)orblit_icall_Globals_GlobalDef,
	(void *)orblit_icall_Globals_EditorDef,
	(void *)orblit_icall_Globals_EditorDefShortcut,
	(void *)orblit_icall_Globals_EditorGetShortcut,
	(void *)orblit_icall_Globals_EditorShortcutOverride,
	(void *)orblit_icall_Globals_TTR,
	(void *)orblit_icall_Utils_OS_GetPlatformName,
	(void *)orblit_icall_Utils_OS_UnixFileHasExecutableAccess,
};

const void **orblitsharp::get_editor_interop_funcs(int32_t &r_size) {
	r_size = sizeof(unmanaged_callbacks);
	return unmanaged_callbacks;
}
