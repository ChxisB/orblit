/**************************************************************************/
/*  view_controller.mm                                                    */

#import "view_controller.h"

#include "core/config/project_settings.h"
#import "backends/apple_embedded/display_system_apple_embedded.h"
#import "backends/apple_embedded/keyboard_input_view.h"
#import "backends/apple_embedded/view_apple_embedded.h"
#import "backends/apple_embedded/view_renderer.h"
#import "backends/apple_embedded/key_mapping_apple_embedded.h"
#import "backends/apple_embedded/os_apple_embedded.h"
#include "systems/camera/camera_system.h"

#import <AVFoundation/AVFoundation.h>
#import <GameController/GameController.h>
#import <objc/runtime.h>

@interface GDTViewController () <GDTViewDelegate>

@property(strong, nonatomic) GDTViewRenderer *renderer;
@property(strong, nonatomic) GDTKeyboardInputView *keyboardView;

@property(strong, nonatomic) UIView *orblitLoadingOverlay;

@end

@implementation GDTViewController

- (GDTView *)orblitView {
	return (GDTView *)self.view;
}

- (void)pressesBegan:(NSSet<UIPress *> *)presses withEvent:(UIPressesEvent *)event {
	[super pressesBegan:presses withEvent:event];

	if (!DisplaySystemAppleEmbedded::get_singleton() || DisplaySystemAppleEmbedded::get_singleton()->is_keyboard_active()) {
		return;
	}
	if (@available(iOS 13.4, *)) {
		for (UIPress *press in presses) {
			String u32lbl = String::utf8([press.key.charactersIgnoringModifiers UTF8String]);
			String u32text = String::utf8([press.key.characters UTF8String]);
			Key key = KeyMappingAppleEmbedded::remap_key(press.key.keyCode);

			if (press.key.keyCode == 0 && u32text.is_empty() && u32lbl.is_empty()) {
				continue;
			}

			char32_t us = 0;
			if (!u32lbl.is_empty() && !u32lbl.begins_with("UIKey")) {
				us = u32lbl[0];
			}

			KeyLocation location = KeyMappingAppleEmbedded::key_location(press.key.keyCode);

			if (!u32text.is_empty() && !u32text.begins_with("UIKey")) {
				for (int i = 0; i < u32text.length(); i++) {
					const char32_t c = u32text[i];
					DisplaySystemAppleEmbedded::get_singleton()->key(fix_keycode(us, key), c, fix_key_label(us, key), key, press.key.modifierFlags, true, location);
				}
			} else {
				DisplaySystemAppleEmbedded::get_singleton()->key(fix_keycode(us, key), 0, fix_key_label(us, key), key, press.key.modifierFlags, true, location);
			}
		}
	}
}

- (void)pressesEnded:(NSSet<UIPress *> *)presses withEvent:(UIPressesEvent *)event {
	[super pressesEnded:presses withEvent:event];

	if (!DisplaySystemAppleEmbedded::get_singleton() || DisplaySystemAppleEmbedded::get_singleton()->is_keyboard_active()) {
		return;
	}
	if (@available(iOS 13.4, *)) {
		for (UIPress *press in presses) {
			String u32lbl = String::utf8([press.key.charactersIgnoringModifiers UTF8String]);
			Key key = KeyMappingAppleEmbedded::remap_key(press.key.keyCode);

			if (press.key.keyCode == 0 && u32lbl.is_empty()) {
				continue;
			}

			char32_t us = 0;
			if (!u32lbl.is_empty() && !u32lbl.begins_with("UIKey")) {
				us = u32lbl[0];
			}

			KeyLocation location = KeyMappingAppleEmbedded::key_location(press.key.keyCode);

			DisplaySystemAppleEmbedded::get_singleton()->key(fix_keycode(us, key), 0, fix_key_label(us, key), key, press.key.modifierFlags, false, location);
		}
	}
}

- (void)loadView {
	GDTView *view = GDTViewCreate();
	GDTViewRenderer *renderer = [[GDTViewRenderer alloc] init];

	self.renderer = renderer;
	self.view = view;

	view.renderer = self.renderer;
	view.delegate = self;
}

- (instancetype)initWithNibName:(NSString *)nibNameOrNil bundle:(NSBundle *)nibBundleOrNil {
	self = [super initWithNibName:nibNameOrNil bundle:nibBundleOrNil];

	if (self) {
		[self orblit_commonInit];
	}

	return self;
}

- (instancetype)initWithCoder:(NSCoder *)coder {
	self = [super initWithCoder:coder];

	if (self) {
		[self orblit_commonInit];
	}

	return self;
}

