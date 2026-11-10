/**************************************************************************/
/*  editor_resource_conversion_plugin.cpp                                 */

#include "editor_resource_conversion_plugin.h"

#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.

void EditorResourceConversionPlugin::register_members() {
	ORBLIT_VIRTUAL_BIND(_converts_to);
	ORBLIT_VIRTUAL_BIND(_handles, "resource");
	ORBLIT_VIRTUAL_BIND(_convert, "resource");
}

String EditorResourceConversionPlugin::converts_to() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_converts_to, ret);
	return ret;
}

bool EditorResourceConversionPlugin::handles(const Ref<Resource> &p_resource) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_handles, p_resource, ret);
	return ret;
}

Ref<Resource> EditorResourceConversionPlugin::convert(const Ref<Resource> &p_resource) const {
	Ref<Resource> ret;
	ORBLIT_VIRTUAL_CALL(_convert, p_resource, ret);
	return ret;
}
