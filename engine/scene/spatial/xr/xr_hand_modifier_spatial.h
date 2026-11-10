/**************************************************************************/
/*  xr_hand_modifier_spatial.h                                                 */

#pragma once

#include "scene/spatial/skeleton_modifier_spatial.h"
#include "systems/xr/xr_hand_tracker.h"

/**
	The XRHandModifier3D node drives a hand skeleton using hand tracking
	data from an XRHandTracking instance.
 */

class XRHandModifier3D : public SkeletonModifier3D {
	ORBLIT_CLASS(XRHandModifier3D, SkeletonModifier3D);

public:
	enum BoneUpdate {
		BONE_UPDATE_FULL,
		BONE_UPDATE_ROTATION_ONLY,
		BONE_UPDATE_MAX
	};

	void set_hand_tracker(const StringName &p_tracker_name);
	StringName get_hand_tracker() const;

	void set_bone_update(BoneUpdate p_bone_update);
	BoneUpdate get_bone_update() const;

	PackedStringArray get_configuration_warnings() const override;

	void _notification(int p_what);

protected:
	static void register_members();

	virtual void _skeleton_changed(Skeleton3D *p_old, Skeleton3D *p_new) override;
	virtual void _process_modification(double p_delta) override;

private:
	struct JointData {
		int bone = -1;
		int parent_joint = -1;
	};

	StringName tracker_name = "/user/hand_tracker/left";
	BoneUpdate bone_update = BONE_UPDATE_FULL;
	JointData joints[XRHandTracker::HAND_JOINT_MAX];

	bool has_stored_previous_transforms = false;
	Vector<Transform3D> previous_relative_transforms;

	void _get_joint_data();
	void _tracker_changed(StringName p_tracker_name, XRSystem::TrackerType p_tracker_type);
};

VARIANT_ENUM_CAST(XRHandModifier3D::BoneUpdate)
