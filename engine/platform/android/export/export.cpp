/**************************************************************************/
/*  export.cpp                                                            */

#include "export.h"

#include "export_plugin.h"

#include "core/object/class_registry.h"
#include "core/os/os.h"
#include "editor/export/android_sdk_manager.h"
#include "editor/export/editor_export.h"
#include "editor/file_system/editor_paths.h"
#include "editor/settings/editor_settings.h"

void register_android_exporter_types() {
	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorExportPlatformAndroid);
}

void register_android_exporter() {
	// TODO: Move to editor_settings.cpp
	EDITOR_DEF_BASIC("export/android/debug_keystore", EditorPaths::get_singleton()->get_debug_keystore_path());
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/android/debug_keystore", PROPERTY_HINT_GLOBAL_FILE, "*.keystore,*.jks"));
	EDITOR_DEF_BASIC("export/android/debug_keystore_user", DEFAULT_ANDROID_KEYSTORE_DEBUG_USER);
	EDITOR_DEF_BASIC("export/android/debug_keystore_pass", DEFAULT_ANDROID_KEYSTORE_DEBUG_PASSWORD);
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/android/debug_keystore_pass", PROPERTY_HINT_PASSWORD));
	EDITOR_DEF_BASIC("export/android/build/automatically_delete_build_directory", false);

#ifdef ANDROID_ENABLED
	EDITOR_DEF_BASIC("export/android/install_exported_apk", !OS::get_singleton()->has_feature("horizonos"));
#else
	EDITOR_DEF("export/android/java_sdk_path", OS::get_singleton()->has_environment("JAVA_HOME") ? OS::get_singleton()->get_environment("JAVA_HOME") : EditorPaths::get_singleton()->get_default_java_sdk_path());
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/android/java_sdk_path", PROPERTY_HINT_GLOBAL_DIR));

	EDITOR_DEF("export/android/android_sdk_path", OS::get_singleton()->has_environment("ANDROID_HOME") ? OS::get_singleton()->get_environment("ANDROID_HOME") : EditorPaths::get_singleton()->get_default_android_sdk_path());
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/android/android_sdk_path", PROPERTY_HINT_GLOBAL_DIR));

	EDITOR_DEF_BASIC("export/android/scrcpy/path", "");
	EDITOR_DEF_BASIC("export/android/scrcpy/virtual_display", true);
	EDITOR_DEF_BASIC("export/android/scrcpy/no_decorations", true);
	EDITOR_DEF_BASIC("export/android/scrcpy/local_ime", true);
	EDITOR_DEF_BASIC("export/android/scrcpy/screen_size", "1920x1080/120");
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/android/scrcpy/screen_size", PROPERTY_HINT_PLACEHOLDER_TEXT, "WIDTHxHEIGHT/DPI"));

#ifdef WINDOWS_ENABLED
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/android/scrcpy_path", PROPERTY_HINT_GLOBAL_FILE, "*.exe"));
#else
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/android/scrcpy_path", PROPERTY_HINT_GLOBAL_FILE));
#endif

	EDITOR_DEF("export/android/force_system_user", false);

	EDITOR_DEF("export/android/shutdown_adb_on_exit", true);

	EDITOR_DEF("export/android/one_click_deploy_clear_previous_install", false);

	EDITOR_DEF("export/android/use_wifi_for_remote_debug", false);
	EDITOR_DEF("export/android/wifi_remote_debug_host", "localhost");
#endif

	Ref<EditorExportPlatformAndroid> exporter = Ref<EditorExportPlatformAndroid>(memnew(EditorExportPlatformAndroid));
	EditorExport::get_singleton()->add_export_platform(exporter);
}
