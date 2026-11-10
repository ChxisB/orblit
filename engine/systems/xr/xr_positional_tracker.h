/**************************************************************************/
/*  xr_positional_tracker.h                                               */

#pragma once

#include "core/os/thread_safe.h"
#include "systems/xr/xr_pose.h"
#include "systems/xr/xr_system.h"
#include "systems/xr/xr_tracker.h"

/**
	The positional tracker object as an object that represents the position and orientation of a tracked object like a controller or headset.
	An AR/VR Interface will registered the trackers it manages with our AR/VR server and update its position and orientation.
	This is where potentially additional AR/VR interfaces may be active as there are AR/VR SDKs that solely deal with positional tracking.
*/

#define XR_TRACKER_HEAD SNAME("head")

class XRPositionalTracker : public XRTracker {
	ORBLIT_CLASS(XRPositionalTracker, XRTracker);
	_THREAD_SAFE_CLASS_

public:
	enum TrackerHand {
		TRACKER_HAND_UNKNOWN, /* unknown or not applicable */
		TRACKER_HAND_LEFT, /* controller is the left hand controller */
		TRACKER_HAND_RIGHT, /* controller is the right hand controller */
		TRACKER_HAND_MAX
	};

protected:
	String profile; // this is interface dependent, for OpenXR this will be the interaction profile bound for to the tracker
	TrackerHand tracker_hand = TRACKER_HAND_UNKNOWN; // if known, the hand this tracker is held in

	HashMap<StringName, Ref<XRPose>> poses;
	HashMap<StringName, Variant> inputs;

	static void register_members();

public:
	void set_tracker_profile(const String &p_profile);
	String get_tracker_profile() const;

	XRPositionalTracker::TrackerHand get_tracker_hand() const;
	virtual void set_tracker_hand(const XRPositionalTracker::TrackerHand p_hand);

	bool has_pose(const StringName &p_action_name) const;
	Ref<XRPose> get_pose(const StringName &p_action_name) const;
	void invalidate_pose(const StringName &p_action_name);
	void set_pose(const StringName &p_action_name, const Transform3D &p_transform, const Vector3 &p_linear_velocity, const Vector3 &p_angular_velocity, const XRPose::TrackingConfidence p_tracking_confidence = XRPose::XR_TRACKING_CONFIDENCE_HIGH);

	Variant get_input(const StringName &p_action_name) const;
	void set_input(const StringName &p_action_name, const Variant &p_value);
};

VARIANT_ENUM_CAST(XRPositionalTracker::TrackerHand);
