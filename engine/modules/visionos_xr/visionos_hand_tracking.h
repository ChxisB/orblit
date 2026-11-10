/**************************************************************************/
/*  visionos_hand_tracking.h                                              */

#pragma once

#ifdef VISIONOS_ENABLED

#include "visionos_definitions.h"

// Hand tracking using ARKit
struct VisionOSHandTracking {
	bool enabled = false;
	VisionOSAuthorizationStatus authorization = VisionOSAuthorizationStatus::NOT_DETERMINED;

	bool active() const { return enabled && authorization == VisionOSAuthorizationStatus::ALLOWED; }

	ar_hand_tracking_provider_t hand_tracking_provider = nullptr;

	// Hand trackers
	Ref<XRHandTracker> left_hand_tracker;
	Ref<XRHandTracker> right_hand_tracker;
	ar_hand_anchor_t left_hand_anchor = nullptr;
	ar_hand_anchor_t right_hand_anchor = nullptr;

	void initialize(XRSystem *xr_system);
	void update_hand_trackers_from_arkit(CFTimeInterval trackable_anchor_time);
	void reset_hand_tracker_data(Ref<XRHandTracker> hand_tracker);
	void set_hand_tracker_data_from_arkit(Ref<XRHandTracker> hand_tracker, ar_hand_anchor_t hand_anchor);
};

#endif // VISIONOS_ENABLED
