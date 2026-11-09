/**************************************************************************/
/*  view_apple_embedded.mm                                                */

#import "view_apple_embedded.h"

#import "backends/apple_embedded/display_layer_apple_embedded.h"
#import "backends/apple_embedded/display_system_apple_embedded.h"
#import "backends/apple_embedded/view_renderer.h"
#include "core/config/project_settings.h"
#include "core/os/keyboard.h"
#include "core/string/ustring.h"

#import <CoreMotion/CoreMotion.h>

static const int max_touches = 32;
static const float earth_gravity = 9.80665;

@interface GDTView () {
	UITouch *orblit_touches[max_touches];
	CGFloat last_edr_headroom;
	NSTimeInterval begin_timestamp[max_touches];
	CGPoint begin_delta[max_touches];
	int touch_count[max_touches];
}

@property(assign, nonatomic) BOOL isActive;

// CADisplayLink available on 3.1+ synchronizes the animation timer & drawing with the refresh rate of the display, only supports animation intervals of 1/60 1/30 & 1/15
@property(strong, nonatomic) CADisplayLink *displayLink;

// An animation timer that, when animation is started, will periodically call -drawView at the given rate.
// Only used if CADisplayLink is not
@property(strong, nonatomic) NSTimer *animationTimer;

@property(strong, nonatomic) CALayer<GDTDisplayLayer> *renderingLayer;

@property(strong, nonatomic) CMMotionManager *motionManager;

@property(assign, nonatomic) BOOL delegateDidFinishSetUp;

@end

@implementation GDTView

// Implemented in subclasses
- (CALayer<GDTDisplayLayer> *)initializeRenderingForDriver:(NSString *)driverName {
	return nil;
}

- (instancetype)initWithCoder:(NSCoder *)coder {
	self = [super initWithCoder:coder];

	if (self) {
		[self orblit_commonInit];
	}

	return self;
}

- (instancetype)initWithFrame:(CGRect)frame {
	self = [super initWithFrame:frame];

	if (self) {
		[self orblit_commonInit];
	}

	return self;
}

- (void)dealloc {
	[self stopRendering];

	self.renderer = nil;
	self.delegate = nil;

	if (self.renderingLayer) {
		[self.renderingLayer removeFromSuperlayer];
		self.renderingLayer = nil;
	}

	if (self.motionManager) {
		[self.motionManager stopDeviceMotionUpdates];
		self.motionManager = nil;
	}

	if (self.displayLink) {
		[self.displayLink invalidate];
		self.displayLink = nil;
	}

	if (self.animationTimer) {
		[self.animationTimer invalidate];
		self.animationTimer = nil;
	}
}

- (void)orblit_commonInit {
	self.preferredFrameRate = 60;
	self.useCADisplayLink = bool(GLOBAL_DEF("display.AppleEmbedded/use_cadisplaylink", true)) ? YES : NO;
	self.hingeClosed = YES;
	last_edr_headroom = 0.0;

#if !defined(VISIONOS_ENABLED)
	self.contentScaleFactor = [UIScreen mainScreen].scale;
#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 270100
	if (@available(iOS 27.1, *)) {
		[self addInteraction:[[UIHingeInteraction alloc] initWithUpdateHandler:^(UIHingeInteraction *interaction, UIHingeInteractionUpdate *update) {
			if (update.hinge) {
				self.hingeClosed = update.hinge.status == UIHingeStatusClosed;
			}
		}]];
	}
#endif
#endif

	if (@available(iOS 17.0, *)) {
		[self registerForTraitChanges:@[ [UITraitUserInterfaceStyle class] ] withTarget:self action:@selector(traitCollectionDidChangeWithView:previousTraitCollection:)];
	}

	[self clearTouches];

	self.multipleTouchEnabled = YES;

	// Configure and start accelerometer
	if (!self.motionManager) {
		self.motionManager = [[CMMotionManager alloc] init];
		if (self.motionManager.deviceMotionAvailable) {
			self.motionManager.deviceMotionUpdateInterval = 1.0 / 70.0;
			[self.motionManager startDeviceMotionUpdatesUsingReferenceFrame:CMAttitudeReferenceFrameXMagneticNorthZVertical];
		} else {
			self.motionManager = nil;
		}
	}
}

