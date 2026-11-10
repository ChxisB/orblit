/**************************************************************************/
/*  skeleton_modification_planar_ccdik.h                                      */

#pragma once

#include "scene/planar/skeleton_planar.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar.h"

///////////////////////////////////////
// SkeletonModification2DCCDIK
///////////////////////////////////////

class SkeletonModification2DCCDIK : public SkeletonModification2D {
	ORBLIT_CLASS(SkeletonModification2DCCDIK, SkeletonModification2D);

private:
	struct CCDIK_Joint_Data2D {
		int bone_idx = -1;
		EntityPath bone2d_node;
		ObjectID bone2d_node_cache;
		bool rotate_from_joint = false;

		bool enable_constraint = false;
		float constraint_angle_min = 0;
		float constraint_angle_max = (2.0 * Math::PI);
		bool constraint_angle_invert = false;
		bool constraint_in_localspace = true;

		bool editor_draw_gizmo = true;
	};

	Vector<CCDIK_Joint_Data2D> ccdik_data_chain;

	EntityPath target_node;
	ObjectID target_node_cache;
	void update_target_cache();

	EntityPath tip_node;
	ObjectID tip_node_cache;
	void update_tip_cache();

	void ccdik_joint_update_bone2d_cache(int p_joint_idx);
	void _execute_ccdik_joint(int p_joint_idx, Entity2D *p_target, Entity2D *p_tip);

protected:
	static void register_members();
	bool _set(const StringName &p_path, const Variant &p_value);
	bool _get(const StringName &p_path, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	void _execute(float p_delta) override;
	void _setup_modification(SkeletonModificationStack2D *p_stack) override;
	void _draw_editor_gizmo() override;

	void set_target_node(const EntityPath &p_target_node);
	EntityPath get_target_node() const;
	void set_tip_node(const EntityPath &p_tip_node);
	EntityPath get_tip_node() const;

	int get_ccdik_data_chain_length();
	void set_ccdik_data_chain_length(int p_new_length);

	void set_ccdik_joint_bone2d_node(int p_joint_idx, const EntityPath &p_target_node);
	EntityPath get_ccdik_joint_bone2d_node(int p_joint_idx) const;
	void set_ccdik_joint_bone_index(int p_joint_idx, int p_bone_idx);
	int get_ccdik_joint_bone_index(int p_joint_idx) const;

	void set_ccdik_joint_rotate_from_joint(int p_joint_idx, bool p_rotate_from_joint);
	bool get_ccdik_joint_rotate_from_joint(int p_joint_idx) const;
	void set_ccdik_joint_enable_constraint(int p_joint_idx, bool p_constraint);
	bool get_ccdik_joint_enable_constraint(int p_joint_idx) const;
	void set_ccdik_joint_constraint_angle_min(int p_joint_idx, float p_angle_min);
	float get_ccdik_joint_constraint_angle_min(int p_joint_idx) const;
	void set_ccdik_joint_constraint_angle_max(int p_joint_idx, float p_angle_max);
	float get_ccdik_joint_constraint_angle_max(int p_joint_idx) const;
	void set_ccdik_joint_constraint_angle_invert(int p_joint_idx, bool p_invert);
	bool get_ccdik_joint_constraint_angle_invert(int p_joint_idx) const;
	void set_ccdik_joint_constraint_in_localspace(int p_joint_idx, bool p_constraint_in_localspace);
	bool get_ccdik_joint_constraint_in_localspace(int p_joint_idx) const;
	void set_ccdik_joint_editor_draw_gizmo(int p_joint_idx, bool p_draw_gizmo);
	bool get_ccdik_joint_editor_draw_gizmo(int p_joint_idx) const;

	SkeletonModification2DCCDIK();
	~SkeletonModification2DCCDIK();
};
