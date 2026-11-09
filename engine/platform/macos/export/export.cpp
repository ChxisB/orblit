/**************************************************************************/
/*  export.cpp                                                            */

#include "export.h"

#include "export_plugin.h"

#include "core/object/class_registry.h"
#include "editor/export/editor_export.h"
#include "editor/settings/editor_settings.h"

void register_macos_exporter_types() {
	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorExportPlatformMacOS);
}

void register_macos_exporter() {
	// TODO: Move to editor_settings.cpp
#ifndef ANDROID_ENABLED
	EDITOR_DEF_BASIC("export/macos/rcodesign", "");
#ifdef WINDOWS_ENABLED
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/macos/rcodesign", PROPERTY_HINT_GLOBAL_FILE, "*.exe"));
#else
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/macos/rcodesign", PROPERTY_HINT_GLOBAL_FILE));
#endif
	EDITOR_DEF_BASIC("export/macos/actool", "");
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/macos/actool", PROPERTY_HINT_GLOBAL_FILE));
#endif

	Ref<EditorExportPlatformMacOS> platform;
	platform.instantiate();

	EditorExport::get_singleton()->add_export_platform(platform);
}
