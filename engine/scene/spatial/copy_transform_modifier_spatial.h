/**************************************************************************/
/*  copy_transform_modifier_spatial.h                                          */

#pragma once

#include "scene/spatial/bone_constraint_spatial.h"

class CopyTransformModifier3D : public BoneConstraint3D {
	ORBLIT_CLASS(CopyTransformModifier3D, BoneConstraint3D);

public:
	enum TransformFlag {
		TRANSFORM_FLAG_POSITION = 1,
		TRANSFORM_FLAG_ROTATION = 2,
		TRANSFORM_FLAG_SCALE = 4,
		TRANSFORM_FLAG_ALL = TRANSFORM_FLAG_POSITION | TRANSFORM_FLAG_ROTATION | TRANSFORM_FLAG_SCALE,
	};

	enum AxisFlag {
		AXIS_FLAG_X = 1,
		AXIS_FLAG_Y = 2,
		AXIS_FLAG_Z = 4,
		AXIS_FLAG_ALL = AXIS_FLAG_X | AXIS_FLAG_Y | AXIS_FLAG_Z,
	};

	struct CopyTransform3DSetting : public BoneConstraint3DSetting {
		BitField<TransformFlag> copy_flags = TRANSFORM_FLAG_ALL;
		BitField<AxisFlag> axis_flags = AXIS_FLAG_ALL;
		BitField<AxisFlag> invert_flags = 0;

		bool global = false;
		bool relative = true;
		bool additive = false;

		bool is_global() {
			if (reference_type == REFERENCE_TYPE_NODE) {
				return false;
			}
			return global;
		}

		bool is_relative() {
			if (reference_type == REFERENCE_TYPE_NODE) {
				return false;
			}
			return relative;
		}
	};

protected:
	bool _get(const StringName &p_path, Variant &r_ret) const;
	bool _set(const StringName &p_path, const Variant &p_value);
	void _get_property_list(List<PropertyInfo> *p_list) const;
	void _validate_dynamic_prop(PropertyInfo &p_property) const;

	static void register_members();

	virtual void _process_constraint_by_bone(int p_index, Skeleton3D *p_skeleton, int p_apply_bone, int p_reference_bone, float p_amount) override;
	virtual void _process_constraint_by_node(int p_index, Skeleton3D *p_skeleton, int p_apply_bone, const EntityPath &p_reference_node, float p_amount) override;
	virtual void _process_copy(int p_index, Skeleton3D *p_skeleton, int p_apply_bone, const Transform3D &p_destination, float p_amount);
	virtual void _validate_setting(int p_index) override;

public:
	void set_copy_flags(int p_index, BitField<TransformFlag> p_copy_flags);
	BitField<TransformFlag> get_copy_flags(int p_index) const;

	void set_copy_position(int p_index, bool p_enabled);
	bool is_position_copying(int p_index) const;
	void set_copy_rotation(int p_index, bool p_enabled);
	bool is_rotation_copying(int p_index) const;
	void set_copy_scale(int p_index, bool p_enabled);
	bool is_scale_copying(int p_index) const;

	void set_axis_flags(int p_index, BitField<AxisFlag> p_axis_flags);
	BitField<AxisFlag> get_axis_flags(int p_index) const;

	void set_axis_x_enabled(int p_index, bool p_enabled);
	bool is_axis_x_enabled(int p_index) const;
	void set_axis_y_enabled(int p_index, bool p_enabled);
	bool is_axis_y_enabled(int p_index) const;
	void set_axis_z_enabled(int p_index, bool p_enabled);
	bool is_axis_z_enabled(int p_index) const;

	void set_invert_flags(int p_index, BitField<AxisFlag> p_axis_flags);
	BitField<AxisFlag> get_invert_flags(int p_index) const;

	void set_axis_x_inverted(int p_index, bool p_enabled);
	bool is_axis_x_inverted(int p_index) const;
	void set_axis_y_inverted(int p_index, bool p_enabled);
	bool is_axis_y_inverted(int p_index) const;
	void set_axis_z_inverted(int p_index, bool p_enabled);
	bool is_axis_z_inverted(int p_index) const;

	void set_global(int p_index, bool p_enabled);
	bool is_global(int p_index) const;

	void set_relative(int p_index, bool p_enabled);
	bool is_relative(int p_index) const;

	void set_additive(int p_index, bool p_enabled);
	bool is_additive(int p_index) const;

	~CopyTransformModifier3D();
};

VARIANT_BITFIELD_CAST(CopyTransformModifier3D::TransformFlag);
VARIANT_BITFIELD_CAST(CopyTransformModifier3D::AxisFlag);
