/**************************************************************************/
/*  export.cpp                                                            */

#include "export.h"

#include "export_plugin.h"

#include "core/object/class_registry.h"
#include "editor/export/editor_export.h"

#ifdef MACOS_ENABLED
#include "editor/settings/editor_settings.h"
#endif

void register_ios_exporter_types() {
	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorExportPlatformIOS);
}

void register_ios_exporter() {
	// TODO: Move to editor_settings.cpp
#ifdef MACOS_ENABLED
	EDITOR_DEF("export/ios/ios_deploy", "");
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/ios/ios_deploy", PROPERTY_HINT_GLOBAL_FILE, "*"));
#endif

	Ref<EditorExportPlatformIOS> platform;
	platform.instantiate();

	EditorExport::get_singleton()->add_export_platform(platform);
}
