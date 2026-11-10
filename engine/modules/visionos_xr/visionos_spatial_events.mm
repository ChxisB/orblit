/**************************************************************************/
/*  visionos_spatial_events.mm                                            */

#ifdef VISIONOS_ENABLED

#include "visionos_spatial_events.h"

#include "core/math/transform_spatial.h"
#include "core/math/vector3.h"
#include "systems/xr/xr_controller_tracker.h"
#include "systems/xr/xr_positional_tracker.h"
#include "systems/xr/xr_system.h"

#include "modules/visionos_xr/visionos_definitions.h"

void VisionOSSpatialEventTracking::initialize(XRSystem *p_xr_system,
		VisionOSSharedController &p_left_hand,
		VisionOSSharedController &p_right_hand) {
	// Left hand
	left_hand.controller = &p_left_hand;
	left_hand.transform_correction = Transform3D(Vector3(-1, 0, 0), Vector3(0, 0, -1), Vector3(0, -1, 0), Vector3(0, 0, 0));

	// Right hand
	right_hand.controller = &p_right_hand;
	right_hand.transform_correction = Transform3D(Vector3(1, 0, 0), Vector3(0, 0, -1), Vector3(0, +1, 0), Vector3(0, 0, 0));

	// Eyes
	eyes_ray.instantiate();
	eyes_ray->set_tracker_name("/user/eyes_ext");
	eyes_ray->set_tracker_desc("visionOS eyes selection ray");
	p_xr_system->add_tracker(eyes_ray);
}

void VisionOSSpatialEventTracking::uninitialize(XRSystem *p_xr_system) {
	if (p_xr_system && eyes_ray.is_valid()) {
		p_xr_system->remove_tracker(eyes_ray);
		eyes_ray->unreference();
	}
}

void VisionOSSpatialEventTracking::on_spatial_event(const VisionOSSpatialEvent &p_event) {
	const bool active = (p_event.phase == VisionOSSpatialEvent::Phase::active);

	// Hand
	Hand *hand = nullptr;
	switch (p_event.chirality) {
		case VisionOSSpatialEvent::Chirality::left:
			hand = &left_hand;
			break;
		case VisionOSSpatialEvent::Chirality::right:
			hand = &right_hand;
			break;
		default:
			break;
	}

	// Updating the pose first and sending the input second.
	if (hand) {
		// Updating the ray.
		if (active) {
			// Setting the ray pose on pinch.
			if (p_event.has_ray && hand->ray_submitted == false) {
				eyes_ray->set_pose("default", p_event.ray, Vector3(), Vector3());
				hand->ray_submitted = true;
			}
		} else {
			// Resetting the state on release (for the next pinch).
			hand->ray_submitted = false;
		}

		if (active) {
			hand->controller->source = VisionOSSharedController::Source::SpatialEvent;
		} else {
			hand->controller->source = VisionOSSharedController::Source::None;
		}

		// Updating the hand pose.
		Transform3D pose = p_event.hand_pose * hand->transform_correction;
		for (const String name : { "default", "aim" }) {
			hand->controller->tracker->set_pose(name, pose, Vector3(), Vector3());
		}
		for (const String name : { "grip", "palm" }) {
			hand->controller->tracker->invalidate_pose(name);
		}

		// Submitting input events. It will send a signal that the game
		// can receive, to handle input.
		hand->controller->tracker->set_input("trigger_click", active);
	}
}

#endif // VISIONOS_ENABLED
