/**************************************************************************/
/*  app_delegate_service_apple_embedded.h                                 */

#pragma once

#import <UIKit/UIKit.h>

@class GDTViewController;

@interface GDTAppDelegateService : NSObject <UIApplicationDelegate, UIWindowSceneDelegate>

@property(weak, class, nonatomic, nullable) GDTViewController *viewController;

@end
