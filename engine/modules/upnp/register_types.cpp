/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "upnp.h"
#include "upnp_device.h"

#ifndef WEB_ENABLED
#include "upnp_device_miniupnp.h"
#include "upnp_miniupnp.h"
#endif

#include "core/object/class_registry.h"

void initialize_upnp_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ClassRegistry::register_custom_instance_class<UPNP>();
	ClassRegistry::register_custom_instance_class<UPNPDevice>();

#ifndef WEB_ENABLED
	UPNPMiniUPNP::make_default();
	UPNPDeviceMiniUPNP::make_default();
#endif
}

void uninitialize_upnp_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
