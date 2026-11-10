/**************************************************************************/
/*  app_delegate_apple_embedded.h                                         */

#pragma once

#import <UIKit/UIKit.h>

typedef NSObject<UIApplicationDelegate, UIWindowSceneDelegate> GDTAppDelegateServiceProtocol;

@interface GDTAppDelegate : NSObject <UIApplicationDelegate, UIWindowSceneDelegate>

@property(class, readonly, strong) NSArray<GDTAppDelegateServiceProtocol *> *services;

+ (void)addService:(GDTAppDelegateServiceProtocol *)service;

@end
