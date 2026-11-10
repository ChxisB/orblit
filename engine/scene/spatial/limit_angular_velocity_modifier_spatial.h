/**************************************************************************/
/*  limit_angular_velocity_modifier_spatial.h                                  */

#pragma once

#include "scene/spatial/skeleton_modifier_spatial.h"

class LimitAngularVelocityModifier3D : public SkeletonModifier3D {
	ORBLIT_CLASS(LimitAngularVelocityModifier3D, SkeletonModifier3D);

public:
	struct BoneJoint {
		StringName name;
		int bone = -1;
	};

	struct Chain {
		BoneJoint root_bone;
		BoneJoint end_bone;
	};

	typedef Pair<int, Quaternion> BoneRot;

private:
	bool exclude = false;
	double max_angular_velocity = Math::TAU;

	LocalVector<Chain> chains;
	LocalVector<BoneJoint> joints;
	LocalVector<BoneRot> bones;

	bool joints_dirty = false;
	bool init_needed = true;

protected:
	bool _get(const StringName &p_path, Variant &r_ret) const;
	bool _set(const StringName &p_path, const Variant &p_value);
	void _get_property_list(List<PropertyInfo> *p_list) const;
	void _validate_property(PropertyInfo &p_property) const;

	static void register_members();

	void _notification(int p_what);

	virtual void _set_active(bool p_active) override;
	virtual void _skeleton_changed(Skeleton3D *p_old, Skeleton3D *p_new) override;

	virtual void _validate_bone_names() override;

	void _make_joints_dirty();
	void _update_joints();
	bool _is_joint_contained(int p_bone);

	// For editor.
	int _get_joint_count() const;

	virtual void _process_modification(double p_delta) override;

public:
	void set_root_bone_name(int p_index, const String &p_bone_name);
	String get_root_bone_name(int p_index) const;
	void set_root_bone(int p_index, int p_bone);
	int get_root_bone(int p_index) const;

	void set_end_bone_name(int p_index, const String &p_bone_name);
	String get_end_bone_name(int p_index) const;
	void set_end_bone(int p_index, int p_bone);
	int get_end_bone(int p_index) const;

	String get_joint_bone_name(int p_index) const;
	int get_joint_bone(int p_index) const;

	void set_chain_count(int p_count);
	int get_chain_count() const;
	void clear_chains();

	void set_max_angular_velocity(double p_angular_velocity);
	double get_max_angular_velocity() const;

	void set_exclude(bool p_exclude);
	bool is_exclude() const;

	void reset();

	~LimitAngularVelocityModifier3D();
};
