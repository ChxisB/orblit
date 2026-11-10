/**************************************************************************/
/*  export.cpp                                                            */

#include "export.h"

#include "export_plugin.h"

#include "core/object/class_registry.h"
#include "editor/export/editor_export.h"
#include "editor/settings/editor_settings.h"

void register_web_exporter_types() {
	ORBLIT_REGISTER_VIRTUAL_CLASS(EditorExportPlatformWeb);
}

void register_web_exporter() {
	// TODO: Move to editor_settings.cpp
	EDITOR_DEF("export/web/http_host", "localhost");
	EDITOR_DEF("export/web/http_port", 8060);
	EDITOR_DEF("export/web/use_tls", false);
	EDITOR_DEF("export/web/tls_key", "");
	EDITOR_DEF("export/web/tls_certificate", "");
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::INT, "export/web/http_port", PROPERTY_HINT_RANGE, "1,65535,1"));
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/web/tls_key", PROPERTY_HINT_GLOBAL_FILE, "*.key"));
	EditorSettings::get_singleton()->add_property_hint(PropertyInfo(Variant::STRING, "export/web/tls_certificate", PROPERTY_HINT_GLOBAL_FILE, "*.crt,*.pem"));

	Ref<EditorExportPlatformWeb> platform;
	platform.instantiate();
	EditorExport::get_singleton()->add_export_platform(platform);
}
