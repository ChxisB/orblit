/**************************************************************************/
/*  macos_utils.cpp                                                       */

#ifdef MACOS_ENABLED

#import "macos_utils.h"

#include "core/string/print_string.h"

#import <CoreFoundation/CoreFoundation.h>
#import <CoreServices/CoreServices.h>

bool macos_is_app_bundle_installed(const String &p_bundle_id) {
	CFStringRef bundle_id = CFStringCreateWithCString(nullptr, p_bundle_id.utf8().get_data(), kCFStringEncodingUTF8);
	CFArrayRef result = LSCopyApplicationURLsForBundleIdentifier(bundle_id, nullptr);
	CFRelease(bundle_id);

	if (result) {
		if (CFArrayGetCount(result) > 0) {
			CFRelease(result);
			return true;
		} else {
			CFRelease(result);
			return false;
		}
	} else {
		return false;
	}
}

#endif
