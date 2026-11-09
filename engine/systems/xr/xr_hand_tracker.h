/**************************************************************************/
/*  xr_hand_tracker.h                                                     */

#pragma once

#include "systems/xr/xr_positional_tracker.h"

class XRHandTracker : public XRPositionalTracker {
	ORBLIT_CLASS(XRHandTracker, XRPositionalTracker);
	_THREAD_SAFE_CLASS_

public:
	enum HandTrackingSource {
		HAND_TRACKING_SOURCE_UNKNOWN,
		HAND_TRACKING_SOURCE_UNOBSTRUCTED,
		HAND_TRACKING_SOURCE_CONTROLLER,
		HAND_TRACKING_SOURCE_NOT_TRACKED,
		HAND_TRACKING_SOURCE_MAX
	};

	enum HandJoint {
		HAND_JOINT_PALM,
		HAND_JOINT_WRIST,
		HAND_JOINT_THUMB_METACARPAL,
		HAND_JOINT_THUMB_PHALANX_PROXIMAL,
		HAND_JOINT_THUMB_PHALANX_DISTAL,
		HAND_JOINT_THUMB_TIP,
		HAND_JOINT_INDEX_FINGER_METACARPAL,
		HAND_JOINT_INDEX_FINGER_PHALANX_PROXIMAL,
		HAND_JOINT_INDEX_FINGER_PHALANX_INTERMEDIATE,
		HAND_JOINT_INDEX_FINGER_PHALANX_DISTAL,
		HAND_JOINT_INDEX_FINGER_TIP,
		HAND_JOINT_MIDDLE_FINGER_METACARPAL,
		HAND_JOINT_MIDDLE_FINGER_PHALANX_PROXIMAL,
		HAND_JOINT_MIDDLE_FINGER_PHALANX_INTERMEDIATE,
		HAND_JOINT_MIDDLE_FINGER_PHALANX_DISTAL,
		HAND_JOINT_MIDDLE_FINGER_TIP,
		HAND_JOINT_RING_FINGER_METACARPAL,
		HAND_JOINT_RING_FINGER_PHALANX_PROXIMAL,
		HAND_JOINT_RING_FINGER_PHALANX_INTERMEDIATE,
		HAND_JOINT_RING_FINGER_PHALANX_DISTAL,
		HAND_JOINT_RING_FINGER_TIP,
		HAND_JOINT_PINKY_FINGER_METACARPAL,
		HAND_JOINT_PINKY_FINGER_PHALANX_PROXIMAL,
		HAND_JOINT_PINKY_FINGER_PHALANX_INTERMEDIATE,
		HAND_JOINT_PINKY_FINGER_PHALANX_DISTAL,
		HAND_JOINT_PINKY_FINGER_TIP,
		HAND_JOINT_MAX,
	};

	enum HandJointFlags {
		HAND_JOINT_FLAG_ORIENTATION_VALID = 1,
		HAND_JOINT_FLAG_ORIENTATION_TRACKED = 2,
		HAND_JOINT_FLAG_POSITION_VALID = 4,
		HAND_JOINT_FLAG_POSITION_TRACKED = 8,
		HAND_JOINT_FLAG_LINEAR_VELOCITY_VALID = 16,
		HAND_JOINT_FLAG_ANGULAR_VELOCITY_VALID = 32,
	};

	void set_tracker_type(XRSystem::TrackerType p_type) override;
	void set_tracker_hand(const XRPositionalTracker::TrackerHand p_hand) override;

	void set_has_tracking_data(bool p_has_tracking_data);
	bool get_has_tracking_data() const;

	void set_hand_tracking_source(HandTrackingSource p_source);
	HandTrackingSource get_hand_tracking_source() const;

	void set_hand_joint_flags(HandJoint p_joint, BitField<HandJointFlags> p_flags);
	BitField<HandJointFlags> get_hand_joint_flags(HandJoint p_joint) const;

	void set_hand_joint_transform(HandJoint p_joint, const Transform3D &p_transform);
	Transform3D get_hand_joint_transform(HandJoint p_joint) const;

	void set_hand_joint_radius(HandJoint p_joint, float p_radius);
	float get_hand_joint_radius(HandJoint p_joint) const;

	void set_hand_joint_linear_velocity(HandJoint p_joint, const Vector3 &p_velocity);
	Vector3 get_hand_joint_linear_velocity(HandJoint p_joint) const;

	void set_hand_joint_angular_velocity(HandJoint p_joint, const Vector3 &p_velocity);
	Vector3 get_hand_joint_angular_velocity(HandJoint p_joint) const;

	XRHandTracker();

protected:
	static void register_members();

private:
	bool has_tracking_data = false;
	HandTrackingSource hand_tracking_source = HAND_TRACKING_SOURCE_UNKNOWN;

	BitField<HandJointFlags> hand_joint_flags[HAND_JOINT_MAX];
	Transform3D hand_joint_transforms[HAND_JOINT_MAX];
	float hand_joint_radii[HAND_JOINT_MAX] = {};
	Vector3 hand_joint_linear_velocities[HAND_JOINT_MAX];
	Vector3 hand_joint_angular_velocities[HAND_JOINT_MAX];
};

VARIANT_ENUM_CAST(XRHandTracker::HandTrackingSource)
VARIANT_ENUM_CAST(XRHandTracker::HandJoint)
VARIANT_BITFIELD_CAST(XRHandTracker::HandJointFlags)
