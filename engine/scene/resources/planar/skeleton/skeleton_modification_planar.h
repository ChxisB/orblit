/**************************************************************************/
/*  skeleton_modification_planar.h                                            */

#pragma once

#include "scene/resources/planar/skeleton/skeleton_modification_stack_planar.h"

///////////////////////////////////////
// SkeletonModification2D
///////////////////////////////////////

class Bone2D;

class SkeletonModification2D : public Resource {
	ORBLIT_CLASS(SkeletonModification2D, Resource);
	friend class Skeleton2D;
	friend class Bone2D;

protected:
	static void register_members();

	SkeletonModificationStack2D *stack = nullptr;
	int execution_mode = 0; // 0 = process

	bool enabled = true;
	bool is_setup = false;

	bool _print_execution_error(bool p_condition, String p_message);

	virtual void reset_state() override;

	ORBLIT_VIRTUAL1(_execute, double)
	ORBLIT_VIRTUAL1(_setup_modification, Ref<SkeletonModificationStack2D>)
	ORBLIT_VIRTUAL0(_draw_editor_gizmo)

public:
	virtual void _execute(float _delta);
	virtual void _setup_modification(SkeletonModificationStack2D *p_stack);
	virtual void _draw_editor_gizmo();

	bool editor_draw_gizmo = false;
	void set_editor_draw_gizmo(bool p_draw_gizmo);
	bool get_editor_draw_gizmo() const;

	void set_enabled(bool p_enabled);
	bool get_enabled();

	Ref<SkeletonModificationStack2D> get_modification_stack();
	void set_is_setup(bool p_setup);
	bool get_is_setup() const;

	void set_execution_mode(int p_mode);
	int get_execution_mode() const;

	float clamp_angle(float p_angle, float p_min_bound, float p_max_bound, bool p_invert_clamp = false);
	void editor_draw_angle_constraints(Bone2D *p_operation_bone, float p_min_bound, float p_max_bound, bool p_constraint_enabled, bool p_constraint_in_localspace, bool p_constraint_inverted);

	SkeletonModification2D();
};
