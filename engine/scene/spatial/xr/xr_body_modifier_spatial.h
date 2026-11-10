/**************************************************************************/
/*  xr_body_modifier_spatial.h                                                 */

#pragma once

#include "scene/spatial/skeleton_modifier_spatial.h"
#include "systems/xr/xr_body_tracker.h"

class Skeleton3D;

/**
	The XRBodyModifier3D node drives a body skeleton using body tracking
	data from an XRBodyTracker instance.
 */

class XRBodyModifier3D : public SkeletonModifier3D {
	ORBLIT_CLASS(XRBodyModifier3D, SkeletonModifier3D);

public:
	enum BodyUpdate {
		BODY_UPDATE_UPPER_BODY = 1,
		BODY_UPDATE_LOWER_BODY = 2,
		BODY_UPDATE_HANDS = 4,
	};

	enum BoneUpdate {
		BONE_UPDATE_FULL,
		BONE_UPDATE_ROTATION_ONLY,
		BONE_UPDATE_MAX
	};

	void set_body_tracker(const StringName &p_tracker_name);
	StringName get_body_tracker() const;

	void set_body_update(BitField<BodyUpdate> p_body_update);
	BitField<BodyUpdate> get_body_update() const;

	void set_bone_update(BoneUpdate p_bone_update);
	BoneUpdate get_bone_update() const;

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

	StringName tracker_name = "/user/body_tracker";
	BitField<BodyUpdate> body_update = BODY_UPDATE_UPPER_BODY | BODY_UPDATE_LOWER_BODY | BODY_UPDATE_HANDS;
	BoneUpdate bone_update = BONE_UPDATE_FULL;
	JointData joints[XRBodyTracker::JOINT_MAX];

	void _get_joint_data();
	void _tracker_changed(const StringName &p_tracker_name, XRSystem::TrackerType p_tracker_type);
};

VARIANT_BITFIELD_CAST(XRBodyModifier3D::BodyUpdate)
VARIANT_ENUM_CAST(XRBodyModifier3D::BoneUpdate)
