/**************************************************************************/
/*  skeleton_modification_planar_fabrik.h                                     */

#pragma once

#include "scene/planar/skeleton_planar.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar.h"

///////////////////////////////////////
// SkeletonModification2DFABRIK
///////////////////////////////////////

class SkeletonModification2DFABRIK : public SkeletonModification2D {
	ORBLIT_CLASS(SkeletonModification2DFABRIK, SkeletonModification2D);

private:
	struct FABRIK_Joint_Data2D {
		int bone_idx = -1;
		EntityPath bone2d_node;
		ObjectID bone2d_node_cache;

		Vector2 magnet_position = Vector2(0, 0);
		bool use_target_rotation = false;

		bool editor_draw_gizmo = true;
	};

	Vector<FABRIK_Joint_Data2D> fabrik_data_chain;

	// Unlike in 3D, we need a vector of Transform2D objects to perform FABRIK.
	// This is because FABRIK (unlike CCDIK) needs to operate on transforms that are NOT
	// affected by each other, making the transforms stored in Bone2D unusable, as well as those in Skeleton2D.
	// For this reason, this modification stores a vector of Transform2Ds used for the calculations, which are then applied at the end.
	Vector<Transform2D> fabrik_transform_chain;

	EntityPath target_node;
	ObjectID target_node_cache;
	void update_target_cache();

	float chain_tolarance = 0.01;
	int chain_max_iterations = 10;
	int chain_iterations = 0;
	Transform2D target_global_pose;
	Transform2D origin_global_pose;

	void fabrik_joint_update_bone2d_cache(int p_joint_idx);
	void chain_backwards();
	void chain_forwards();

protected:
	static void register_members();
	bool _set(const StringName &p_path, const Variant &p_value);
	bool _get(const StringName &p_path, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	void _execute(float p_delta) override;
	void _setup_modification(SkeletonModificationStack2D *p_stack) override;

	void set_target_node(const EntityPath &p_target_node);
	EntityPath get_target_node() const;

	int get_fabrik_data_chain_length();
	void set_fabrik_data_chain_length(int p_new_length);

	void set_fabrik_joint_bone2d_node(int p_joint_idx, const EntityPath &p_target_node);
	EntityPath get_fabrik_joint_bone2d_node(int p_joint_idx) const;
	void set_fabrik_joint_bone_index(int p_joint_idx, int p_bone_idx);
	int get_fabrik_joint_bone_index(int p_joint_idx) const;

	void set_fabrik_joint_magnet_position(int p_joint_idx, Vector2 p_magnet_position);
	Vector2 get_fabrik_joint_magnet_position(int p_joint_idx) const;
	void set_fabrik_joint_use_target_rotation(int p_joint_idx, bool p_use_target_rotation);
	bool get_fabrik_joint_use_target_rotation(int p_joint_idx) const;

	SkeletonModification2DFABRIK();
	~SkeletonModification2DFABRIK();
};
