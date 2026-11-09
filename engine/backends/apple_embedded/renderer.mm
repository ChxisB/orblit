/**************************************************************************/
/*  renderer.mm                                                           */

#import "renderer.h"

#include "core/config/project_settings.h"
#import "backends/apple_embedded/display_system_apple_embedded.h"
#import "backends/apple_embedded/os_apple_embedded.h"
#include "startup/main.h"

@interface GDTRenderer ()

@property(assign, nonatomic) BOOL hasCalledProjectDataSetUp;
@property(assign, nonatomic) BOOL hasStartedMain;
@property(assign, nonatomic) BOOL hasFinishedSetUp;

@end

@implementation GDTRenderer

- (BOOL)setUp {
	if (self.hasFinishedSetUp) {
		return NO;
	}

	if (!OS::get_singleton()) {
		exit(0);
	}

	if (!self.hasCalledProjectDataSetUp) {
		[self setUpProjectDataShowingBootLogo:YES];
	}

	if (!self.hasStartedMain) {
		[self startMain];
	}

	self.hasFinishedSetUp = YES;

	return NO;
}

- (void)setUpProjectDataShowingBootLogo:(BOOL)p_show_boot_logo {
	self.hasCalledProjectDataSetUp = YES;
	safeDispatchSyncToMain(^{
		Main::setup2(p_show_boot_logo);

		// this might be necessary before here
		NSDictionary *dict = [[NSBundle mainBundle] infoDictionary];
		for (NSString *key in dict) {
			NSObject *value = [dict objectForKey:key];
			String ukey = String::utf8([key UTF8String]);

			// we need a NSObject to Variant conversor

			if ([value isKindOfClass:[NSString class]]) {
				NSString *str = (NSString *)value;
				String uval = String::utf8([str UTF8String]);

				ProjectSettings::get_singleton()->set("Info.plist/" + ukey, uval);

			} else if ([value isKindOfClass:[NSNumber class]]) {
				NSNumber *n = (NSNumber *)value;
				double dval = [n doubleValue];

				ProjectSettings::get_singleton()->set("Info.plist/" + ukey, dval);
			}
			// do stuff
		}
	});
}

- (void)startMain {
	self.hasStartedMain = YES;
	safeDispatchSyncToMain(^{
		OS_AppleEmbedded::get_singleton()->start();
	});
}

@end
