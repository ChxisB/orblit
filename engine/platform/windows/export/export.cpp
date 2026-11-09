/**************************************************************************/
/*  export.cpp                                                            */

#include "export.h"

#include "export_plugin.h"

#include "core/object/class_registry.h"
#include "editor/export/editor_export.h"
#include "editor/settings/editor_settings.h"

void register_windows_exporter_types() {
	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorExportPlatformWindows);
}

void register_windows_exporter() {
	// TODO: Move to editor_settings.cpp
#ifdef WINDOWS_ENABLED
	EDITOR_DEF_BASIC("export/windows/signtool", "");
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/windows/signtool", PROPERTY_HINT_GLOBAL_FILE, "*.exe"));
#else
	EDITOR_DEF_BASIC("export/windows/osslsigncode", "");
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/windows/osslsigncode", PROPERTY_HINT_GLOBAL_FILE));
#endif

	Ref<EditorExportPlatformWindows> platform;
	platform.instantiate();
	platform->set_name("Windows Desktop");
	platform->set_os_name("Windows");

	EditorExport::get_singleton()->add_export_platform(platform);
}
