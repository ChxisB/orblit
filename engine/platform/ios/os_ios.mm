/**************************************************************************/
/*  os_ios.mm                                                             */

#import "os_ios.h"

#import "display_system_ios.h"

#ifdef IOS_ENABLED

OS_IOS *OS_IOS::get_singleton() {
	return (OS_IOS *)OS_AppleEmbedded::get_singleton();
}

OS_IOS::OS_IOS() :
		OS_AppleEmbedded() {
	DisplaySystemIOS::register_ios_driver();
}

OS_IOS::~OS_IOS() {}

String OS_IOS::get_name() const {
	return "iOS";
}

#endif // IOS_ENABLED
