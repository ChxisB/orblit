/**************************************************************************/
/*  bone_space_adjuster_spatial.h                                              */

#pragma once

#include "scene/spatial/skeleton_modifier_spatial.h"

class BoneSpaceAdjuster3D : public SkeletonModifier3D {
	ORBLIT_CLASS(BoneSpaceAdjuster3D, SkeletonModifier3D);

	const Vector3 INITIAL_SCALE = Vector3(1, 1, 1);
	bool use_bone_skin_scale_for_all_bones = true;

public:
	struct BoneSpaceAdjuster3DSetting {
		StringName bone_name;
		int bone = -1;
		bool use_bone_skin_scale = true;
		Vector3 scale = Vector3(1, 1, 1);
	};

protected:
	LocalVector<BoneSpaceAdjuster3DSetting> settings;

	bool _get(const StringName &p_path, Variant &r_ret) const;
	bool _set(const StringName &p_path, const Variant &p_value);
	void _get_property_list(List<PropertyInfo> *p_list) const;
	void _validate_property(PropertyInfo &p_property) const;
	void _validate_dynamic_prop(PropertyInfo &p_property) const;

	static void register_members();

	virtual void _process_modification(double p_delta) override;

public:
	int get_setting_size();
	void set_setting_size(int p_size);
	void clear_settings();

	void set_bone_name(int p_index, const StringName &p_bone_name);
	StringName get_bone_name(int p_index) const;
	void set_bone(int p_index, int p_bone);
	int get_bone(int p_index) const;
	void set_use_bone_skin_scale(int p_index, bool p_enabled);
	bool is_using_bone_skin_scale(int p_index) const;
	void set_bone_scale(int p_index, const Vector3 &p_scale);
	Vector3 get_bone_scale(int p_index) const;

	void set_use_bone_skin_scale_for_all_bones(bool p_enabled);
	bool are_all_bones_using_bone_skin_scale() const;
};