- (void)system_theme_changed {
	DisplaySystemAppleEmbedded *ds = (DisplaySystemAppleEmbedded *)DisplaySystem::get_singleton();
	if (ds) {
		ds->emit_system_theme_changed();
	}
}

- (void)traitCollectionDidChange:(UITraitCollection *)previousTraitCollection {
	if (@available(iOS 13.0, *)) {
#if !defined(VISIONOS_ENABLED)
		[super traitCollectionDidChange:previousTraitCollection];
#endif
		[self traitCollectionDidChangeWithView:self
					   previousTraitCollection:previousTraitCollection];
	}
}

- (void)traitCollectionDidChangeWithView:(UIView *)view previousTraitCollection:(UITraitCollection *)previousTraitCollection {
	if (@available(iOS 13.0, *)) {
		if ([UITraitCollection currentTraitCollection].userInterfaceStyle != previousTraitCollection.userInterfaceStyle) {
			[self system_theme_changed];
		}
	}
}

- (void)stopRendering {
	if (!self.isActive) {
		return;
	}

	self.isActive = NO;

	print_verbose("Stop rendering");

	if (self.useCADisplayLink) {
		[self.displayLink invalidate];
		self.displayLink = nil;
	} else {
		[self.animationTimer invalidate];
		self.animationTimer = nil;
	}

	[self clearTouches];
}

- (void)startRendering {
	if (self.isActive) {
		return;
	}

	self.isActive = YES;

	print_verbose("Start rendering");

	if (self.useCADisplayLink) {
		self.displayLink = [CADisplayLink displayLinkWithTarget:self selector:@selector(drawView)];
		self.displayLink.preferredFramesPerSecond = self.preferredFrameRate;

		// Setup DisplayLink in main thread
		[self.displayLink addToRunLoop:[NSRunLoop currentRunLoop] forMode:NSRunLoopCommonModes];
	} else {
		self.animationTimer = [NSTimer scheduledTimerWithTimeInterval:(1.0 / self.preferredFrameRate) target:self selector:@selector(drawView) userInfo:nil repeats:YES];
	}
}

- (void)drawView {
	if (!self.isActive) {
		print_verbose("Draw view not active!");
		return;
	}

	if (self.useCADisplayLink) {
		// Pause the CADisplayLink to avoid recursion
		[self.displayLink setPaused:YES];

		// Process all input events
		while (CFRunLoopRunInMode(kCFRunLoopDefaultMode, 0.0, TRUE) == kCFRunLoopRunHandledSource) {
			// Continue.
		}

		// We are good to go, resume the CADisplayLink
		[self.displayLink setPaused:NO];
	}

	[self.renderingLayer startRenderDisplayLayer];

	if (!self.renderer) {
		return;
	}

	if ([self.renderer setUp]) {
		return;
	}

	if (self.delegate && !self.delegateDidFinishSetUp) {
		[self layoutRenderingLayer]; // Trigger DisplaySystemVisionOS::resize_window after Main::start()
		self.delegateDidFinishSetUp = [self.delegate orblitViewFinishedSetup:self];
		if (!_delegateDidFinishSetUp) {
			return;
		}
	}

	[self handleMotion];

#if !defined(VISIONOS_ENABLED)
	if (@available(iOS 16.0, *)) {
		CGFloat edr_headroom = UIScreen.mainScreen.currentEDRHeadroom;
		if (last_edr_headroom != edr_headroom) {
			last_edr_headroom = edr_headroom;
			if (DisplaySystemAppleEmbedded::get_singleton()) {
				DisplaySystemAppleEmbedded::get_singleton()->current_edr_headroom_changed();
			}
		}
	}

#endif

	[self.renderer renderOnView:self];

	[self.renderingLayer stopRenderDisplayLayer];
}

