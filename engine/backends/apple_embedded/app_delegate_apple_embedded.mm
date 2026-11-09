/**************************************************************************/
/*  app_delegate_apple_embedded.mm                                        */

#import "app_delegate_apple_embedded.h"

#include "core/typedefs.h"
#import "backends/apple_embedded/app_delegate_service_apple_embedded.h"

@implementation GDTAppDelegate

static NSMutableArray<GDTAppDelegateServiceProtocol *> *services = nil;

+ (NSArray<GDTAppDelegateServiceProtocol *> *)services {
	return services;
}

+ (void)load {
	services = [NSMutableArray new];
	// Add the specific GDTAppDelegateService subclass in each inheriting platform
}

+ (void)addService:(GDTAppDelegateServiceProtocol *)service {
	if (!services || !service) {
		return;
	}
	[services addObject:service];
}

// UIApplicationDelegate documentation can be found here: https://developer.apple.com/documentation/uikit/uiapplicationdelegate

// MARK: Window

- (UIWindow *)window {
	UIWindow *result = nil;

	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		UIWindow *value = [service window];

		if (value) {
			result = value;
		}
	}

	return result;
}

// MARK: Initializing

- (BOOL)application:(UIApplication *)application willFinishLaunchingWithOptions:(NSDictionary<UIApplicationLaunchOptionsKey, id> *)launchOptions {
	BOOL result = NO;

	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		if ([service application:application willFinishLaunchingWithOptions:launchOptions]) {
			result = YES;
		}
	}

	return result;
}

- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary<UIApplicationLaunchOptionsKey, id> *)launchOptions {
	BOOL result = NO;

	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		if ([service application:application didFinishLaunchingWithOptions:launchOptions]) {
			result = YES;
		}
	}

	return result;
}

// MARK: Scene

- (UISceneConfiguration *)application:(UIApplication *)application configurationForConnectingSceneSession:(UISceneSession *)connectingSceneSession options:(UISceneConnectionOptions *)options API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
	UISceneConfiguration *config = [[UISceneConfiguration alloc] initWithName:@"Default Configuration" sessionRole:connectingSceneSession.role];
	config.delegateClass = [GDTAppDelegate class];
	return config;
}

- (void)application:(UIApplication *)application didDiscardSceneSessions:(NSSet<UISceneSession *> *)sceneSessions API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
}

- (void)scene:(UIScene *)scene willConnectToSession:(UISceneSession *)session options:(UISceneConnectionOptions *)connectionOptions API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service scene:scene willConnectToSession:session options:connectionOptions];
	}
}

- (void)scene:(UIScene *)scene openURLContexts:(NSSet<UIOpenURLContext *> *)URLContexts API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service scene:scene openURLContexts:URLContexts];
	}
}

- (void)scene:(UIScene *)scene continueUserActivity:(NSUserActivity *)userActivity API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service scene:scene continueUserActivity:userActivity];
	}
}

// MARK: Life-Cycle

- (void)sceneDidDisconnect:(UIScene *)scene API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service sceneDidDisconnect:scene];
	}
}

- (void)sceneDidBecomeActive:(UIScene *)scene API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service sceneDidBecomeActive:scene];
	}
}

- (void)sceneWillResignActive:(UIScene *)scene API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service sceneWillResignActive:scene];
	}
}

- (void)sceneDidEnterBackground:(UIScene *)scene API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service sceneDidEnterBackground:scene];
	}
}

- (void)sceneWillEnterForeground:(UIScene *)scene API_AVAILABLE(ios(13.0), tvos(13.0), visionos(1.0)) {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service sceneWillEnterForeground:scene];
	}
}

// UIApplication lifecycle has become deprecated in favor of UIScene lifecycle
ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wdeprecated-declarations")

- (void)applicationDidBecomeActive:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationDidBecomeActive:application];
	}
}

- (void)applicationWillResignActive:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationWillResignActive:application];
	}
}

- (void)applicationDidEnterBackground:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationDidEnterBackground:application];
	}
}

- (void)applicationWillEnterForeground:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationWillEnterForeground:application];
	}
}

- (void)applicationWillTerminate:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationWillTerminate:application];
	}
}

// MARK: Environment Changes

- (void)applicationProtectedDataDidBecomeAvailable:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationProtectedDataDidBecomeAvailable:application];
	}
}

- (void)applicationProtectedDataWillBecomeUnavailable:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationProtectedDataWillBecomeUnavailable:application];
	}
}

- (void)applicationDidReceiveMemoryWarning:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationDidReceiveMemoryWarning:application];
	}
}

- (void)applicationSignificantTimeChange:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationSignificantTimeChange:application];
	}
}

// MARK: App State Restoration

- (BOOL)application:(UIApplication *)application shouldSaveSecureApplicationState:(NSCoder *)coder API_AVAILABLE(ios(13.2)) {
	BOOL result = NO;

	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		if ([service application:application shouldSaveSecureApplicationState:coder]) {
			result = YES;
		}
	}

	return result;
}

