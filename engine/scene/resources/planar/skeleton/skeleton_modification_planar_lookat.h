/**************************************************************************/
/*  skeleton_modification_planar_lookat.h                                     */

#pragma once

#include "scene/planar/skeleton_planar.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar.h"

///////////////////////////////////////
// SkeletonModification2DLookAt
///////////////////////////////////////

class SkeletonModification2DLookAt : public SkeletonModification2D {
	ORBLIT_CLASS(SkeletonModification2DLookAt, SkeletonModification2D);

private:
	int bone_idx = -1;
	EntityPath bone2d_node;
	ObjectID bone2d_node_cache;

	EntityPath target_node;
	ObjectID target_node_cache;
	Entity2D *target_node_reference = nullptr;

	float additional_rotation = 0;
	bool enable_constraint = false;
	float constraint_angle_min = 0;
	float constraint_angle_max = (2.0 * Math::PI);
	bool constraint_angle_invert = false;
	bool constraint_in_localspace = true;

	void update_bone2d_cache();
	void update_target_cache();

protected:
	static void register_members();
	bool _set(const StringName &p_path, const Variant &p_value);
	bool _get(const StringName &p_path, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	void _execute(float p_delta) override;
	void _setup_modification(SkeletonModificationStack2D *p_stack) override;
	void _draw_editor_gizmo() override;

	void set_bone2d_node(const EntityPath &p_target_node);
	EntityPath get_bone2d_node() const;
	void set_bone_index(int p_idx);
	int get_bone_index() const;

	void set_target_node(const EntityPath &p_target_node);
	EntityPath get_target_node() const;

	void set_additional_rotation(float p_rotation);
	float get_additional_rotation() const;

	void set_enable_constraint(bool p_constraint);
	bool get_enable_constraint() const;
	void set_constraint_angle_min(float p_angle_min);
	float get_constraint_angle_min() const;
	void set_constraint_angle_max(float p_angle_max);
	float get_constraint_angle_max() const;
	void set_constraint_angle_invert(bool p_invert);
	bool get_constraint_angle_invert() const;
	void set_constraint_in_localspace(bool p_constraint_in_localspace);
	bool get_constraint_in_localspace() const;

	SkeletonModification2DLookAt();
	~SkeletonModification2DLookAt();
};