- (BOOL)canRender {
	if (self.useCADisplayLink) {
		return self.displayLink != nil;
	} else {
		return self.animationTimer != nil;
	}
}

- (void)setPreferredFrameRate:(float)preferredFrameRate {
	_preferredFrameRate = preferredFrameRate;

	if (self.canRender) {
		[self stopRendering];
		[self startRendering];
	}
}

- (void)layoutSubviews {
	[super layoutSubviews];
	[self layoutRenderingLayer];
}

- (void)layoutRenderingLayer {
	if (self.renderingLayer) {
		self.renderingLayer.frame = self.bounds;
		[self.renderingLayer layoutDisplayLayer];

		if (DisplaySystemAppleEmbedded::get_singleton()) {
			DisplaySystemAppleEmbedded::get_singleton()->resize_window(self.bounds.size);
		}
	}
}

// MARK: - Input

// MARK: Touches

- (int)getTouchIDForTouch:(UITouch *)p_touch {
	int first = -1;
	for (int i = 0; i < max_touches; i++) {
		if (first == -1 && orblit_touches[i] == nullptr) {
			first = i;
			continue;
		}
		if (orblit_touches[i] == p_touch) {
			return i;
		}
	}

	if (first != -1) {
		orblit_touches[first] = p_touch;
		return first;
	}

	return -1;
}

- (int)removeTouch:(UITouch *)p_touch {
	int remaining = 0;
	for (int i = 0; i < max_touches; i++) {
		if (orblit_touches[i] == nullptr) {
			continue;
		}
		if (orblit_touches[i] == p_touch) {
			orblit_touches[i] = nullptr;
			begin_timestamp[i] = 0.0;
			begin_delta[i] = CGPointMake(0, 0);
			touch_count[i] = 0;
		} else {
			++remaining;
		}
	}
	return remaining;
}

- (void)clearTouches {
	for (int i = 0; i < max_touches; i++) {
		orblit_touches[i] = nullptr;
		begin_timestamp[i] = 0.0;
		begin_delta[i] = CGPointMake(0, 0);
		touch_count[i] = 0;
	}
}

- (void)touchesBegan:(NSSet *)touches withEvent:(UIEvent *)event {
	for (UITouch *touch in touches) {
		int tid = [self getTouchIDForTouch:touch];
		ERR_FAIL_COND(tid == -1);
		CGPoint touchPoint = [touch locationInView:self];
		begin_timestamp[tid] = [touch timestamp];
		begin_delta[tid] = CGPointMake(0, 0);
		touch_count[tid] = touch.tapCount;
		DisplaySystemAppleEmbedded::get_singleton()->touch_press(tid, touchPoint.x * self.contentScaleFactor, touchPoint.y * self.contentScaleFactor, true, touch.tapCount > 1, false);
	}
}

- (void)touchesMoved:(NSSet *)touches withEvent:(UIEvent *)event {
	for (UITouch *touch in touches) {
		int tid = [self getTouchIDForTouch:touch];
		ERR_FAIL_COND(tid == -1);
		CGPoint touchPoint = [touch locationInView:self];
		CGPoint prev_point = [touch previousLocationInView:self];
		if (begin_timestamp[tid] != 0.0) {
			begin_delta[tid].x = fmax(begin_delta[tid].x, Math::abs(touchPoint.x - prev_point.x));
			begin_delta[tid].y = fmax(begin_delta[tid].y, Math::abs(touchPoint.y - prev_point.y));
			if ([touch timestamp] - begin_timestamp[tid] >= 0.5) {
				begin_timestamp[tid] = 0.0;
				if (begin_delta[tid].x <= 10.0 && begin_delta[tid].y <= 10.0) {
					DisplaySystemAppleEmbedded::get_singleton()->touch_press(tid, touchPoint.x * self.contentScaleFactor, touchPoint.y * self.contentScaleFactor, true, touch.tapCount > 1, true);
					return;
				}
			}
		}
		CGFloat alt = [touch altitudeAngle];
		CGVector azim = [touch azimuthUnitVectorInView:self];
		DisplaySystemAppleEmbedded::get_singleton()->touch_drag(tid, prev_point.x * self.contentScaleFactor, prev_point.y * self.contentScaleFactor, touchPoint.x * self.contentScaleFactor, touchPoint.y * self.contentScaleFactor, [touch force] / [touch maximumPossibleForce], Vector2(azim.dx, azim.dy) * Math::cos(alt));
	}
}

