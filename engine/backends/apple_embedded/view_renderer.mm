/**************************************************************************/
/*  view_renderer.mm                                                      */

#import "view_renderer.h"

#import "backends/apple_embedded/display_system_apple_embedded.h"
#import "backends/apple_embedded/os_apple_embedded.h"
#include "core/config/project_settings.h"
#include "core/os/keyboard.h"
#include "core/os/os.h"
#include "startup/main.h"
#include "systems/audio/audio_system.h"

#import <AudioToolbox/AudioServices.h>
#import <CoreMotion/CoreMotion.h>
#import <GameController/GameController.h>
#import <QuartzCore/QuartzCore.h>
#import <UIKit/UIKit.h>

@implementation GDTViewRenderer

- (void)renderOnView:(UIView *)view {
	safeDispatchSyncToMain(^{
		if (!OS_AppleEmbedded::get_singleton()) {
			return;
		}
		OS_AppleEmbedded::get_singleton()->iterate();
	});
}

@end
