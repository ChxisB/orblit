/**************************************************************************/
/*  os_visionos.mm                                                        */

#import "os_visionos.h"

#import "display_system_visionos.h"

#ifdef VISIONOS_ENABLED

OS_VisionOS *OS_VisionOS::get_singleton() {
	return (OS_VisionOS *)OS_AppleEmbedded::get_singleton();
}

OS_VisionOS::OS_VisionOS() :
		OS_AppleEmbedded() {
	DisplaySystemVisionOS::register_visionos_driver();
}

OS_VisionOS::~OS_VisionOS() {}

String OS_VisionOS::get_name() const {
	return "visionOS";
}

#endif // VISIONOS_ENABLED
