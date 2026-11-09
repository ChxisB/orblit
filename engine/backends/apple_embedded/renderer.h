/**************************************************************************/
/*  renderer.h                                                            */

#pragma once

#import <Foundation/Foundation.h>

inline void safeDispatchSyncToMain(void (^block)(void)) {
	if ([NSThread isMainThread]) {
		block();
	} else {
		dispatch_sync(dispatch_get_main_queue(), block);
	}
}

@interface GDTRenderer : NSObject

@property(assign, readonly, nonatomic) BOOL hasFinishedSetup;

- (BOOL)setUp;
- (void)setUpProjectDataShowingBootLogo:(BOOL)p_show_boot_logo;

@end