- (void)touchesEnded:(NSSet *)touches withEvent:(UIEvent *)event {
	for (UITouch *touch in touches) {
		int tid = [self getTouchIDForTouch:touch];
		ERR_FAIL_COND(tid == -1);
		CGPoint touchPoint = [touch locationInView:self];
		DisplaySystemAppleEmbedded::get_singleton()->touch_press(tid, touchPoint.x * self.contentScaleFactor, touchPoint.y * self.contentScaleFactor, false, touch_count[tid] > 1, false);
		[self removeTouch:touch];
	}
}

- (void)touchesCancelled:(NSSet *)touches withEvent:(UIEvent *)event {
	for (UITouch *touch in touches) {
		int tid = [self getTouchIDForTouch:touch];
		ERR_FAIL_COND(tid == -1);
		DisplaySystemAppleEmbedded::get_singleton()->touches_canceled(tid);
	}
	[self clearTouches];
}

// MARK: Motion

- (void)handleMotion {
	if (!self.motionManager) {
		return;
	}

	// Just using polling approach for now, we can set this up so it sends
	// data to us in intervals, might be better. See Apple reference pages
	// for more details:
	// https://developer.apple.com/reference/coremotion/cmmotionmanager?language=objc

	// Apple splits our accelerometer date into a gravity and user movement
	// component. We add them back together.
	CMAcceleration gravity = self.motionManager.deviceMotion.gravity;
	CMAcceleration acceleration = self.motionManager.deviceMotion.userAcceleration;

	// To be consistent with Android we convert the unit of measurement from g (Earth's gravity)
	// to m/s^2.
	gravity.x *= earth_gravity;
	gravity.y *= earth_gravity;
	gravity.z *= earth_gravity;
	acceleration.x *= earth_gravity;
	acceleration.y *= earth_gravity;
	acceleration.z *= earth_gravity;

	///@TODO We don't seem to be getting data here, is my device broken or
	/// is this code incorrect?
	CMMagneticField magnetic = self.motionManager.deviceMotion.magneticField.field;

	///@TODO we can access rotationRate as a CMRotationRate variable
	///(processed date) or CMGyroData (raw data), have to see what works
	/// best
	CMRotationRate rotation = self.motionManager.deviceMotion.rotationRate;

	// Device orientation (attitude) as quaternion.
	CMQuaternion att = self.motionManager.deviceMotion.attitude.quaternion;
	Quaternion device_orientation(att.x, att.y, att.z, att.w);

	// Adjust for screen orientation.
	// [[UIDevice currentDevice] orientation] changes even if we've fixed
	// our orientation which is not a good thing when you're trying to get
	// your user to move the screen in all directions and want consistent
	// output
#if defined(VISIONOS_ENABLED)
	UIInterfaceOrientation interfaceOrientation = [UIApplication sharedApplication].delegate.window.windowScene.effectiveGeometry.interfaceOrientation;
#else
	UIInterfaceOrientation interfaceOrientation = [UIApplication sharedApplication].delegate.window.windowScene.interfaceOrientation;
#endif

	switch (interfaceOrientation) {
		case UIInterfaceOrientationLandscapeLeft: {
			DisplaySystemAppleEmbedded::get_singleton()->update_gravity(Vector3(gravity.x, gravity.y, gravity.z).rotated(Vector3(0, 0, 1), -Math::PI * 0.5));
			DisplaySystemAppleEmbedded::get_singleton()->update_accelerometer(Vector3(acceleration.x + gravity.x, acceleration.y + gravity.y, acceleration.z + gravity.z).rotated(Vector3(0, 0, 1), -Math::PI * 0.5));
			DisplaySystemAppleEmbedded::get_singleton()->update_magnetometer(Vector3(magnetic.x, magnetic.y, magnetic.z).rotated(Vector3(0, 0, 1), -Math::PI * 0.5));
			DisplaySystemAppleEmbedded::get_singleton()->update_gyroscope(Vector3(rotation.x, rotation.y, rotation.z).rotated(Vector3(0, 0, 1), -Math::PI * 0.5));
			DisplaySystemAppleEmbedded::get_singleton()->update_device_orientation(Quaternion(Vector3(0, 0, 1), -Math::PI * 0.5) * device_orientation);
		} break;
		case UIInterfaceOrientationLandscapeRight: {
			DisplaySystemAppleEmbedded::get_singleton()->update_gravity(Vector3(gravity.x, gravity.y, gravity.z).rotated(Vector3(0, 0, 1), Math::PI * 0.5));
			DisplaySystemAppleEmbedded::get_singleton()->update_accelerometer(Vector3(acceleration.x + gravity.x, acceleration.y + gravity.y, acceleration.z + gravity.z).rotated(Vector3(0, 0, 1), Math::PI * 0.5));
			DisplaySystemAppleEmbedded::get_singleton()->update_magnetometer(Vector3(magnetic.x, magnetic.y, magnetic.z).rotated(Vector3(0, 0, 1), Math::PI * 0.5));
			DisplaySystemAppleEmbedded::get_singleton()->update_gyroscope(Vector3(rotation.x, rotation.y, rotation.z).rotated(Vector3(0, 0, 1), Math::PI * 0.5));
			DisplaySystemAppleEmbedded::get_singleton()->update_device_orientation(Quaternion(Vector3(0, 0, 1), Math::PI * 0.5) * device_orientation);
		} break;
		case UIInterfaceOrientationPortraitUpsideDown: {
			DisplaySystemAppleEmbedded::get_singleton()->update_gravity(Vector3(gravity.x, gravity.y, gravity.z).rotated(Vector3(0, 0, 1), Math::PI));
			DisplaySystemAppleEmbedded::get_singleton()->update_accelerometer(Vector3(acceleration.x + gravity.x, acceleration.y + gravity.y, acceleration.z + gravity.z).rotated(Vector3(0, 0, 1), Math::PI));
			DisplaySystemAppleEmbedded::get_singleton()->update_magnetometer(Vector3(magnetic.x, magnetic.y, magnetic.z).rotated(Vector3(0, 0, 1), Math::PI));
			DisplaySystemAppleEmbedded::get_singleton()->update_gyroscope(Vector3(rotation.x, rotation.y, rotation.z).rotated(Vector3(0, 0, 1), Math::PI));
			DisplaySystemAppleEmbedded::get_singleton()->update_device_orientation(Quaternion(Vector3(0, 0, 1), Math::PI) * device_orientation);
		} break;
		default: { // assume portrait
			DisplaySystemAppleEmbedded::get_singleton()->update_gravity(Vector3(gravity.x, gravity.y, gravity.z));
			DisplaySystemAppleEmbedded::get_singleton()->update_accelerometer(Vector3(acceleration.x + gravity.x, acceleration.y + gravity.y, acceleration.z + gravity.z));
			DisplaySystemAppleEmbedded::get_singleton()->update_magnetometer(Vector3(magnetic.x, magnetic.y, magnetic.z));
			DisplaySystemAppleEmbedded::get_singleton()->update_gyroscope(Vector3(rotation.x, rotation.y, rotation.z));
			DisplaySystemAppleEmbedded::get_singleton()->update_device_orientation(device_orientation);
		} break;
	}
}

@end
