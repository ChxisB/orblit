/**************************************************************************/
/*  visionos_spatial_events.h                                             */

#pragma once

#ifdef VISIONOS_ENABLED

#include "visionos_definitions.h"

#include "core/math/transform_spatial.h"
#include "systems/xr/xr_controller_tracker.h"

// Equivalent to https://developer.apple.com/documentation/swiftui/spatialeventcollection/event
struct VisionOSSpatialEvent {
	// Ray
	bool has_ray;
	Transform3D ray;

	// Hand
	enum class Chirality : int {
		none = 0,
		left = 1,
		right = 2
	};
	Chirality chirality;
	Transform3D hand_pose;

	// Phase
	enum class Phase : int {
		unknown = 0,
		active = 1,
		cancelled = 2,
		ended = 3
	};
	Phase phase;
};

// Orblit representation of visionOS spatial events.
struct VisionOSSpatialEventTracking {
	// Ray from center of the head to
	// the direction of the eyes, when a
	// pinch gesture begins.
	Ref<XRControllerTracker> eyes_ray;

	struct Hand {
		// Hand pose when pinching and dragging.
		VisionOSSharedController *controller = nullptr;

		// Update the ray only once per gesture.
		bool ray_submitted = false;

		// Correcting the transforms from each hand to
		// map to the Orblit and OpenXR convention:
		// https://registry.khronos.org/OpenXR/specs/1.1/html/xrspec.html#XR_EXT_hand_interaction
		Transform3D transform_correction;
	};

	// Left and right hands.
	Hand left_hand, right_hand;

	void initialize(XRSystem *p_xr_system, VisionOSSharedController &p_left_hand,
			VisionOSSharedController &p_right_hand);
	void uninitialize(XRSystem *p_xr_system);

	void on_spatial_event(const VisionOSSpatialEvent &);
};

#endif // VISIONOS_ENABLED
