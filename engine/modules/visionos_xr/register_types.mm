/**************************************************************************/
/*  register_types.mm                                                     */

#ifdef VISIONOS_ENABLED

#include "register_types.h"

#include "visionos_xr_interface.h"

#include "core/object/class_registry.h"

Ref<VisionOSXRInterface> visionos_xr;

void initialize_visionos_xr_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ORBLIT_REGISTER_CLASS(VisionOSXRInterface);

	if (XRSystem::get_singleton()) {
		visionos_xr.instantiate();
		XRSystem::get_singleton()->add_interface(visionos_xr);
	}
}

void uninitialize_visionos_xr_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	if (visionos_xr.is_valid()) {
		// uninitialize our interface if it is initialized
		if (visionos_xr->is_initialized()) {
			visionos_xr->uninitialize();
		}

		// unregister our interface from the XR server
		if (XRSystem::get_singleton()) {
			XRSystem::get_singleton()->remove_interface(visionos_xr);
		}

		// and release
		visionos_xr.unref();
	}
}

#endif // VISIONOS_ENABLED
