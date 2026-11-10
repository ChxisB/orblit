/**************************************************************************/
/*  physical_bone_simulator_spatial.h                                          */

#pragma once

#include "scene/spatial/skeleton_modifier_spatial.h"

class PhysicalBone3D;

class PhysicalBoneSimulator3D : public SkeletonModifier3D {
	ORBLIT_CLASS(PhysicalBoneSimulator3D, SkeletonModifier3D);

	bool simulating = false;

	struct SimulatedBone {
		int parent;
		Vector<int> child_bones;

		Transform3D global_pose;

		PhysicalBone3D *physical_bone = nullptr;
		PhysicalBone3D *cache_parent_physical_bone = nullptr;

		SimulatedBone() {
			parent = -1;
			global_pose = Transform3D();
			physical_bone = nullptr;
			cache_parent_physical_bone = nullptr;
		}
	};

	LocalVector<SimulatedBone> bones;

	/// This is a slow API, so it's cached
	PhysicalBone3D *_get_physical_bone_parent(int p_bone);
	void _rebuild_physical_bones_cache();
	void _reset_physical_bones_state();

protected:
	static void register_members();

	virtual void _set_active(bool p_active) override;

	void _bone_list_changed();
	void _pose_updated();
	void _bone_pose_updated(Skeleton3D *skeleton, int p_bone_id);

	virtual void _process_modification(double p_delta) override;

	virtual void _skeleton_changed(Skeleton3D *p_old, Skeleton3D *p_new) override;

public:
	bool is_simulating_physics() const;

	int find_bone(const String &p_name) const;
	String get_bone_name(int p_bone) const;
	int get_bone_count() const;
	bool is_bone_parent_of(int p_bone_id, int p_parent_bone_id) const;

	Transform3D get_bone_global_pose(int p_bone) const;
	void set_bone_global_pose(int p_bone, const Transform3D &p_pose);

	void bind_physical_bone_to_bone(int p_bone, PhysicalBone3D *p_physical_bone);
	void unbind_physical_bone_from_bone(int p_bone);

	PhysicalBone3D *get_physical_bone(int p_bone);
	PhysicalBone3D *get_physical_bone_parent(int p_bone);

	void physical_bones_stop_simulation();
	void physical_bones_start_simulation_on(const TypedArray<StringName> &p_bones);
	void physical_bones_add_collision_exception(RID p_exception);
	void physical_bones_remove_collision_exception(RID p_exception);

	PhysicalBoneSimulator3D();
};