- (void)orblit_commonInit {
	// Initialize view controller values.
}

- (void)didReceiveMemoryWarning {
	[super didReceiveMemoryWarning];
	print_verbose("Did receive memory warning!");
}

- (void)viewDidLoad {
	[super viewDidLoad];

	[self observeKeyboard];
	[self displayLoadingOverlay];

	[self setNeedsUpdateOfScreenEdgesDeferringSystemGestures];
}

- (void)viewDidAppear:(BOOL)animated {
	[super viewDidAppear:animated];
	[self.orblitView startRendering];
#ifdef IOS_ENABLED
	[self propagateUIPreferencesToRootViewController];
#endif
}

- (void)viewDidDisappear:(BOOL)animated {
	[self.orblitView stopRendering];
	[super viewDidDisappear:animated];
}

- (void)observeKeyboard {
	print_verbose("Setting up keyboard input view.");
	self.keyboardView = [GDTKeyboardInputView new];
	[self.view addSubview:self.keyboardView];

	print_verbose("Adding observer for keyboard show/hide.");
	[[NSNotificationCenter defaultCenter]
			addObserver:self
			   selector:@selector(keyboardOnScreen:)
				   name:UIKeyboardDidShowNotification
				 object:nil];
	[[NSNotificationCenter defaultCenter]
			addObserver:self
			   selector:@selector(keyboardHidden:)
				   name:UIKeyboardDidHideNotification
				 object:nil];
}

- (void)displayLoadingOverlay {
#if !defined(VISIONOS_ENABLED)
	NSBundle *bundle = [NSBundle mainBundle];
	NSString *storyboardName = @"Launch Screen";

	if ([bundle pathForResource:storyboardName ofType:@"storyboardc"] == nil) {
		return;
	}

	UIStoryboard *launchStoryboard = [UIStoryboard storyboardWithName:storyboardName bundle:bundle];

	UIViewController *controller = [launchStoryboard instantiateInitialViewController];
	self.orblitLoadingOverlay = controller.view;
	self.orblitLoadingOverlay.frame = self.view.bounds;
	self.orblitLoadingOverlay.autoresizingMask = UIViewAutoresizingFlexibleHeight | UIViewAutoresizingFlexibleWidth;

	[self.view addSubview:self.orblitLoadingOverlay];
#endif
}

- (BOOL)orblitViewFinishedSetup:(GDTView *)view {
	[self.orblitLoadingOverlay removeFromSuperview];
	self.orblitLoadingOverlay = nil;

	return YES;
}

- (void)dealloc {
	self.keyboardView = nil;

	self.renderer = nil;

	if (self.orblitLoadingOverlay) {
		[self.orblitLoadingOverlay removeFromSuperview];
		self.orblitLoadingOverlay = nil;
	}

	[[NSNotificationCenter defaultCenter] removeObserver:self];
}

// Propagate UI preferences to the root VC (the SwiftUI hosting controller doesn't delegate these).
#ifdef IOS_ENABLED
- (void)propagateUIPreferencesToRootViewController {
	UIViewController *rootVC = self.view.window.rootViewController;
	if (!rootVC || rootVC == self) {
		return;
	}

	Class rootClass = [rootVC class];

	SEL selectors[] = {
		@selector(preferredScreenEdgesDeferringSystemGestures),
		@selector(prefersHomeIndicatorAutoHidden),
		@selector(prefersStatusBarHidden),
		@selector(shouldAutorotate),
		@selector(supportedInterfaceOrientations),
	};

	for (SEL sel : selectors) {
		Method method = class_getInstanceMethod([GDTViewController class], sel);
		if (!class_addMethod(rootClass, sel, method_getImplementation(method), method_getTypeEncoding(method))) {
			method_setImplementation(class_getInstanceMethod(rootClass, sel), method_getImplementation(method));
		}
	}

	[rootVC setNeedsUpdateOfScreenEdgesDeferringSystemGestures];
	[rootVC setNeedsUpdateOfHomeIndicatorAutoHidden];
	[rootVC setNeedsStatusBarAppearanceUpdate];
}
#endif

// MARK: Orientation

