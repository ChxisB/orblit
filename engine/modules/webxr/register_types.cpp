/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "webxr_interface.h"

#ifdef WEB_ENABLED
#include "webxr_interface_js.h"
#endif

#include "core/object/class_registry.h"

#ifdef WEB_ENABLED
Ref<WebXRInterfaceJS> webxr;
#endif

void initialize_webxr_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ORBLIT_REGISTER_ABSTRACT_CLASS(WebXRInterface);

#ifdef WEB_ENABLED
	if (XRSystem::get_singleton()) {
		webxr.instantiate();
		XRSystem::get_singleton()->add_interface(webxr);
	}
#endif
}

void uninitialize_webxr_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

#ifdef WEB_ENABLED
	if (webxr.is_valid()) {
		// uninitialize our interface if it is initialized
		if (webxr->is_initialized()) {
			webxr->uninitialize();
		}

		// unregister our interface from the XR server
		if (XRSystem::get_singleton()) {
			XRSystem::get_singleton()->remove_interface(webxr);
		}

		// and release
		webxr.unref();
	}
#endif
}
