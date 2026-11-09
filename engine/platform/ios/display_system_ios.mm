/**************************************************************************/
/*  display_system_ios.mm                                                 */

#import "display_system_ios.h"

#import "device_metrics.h"

#import "backends/apple_embedded/app_delegate_service_apple_embedded.h"
#import "backends/apple_embedded/view_apple_embedded.h"
#import "backends/apple_embedded/view_controller.h"

#import <UIKit/UIKit.h>
#import <sys/utsname.h>

DisplaySystemIOS *DisplaySystemIOS::get_singleton() {
	return (DisplaySystemIOS *)DisplaySystemAppleEmbedded::get_singleton();
}

DisplaySystemIOS::DisplaySystemIOS(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error) :
		DisplaySystemAppleEmbedded(p_rendering_driver, p_mode, p_vsync_mode, p_flags, p_position, p_resolution, p_screen, p_context, p_parent_window, r_error) {
}

DisplaySystemIOS::~DisplaySystemIOS() {
}

DisplaySystem *DisplaySystemIOS::create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error) {
	return memnew(DisplaySystemIOS(p_rendering_driver, p_mode, p_vsync_mode, p_flags, p_position, p_resolution, p_screen, p_context, p_parent_window, r_error));
}

void DisplaySystemIOS::register_ios_driver() {
	register_create_function("iOS", create_func, get_rendering_drivers_func);
}

String DisplaySystemIOS::get_name() const {
	return "iOS";
}

int DisplaySystemIOS::screen_get_dpi(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, 72);

	struct utsname systemInfo;
	uname(&systemInfo);

	NSString *string = [NSString stringWithCString:systemInfo.machine encoding:NSUTF8StringEncoding];
	print_line(String::utf8([string UTF8String]));
#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 270100
	if (@available(iOS 27.1, *)) {
		if (!GDTAppDelegateService.viewController.orblitView.hingeClosed) {
			NSDictionary *iOSModelToDPI = [GDTDeviceMetrics dpiListInternal];
			for (NSArray *keyArray in iOSModelToDPI) {
				if ([keyArray containsObject:string]) {
					NSNumber *value = iOSModelToDPI[keyArray];
					return [value intValue];
				}
			}
		}
	}
#endif
	{
		NSDictionary *iOSModelToDPI = [GDTDeviceMetrics dpiList];
		for (NSArray *keyArray in iOSModelToDPI) {
			if ([keyArray containsObject:string]) {
				NSNumber *value = iOSModelToDPI[keyArray];
				return [value intValue];
			}
		}
	}

	// If device wasn't found in dictionary
	// make a best guess from device metrics.
	CGFloat scale = [UIScreen mainScreen].scale;

	UIUserInterfaceIdiom idiom = [UIDevice currentDevice].userInterfaceIdiom;

	switch (idiom) {
		case UIUserInterfaceIdiomPad:
			return scale == 2 ? 264 : 132;
		case UIUserInterfaceIdiomPhone: {
			if (scale == 3) {
				CGFloat nativeScale = [UIScreen mainScreen].nativeScale;
				return nativeScale == 3 ? 458 : 401;
			}

			return 326;
		}
		default:
			return 72;
	}
}

float DisplaySystemIOS::screen_get_refresh_rate(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, SCREEN_REFRESH_RATE_FALLBACK);

	float fps = [UIScreen mainScreen].maximumFramesPerSecond;
	if ([NSProcessInfo processInfo].lowPowerModeEnabled) {
		fps = 60;
	}
	return fps;
}

float DisplaySystemIOS::screen_get_scale(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, 1.0f);

	return [UIScreen mainScreen].scale;
}

bool DisplaySystemIOS::_screen_hdr_is_supported() const {
	if (@available(iOS 16.0, *)) {
		return [UIScreen mainScreen].potentialEDRHeadroom > 1.0;
	}
	return false;
}

float DisplaySystemIOS::_screen_potential_edr_headroom() const {
	if (@available(iOS 16.0, *)) {
		return [UIScreen mainScreen].potentialEDRHeadroom;
	}
	return 1.0f;
}

float DisplaySystemIOS::_screen_current_edr_headroom() const {
	if (@available(iOS 16.0, *)) {
		return [UIScreen mainScreen].currentEDRHeadroom;
	}
	return 1.0f;
}