#ifdef IOS_ENABLED
- (void)viewWillTransitionToSize:(CGSize)size withTransitionCoordinator:(id<UIViewControllerTransitionCoordinator>)coordinator {
	[super viewWillTransitionToSize:size withTransitionCoordinator:coordinator];

	[coordinator animateAlongsideTransition:nil
								 completion:^(id<UIViewControllerTransitionCoordinatorContext> context) {
									 // Get the new interface orientation after rotation completes (iOS only)
									 UIInterfaceOrientation orientation = self.view.window.windowScene.interfaceOrientation;

									 // Notify camera server of orientation change
									 CameraSystem *camera_system = CameraSystem::get_singleton();
									 if (camera_system) {
										 camera_system->handle_display_rotation_change((int)orientation);
									 }

									 DisplaySystem *display_system = DisplaySystem::get_singleton();
									 if (display_system) {
										 int out = DisplaySystemEnums::SENSOR_ORIENTATION_UNDEFINED;
										 if (UIInterfaceOrientationIsPortrait(orientation)) {
											 out = DisplaySystemEnums::SENSOR_ORIENTATION_PORTRAIT;
										 } else if (UIInterfaceOrientationIsLandscape(orientation)) {
											 out = DisplaySystemEnums::SENSOR_ORIENTATION_LANDSCAPE;
										 }
										 display_system->emit_signal("orientation_changed", out);
									 }
								 }];
}
#endif

- (UIRectEdge)preferredScreenEdgesDeferringSystemGestures {
	if (GLOBAL_GET("display/window/ios/suppress_ui_gesture")) {
		return UIRectEdgeAll;
	} else {
		return UIRectEdgeNone;
	}
}

- (BOOL)shouldAutorotate {
	if (!DisplaySystemAppleEmbedded::get_singleton()) {
		return NO;
	}

	switch (DisplaySystemAppleEmbedded::get_singleton()->screen_get_orientation(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW)) {
		case DisplaySystemEnums::SCREEN_SENSOR:
		case DisplaySystemEnums::SCREEN_SENSOR_LANDSCAPE:
		case DisplaySystemEnums::SCREEN_SENSOR_PORTRAIT:
			return YES;
		default:
			return NO;
	}
}

- (UIInterfaceOrientationMask)supportedInterfaceOrientations {
	if (!DisplaySystemAppleEmbedded::get_singleton()) {
		return UIInterfaceOrientationMaskAll;
	}

	switch (DisplaySystemAppleEmbedded::get_singleton()->screen_get_orientation(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW)) {
		case DisplaySystemEnums::SCREEN_PORTRAIT:
			return UIInterfaceOrientationMaskPortrait;
		case DisplaySystemEnums::SCREEN_REVERSE_LANDSCAPE:
			if (UIDevice.currentDevice.userInterfaceIdiom == UIUserInterfaceIdiomPad) {
				return UIInterfaceOrientationMaskLandscapeLeft;
			} else {
				return UIInterfaceOrientationMaskLandscapeRight;
			}
		case DisplaySystemEnums::SCREEN_REVERSE_PORTRAIT:
			return UIInterfaceOrientationMaskPortraitUpsideDown;
		case DisplaySystemEnums::SCREEN_SENSOR_LANDSCAPE:
			return UIInterfaceOrientationMaskLandscape;
		case DisplaySystemEnums::SCREEN_SENSOR_PORTRAIT:
			return UIInterfaceOrientationMaskPortrait | UIInterfaceOrientationMaskPortraitUpsideDown;
		case DisplaySystemEnums::SCREEN_SENSOR:
			return UIInterfaceOrientationMaskAll;
		case DisplaySystemEnums::SCREEN_LANDSCAPE:
			if (UIDevice.currentDevice.userInterfaceIdiom == UIUserInterfaceIdiomPad) {
				return UIInterfaceOrientationMaskLandscapeRight;
			} else {
				return UIInterfaceOrientationMaskLandscapeLeft;
			}
	}
}

- (BOOL)prefersStatusBarHidden {
	if (GLOBAL_GET("display/window/ios/hide_status_bar")) {
		return YES;
	} else {
		return NO;
	}
}

- (BOOL)prefersHomeIndicatorAutoHidden {
	if (GLOBAL_GET("display/window/ios/hide_home_indicator")) {
		return YES;
	} else {
		return NO;
	}
}

// MARK: Keyboard

- (void)keyboardOnScreen:(NSNotification *)notification {
	NSDictionary *info = notification.userInfo;
	NSValue *value = info[UIKeyboardFrameEndUserInfoKey];

	CGRect rawFrame = [value CGRectValue];
	CGRect keyboardFrame = [self.view convertRect:rawFrame fromView:nil];

	if (DisplaySystemAppleEmbedded::get_singleton()) {
		DisplaySystemAppleEmbedded::get_singleton()->virtual_keyboard_set_height(keyboardFrame.size.height);
	}
}

- (void)keyboardHidden:(NSNotification *)notification {
	if (DisplaySystemAppleEmbedded::get_singleton()) {
		DisplaySystemAppleEmbedded::get_singleton()->virtual_keyboard_set_height(0);
	}
}

@end
