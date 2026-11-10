/**************************************************************************/
/*  skeleton_modification_planar_stackholder.h                                */

#pragma once

#include "scene/planar/skeleton_planar.h"
#include "scene/resources/planar/skeleton/skeleton_modification_planar.h"

///////////////////////////////////////
// SkeletonModification2DJIGGLE
///////////////////////////////////////

class SkeletonModification2DStackHolder : public SkeletonModification2D {
	ORBLIT_CLASS(SkeletonModification2DStackHolder, SkeletonModification2D);

protected:
	static void register_members();
	bool _get(const StringName &p_path, Variant &r_ret) const;
	bool _set(const StringName &p_path, const Variant &p_value);
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	Ref<SkeletonModificationStack2D> held_modification_stack;

	void _execute(float p_delta) override;
	void _setup_modification(SkeletonModificationStack2D *p_stack) override;
	void _draw_editor_gizmo() override;

	void set_held_modification_stack(Ref<SkeletonModificationStack2D> p_held_stack);
	Ref<SkeletonModificationStack2D> get_held_modification_stack() const;

	SkeletonModification2DStackHolder();
	~SkeletonModification2DStackHolder();
};
