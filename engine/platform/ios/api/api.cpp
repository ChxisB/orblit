/**************************************************************************/
/*  api.cpp                                                               */

#include "api.h"

#if defined(IOS_ENABLED)

void register_ios_api() {
	orblit_apple_embedded_plugins_initialize();
}

void unregister_ios_api() {
	orblit_apple_embedded_plugins_deinitialize();
}

#else

void register_ios_api() {}
void unregister_ios_api() {}

#endif // IOS_ENABLED
