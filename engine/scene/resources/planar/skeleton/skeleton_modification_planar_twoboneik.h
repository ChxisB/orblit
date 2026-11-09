/**************************************************************************/
/*  skeleton_modification_planar_twoboneik.h                                  */

#pragma once

#include "scene/planar/skeleton_planar.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar.h"

///////////////////////////////////////
// SkeletonModification2DJIGGLE
///////////////////////////////////////

class SkeletonModification2DTwoBoneIK : public SkeletonModification2D {
	ORBLIT_CLASS(SkeletonModification2DTwoBoneIK, SkeletonModification2D);

private:
	EntityPath target_node;
	ObjectID target_node_cache;
	float target_minimum_distance = 0;
	float target_maximum_distance = 0;
	bool flip_bend_direction = false;

	EntityPath joint_one_bone2d_node;
	ObjectID joint_one_bone2d_node_cache;
	int joint_one_bone_idx = -1;

	EntityPath joint_two_bone2d_node;
	ObjectID joint_two_bone2d_node_cache;
	int joint_two_bone_idx = -1;

#ifdef TOOLS_ENABLED
	bool editor_draw_min_max = false;
#endif // TOOLS_ENABLED

	void update_target_cache();
	void update_joint_one_bone2d_cache();
	void update_joint_two_bone2d_cache();

protected:
	static void register_members();
	bool _get(const StringName &p_path, Variant &r_ret) const;
	bool _set(const StringName &p_path, const Variant &p_value);
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	void _execute(float p_delta) override;
	void _setup_modification(SkeletonModificationStack2D *p_stack) override;
	void _draw_editor_gizmo() override;

	void set_target_node(const EntityPath &p_target_node);
	EntityPath get_target_node() const;

	void set_target_minimum_distance(float p_minimum_distance);
	float get_target_minimum_distance() const;
	void set_target_maximum_distance(float p_maximum_distance);
	float get_target_maximum_distance() const;
	void set_flip_bend_direction(bool p_flip_direction);
	bool get_flip_bend_direction() const;

	void set_joint_one_bone2d_node(const EntityPath &p_node);
	EntityPath get_joint_one_bone2d_node() const;
	void set_joint_one_bone_idx(int p_bone_idx);
	int get_joint_one_bone_idx() const;

	void set_joint_two_bone2d_node(const EntityPath &p_node);
	EntityPath get_joint_two_bone2d_node() const;
	void set_joint_two_bone_idx(int p_bone_idx);
	int get_joint_two_bone_idx() const;

#ifdef TOOLS_ENABLED
	void set_editor_draw_min_max(bool p_draw);
	bool get_editor_draw_min_max() const;
#endif // TOOLS_ENABLED

	SkeletonModification2DTwoBoneIK();
	~SkeletonModification2DTwoBoneIK();
};
