/**************************************************************************/
/*  aim_modifier_spatial.h                                                     */

#pragma once

#include "scene/spatial/bone_constraint_spatial.h"

class AimModifier3D : public BoneConstraint3D {
	ORBLIT_CLASS(AimModifier3D, BoneConstraint3D);

public:
	struct AimModifier3DSetting : public BoneConstraint3DSetting {
		BoneAxis forward_axis = BONE_AXIS_PLUS_Y;
		bool use_euler = false;
		Vector3::Axis primary_rotation_axis = Vector3::AXIS_X;
		bool use_secondary_rotation = true;
		bool relative = true;
	};

protected:
	bool _get(const StringName &p_path, Variant &r_ret) const;
	bool _set(const StringName &p_path, const Variant &p_value);
	virtual PackedStringArray get_configuration_warnings() const override;
	void _get_property_list(List<PropertyInfo> *p_list) const;

	static void register_members();

	virtual void _process_constraint_by_bone(int p_index, Skeleton3D *p_skeleton, int p_apply_bone, int p_reference_bone, float p_amount) override;
	virtual void _process_constraint_by_node(int p_index, Skeleton3D *p_skeleton, int p_apply_bone, const EntityPath &p_reference_node, float p_amount) override;
	virtual void _process_aim(int p_index, Skeleton3D *p_skeleton, int p_apply_bone, Vector3 p_target, float p_amount);
	virtual void _validate_setting(int p_index) override;

public:
	void set_forward_axis(int p_index, BoneAxis p_axis);
	BoneAxis get_forward_axis(int p_index) const;
	void set_use_euler(int p_index, bool p_enabled);
	bool is_using_euler(int p_index) const;
	void set_primary_rotation_axis(int p_index, Vector3::Axis p_axis);
	Vector3::Axis get_primary_rotation_axis(int p_index) const;
	void set_use_secondary_rotation(int p_index, bool p_enabled);
	bool is_using_secondary_rotation(int p_index) const;
	void set_relative(int p_index, bool p_enabled);
	bool is_relative(int p_index) const;

	~AimModifier3D();
};
