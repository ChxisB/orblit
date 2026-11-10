/**************************************************************************/
/*  apple_embedded.mm                                                     */

#import "apple_embedded.h"

#import "backends/apple_embedded/app_delegate_service_apple_embedded.h"
#import "backends/apple_embedded/view_controller.h"
#include "core/object/class_registry.h"

#import <CoreHaptics/CoreHaptics.h>
#import <UIKit/UIKit.h>
#include <sys/sysctl.h>

void AppleEmbedded::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_rate_url", "app_id"), &AppleEmbedded::get_rate_url);
	ClassRegistry::bind_method(D_METHOD("supports_haptic_engine"), &AppleEmbedded::supports_haptic_engine);
	ClassRegistry::bind_method(D_METHOD("start_haptic_engine"), &AppleEmbedded::start_haptic_engine);
	ClassRegistry::bind_method(D_METHOD("stop_haptic_engine"), &AppleEmbedded::stop_haptic_engine);
}

bool AppleEmbedded::supports_haptic_engine() {
	if (@available(iOS 13, *)) {
		id<CHHapticDeviceCapability> capabilities = [CHHapticEngine capabilitiesForHardware];
		return capabilities.supportsHaptics;
	}

	return false;
}

CHHapticEngine *AppleEmbedded::get_haptic_engine_instance() API_AVAILABLE(ios(13)) {
	if (haptic_engine == nullptr) {
		NSError *error = nullptr;
		haptic_engine = [[CHHapticEngine alloc] initAndReturnError:&error];

		if (!error) {
			[haptic_engine setAutoShutdownEnabled:true];
		} else {
			haptic_engine = nullptr;
			NSLog(@"Could not initialize haptic engine: %@", error);
		}
	}

	return haptic_engine;
}

void AppleEmbedded::vibrate_haptic_engine(float p_duration_seconds, float p_amplitude) API_AVAILABLE(ios(13)) {
	if (@available(iOS 13, *)) { // We need the @available check every time to make the compiler happy...
		if (supports_haptic_engine()) {
			CHHapticEngine *cur_haptic_engine = get_haptic_engine_instance();
			if (cur_haptic_engine) {
				NSDictionary *hapticDict;
				if (p_amplitude < 0) {
					hapticDict = @{
						CHHapticPatternKeyPattern : @[
							@{CHHapticPatternKeyEvent : @{
								CHHapticPatternKeyEventType : CHHapticEventTypeHapticContinuous,
								CHHapticPatternKeyTime : @(CHHapticTimeImmediate),
								CHHapticPatternKeyEventDuration : @(p_duration_seconds),
							},
							},
						],
					};
				} else {
					hapticDict = @{
						CHHapticPatternKeyPattern : @[
							@{CHHapticPatternKeyEvent : @{
								CHHapticPatternKeyEventType : CHHapticEventTypeHapticContinuous,
								CHHapticPatternKeyTime : @(CHHapticTimeImmediate),
								CHHapticPatternKeyEventDuration : @(p_duration_seconds),
								CHHapticPatternKeyEventParameters : @[
									@{
										CHHapticPatternKeyParameterID : @("HapticIntensity"),
										CHHapticPatternKeyParameterValue : @(p_amplitude)
									},
								],
							},
							},
						],
					};
				}

				NSError *error = nil;
				CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithDictionary:hapticDict error:&error];
				if (error) {
					NSLog(@"Could not vibrate using haptic engine: %@", error);
					return;
				}
				id player = [cur_haptic_engine createPlayerWithPattern:pattern error:&error];
				if (error) {
					NSLog(@"Could not vibrate using haptic engine: %@", error);
					return;
				}
				[player startAtTime:0 error:&error];
				if (error) {
					NSLog(@"Could not vibrate using haptic engine: %@", error);
					return;
				}
			}

			return;
		}
	}

	NSLog(@"Haptic engine is not supported");
}

void AppleEmbedded::start_haptic_engine() {
	if (@available(iOS 13, *)) {
		if (supports_haptic_engine()) {
			CHHapticEngine *cur_haptic_engine = get_haptic_engine_instance();
			if (cur_haptic_engine) {
				[cur_haptic_engine startWithCompletionHandler:^(NSError *returnedError) {
					if (returnedError) {
						NSLog(@"Could not start haptic engine: %@", returnedError);
					}
				}];
			}

			return;
		}
	}

	NSLog(@"Haptic engine is not supported");
}

void AppleEmbedded::stop_haptic_engine() {
	if (@available(iOS 13, *)) {
		if (supports_haptic_engine()) {
			CHHapticEngine *cur_haptic_engine = get_haptic_engine_instance();
			if (cur_haptic_engine) {
				[cur_haptic_engine stopWithCompletionHandler:^(NSError *returnedError) {
					if (returnedError) {
						NSLog(@"Could not stop haptic engine: %@", returnedError);
					}
				}];
			}

			return;
		}
	}

	NSLog(@"Haptic engine is not supported");
}

void AppleEmbedded::alert(const char *p_alert, const char *p_title) {
	NSString *title = [NSString stringWithUTF8String:p_title];
	NSString *message = [NSString stringWithUTF8String:p_alert];

	UIAlertController *alert = [UIAlertController alertControllerWithTitle:title message:message preferredStyle:UIAlertControllerStyleAlert];
	UIAlertAction *button = [UIAlertAction actionWithTitle:@"OK"
													 style:UIAlertActionStyleCancel
												   handler:^(id){
												   }];

	[alert addAction:button];

	[GDTAppDelegateService.viewController presentViewController:alert animated:YES completion:nil];
}

String AppleEmbedded::get_model() const {
	// [[UIDevice currentDevice] model] only returns "iPad" or "iPhone".
	size_t size;
	sysctlbyname("hw.machine", nullptr, &size, nullptr, 0);
	char *model = (char *)malloc(size);
	if (model == nullptr) {
		return "";
	}
	sysctlbyname("hw.machine", model, &size, nullptr, 0);
	NSString *platform = [NSString stringWithCString:model encoding:NSUTF8StringEncoding];
	free(model);
	const char *str = [platform UTF8String];
	return String::utf8(str != nullptr ? str : "");
}

String AppleEmbedded::get_rate_url(int p_app_id) const {
	String app_url_path = "itms-apps://itunes.apple.com/app/idAPP_ID";

	String ret = app_url_path.replace("APP_ID", String::num_int64(p_app_id));

	print_verbose(vformat("Returning rate url %s", ret));
	return ret;
}

AppleEmbedded::AppleEmbedded() {}
