/**************************************************************************/
/*  export.cpp                                                            */

#include "export.h"

#include "export_plugin.h"

#include "core/object/class_registry.h"
#include "editor/export/editor_export.h"

void register_visionos_exporter_types() {
	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorExportPlatformVisionOS);
}

void register_visionos_exporter() {
	Ref<EditorExportPlatformVisionOS> platform;
	platform.instantiate();

	EditorExport::get_singleton()->add_export_platform(platform);
}