- (BOOL)application:(UIApplication *)application shouldRestoreSecureApplicationState:(NSCoder *)coder API_AVAILABLE(ios(13.2)) {
	BOOL result = NO;

	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		if ([service application:application shouldRestoreSecureApplicationState:coder]) {
			result = YES;
		}
	}

	return result;
}

- (UIViewController *)application:(UIApplication *)application viewControllerWithRestorationIdentifierPath:(NSArray<NSString *> *)identifierComponents coder:(NSCoder *)coder {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		UIViewController *controller = [service application:application viewControllerWithRestorationIdentifierPath:identifierComponents coder:coder];

		if (controller) {
			return controller;
		}
	}

	return nil;
}

- (void)application:(UIApplication *)application willEncodeRestorableStateWithCoder:(NSCoder *)coder {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service application:application willEncodeRestorableStateWithCoder:coder];
	}
}

- (void)application:(UIApplication *)application didDecodeRestorableStateWithCoder:(NSCoder *)coder {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service application:application didDecodeRestorableStateWithCoder:coder];
	}
}

// MARK: Download Data in Background

- (void)application:(UIApplication *)application handleEventsForBackgroundURLSession:(NSString *)identifier completionHandler:(void (^)(void))completionHandler {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service application:application handleEventsForBackgroundURLSession:identifier completionHandler:completionHandler];
	}

	completionHandler();
}

// MARK: Remote Notification

// Moved to the iOS Plugin

// MARK: User Activity and Handling Quick Actions

- (BOOL)application:(UIApplication *)application willContinueUserActivityWithType:(NSString *)userActivityType {
	BOOL result = NO;

	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		if ([service application:application willContinueUserActivityWithType:userActivityType]) {
			result = YES;
		}
	}

	return result;
}

- (BOOL)application:(UIApplication *)application continueUserActivity:(NSUserActivity *)userActivity restorationHandler:(void (^)(NSArray<id<UIUserActivityRestoring>> *restorableObjects))restorationHandler {
	BOOL result = NO;

	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		if ([service application:application continueUserActivity:userActivity restorationHandler:restorationHandler]) {
			result = YES;
		}
	}

	return result;
}

- (void)application:(UIApplication *)application didUpdateUserActivity:(NSUserActivity *)userActivity {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service application:application didUpdateUserActivity:userActivity];
	}
}

- (void)application:(UIApplication *)application didFailToContinueUserActivityWithType:(NSString *)userActivityType error:(NSError *)error {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service application:application didFailToContinueUserActivityWithType:userActivityType error:error];
	}
}

- (void)application:(UIApplication *)application performActionForShortcutItem:(UIApplicationShortcutItem *)shortcutItem completionHandler:(void (^)(BOOL succeeded))completionHandler {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service application:application performActionForShortcutItem:shortcutItem completionHandler:completionHandler];
	}
}

// MARK: WatchKit

- (void)application:(UIApplication *)application handleWatchKitExtensionRequest:(NSDictionary *)userInfo reply:(void (^)(NSDictionary *replyInfo))reply {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service application:application handleWatchKitExtensionRequest:userInfo reply:reply];
	}
}

// MARK: HealthKit

- (void)applicationShouldRequestHealthAuthorization:(UIApplication *)application {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service applicationShouldRequestHealthAuthorization:application];
	}
}

// MARK: Opening an URL

- (BOOL)application:(UIApplication *)app openURL:(NSURL *)url options:(NSDictionary<UIApplicationOpenURLOptionsKey, id> *)options {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		if ([service application:app openURL:url options:options]) {
			return YES;
		}
	}

	return NO;
}

// MARK: Disallowing Specified App Extension Types

- (BOOL)application:(UIApplication *)application shouldAllowExtensionPointIdentifier:(UIApplicationExtensionPointIdentifier)extensionPointIdentifier {
	BOOL result = NO;

	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		if ([service application:application shouldAllowExtensionPointIdentifier:extensionPointIdentifier]) {
			result = YES;
		}
	}

	return result;
}

// MARK: SiriKit

- (id)application:(UIApplication *)application handlerForIntent:(INIntent *)intent {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		id result = [service application:application handlerForIntent:intent];

		if (result) {
			return result;
		}
	}

	return nil;
}

// MARK: CloudKit

- (void)application:(UIApplication *)application userDidAcceptCloudKitShareWithMetadata:(CKShareMetadata *)cloudKitShareMetadata {
	for (GDTAppDelegateServiceProtocol *service in services) {
		if (![service respondsToSelector:_cmd]) {
			continue;
		}

		[service application:application userDidAcceptCloudKitShareWithMetadata:cloudKitShareMetadata];
	}
}

/* Handled By Info.plist file for now

// MARK: Interface Geometry

- (UIInterfaceOrientationMask)application:(UIApplication *)application supportedInterfaceOrientationsForWindow:(UIWindow *)window {}

*/

@end

ORBLIT_CLANG_WARNING_POP
