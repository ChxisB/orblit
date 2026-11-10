/**************************************************************************/
/*  visionos_controller_tracking.h                                        */

#pragma once

#ifdef VISIONOS_ENABLED

#include "visionos_definitions.h"

#include "systems/xr/xr_controller_tracker.h"

#ifdef __OBJC__
#define Key OrblitKey
#import <CoreHaptics/CoreHaptics.h>
#import <GameController/GameController.h>
#undef Key
#else // __OBJC__
typedef struct GCController *GCController_t;
typedef struct CHHapticEngine *CHHapticEngine_t;
#endif // __OBJC__

class VisionOSXRInterface;

// Controller tracking using ARKit and GCController
struct VisionOSControllerTracking {
	bool enabled = false;
	VisionOSAuthorizationStatus authorization = VisionOSAuthorizationStatus::NOT_DETERMINED;

	bool active() const { return enabled && authorization == VisionOSAuthorizationStatus::ALLOWED; }

	// ARKit state
	ar_accessory_tracking_provider_t accessory_tracking_provider = nullptr;
	ar_accessories_t accessories = nullptr;
	ar_accessory_anchor_t left_controller_anchor = nullptr;
	ar_accessory_anchor_t right_controller_anchor = nullptr;

	// Controller state
	VisionOSSharedController *left_shared_controller = nullptr;
	VisionOSSharedController *right_shared_controller = nullptr;
	GCController *left_gc_controller = nullptr;
	GCController *right_gc_controller = nullptr;
	CHHapticEngine *left_haptic_engine = nullptr;
	CHHapticEngine *right_haptic_engine = nullptr;

	// Notification observers
	id controller_observer = nullptr;
	id controller_disconnect_observer = nullptr;

	void initialize(XRSystem *p_xr_system, VisionOSXRInterface *p_xr_interface,
			VisionOSSharedController &p_left_hand,
			VisionOSSharedController &p_right_hand);
	void uninitialize(XRSystem *p_xr_system);

	void init_for_controller(GCController *p_controller);
	void setup_controller_notifications();
	void cleanup_controller_notifications();
	void handle_controller_disconnect(GCController *p_controller);
	void update_accessories_list();
	void update_controller_trackers_from_arkit(CFTimeInterval p_trackable_anchor_time);
	void update_controller_from_anchor(Ref<XRControllerTracker> p_controller_tracker, ar_accessory_anchor_t p_controller_anchor, GCController *p_gc_controller);

	// Called directly from the same function on the XRInterface
	void trigger_haptic_pulse(const String &p_action_name, const StringName &p_tracker_name, double p_frequency, double p_amplitude, double p_duration_sec, double p_delay_sec);

	VisionOSXRInterface *xr_interface = nullptr; // assigned on initialization
};

#endif // VISIONOS_ENABLED
