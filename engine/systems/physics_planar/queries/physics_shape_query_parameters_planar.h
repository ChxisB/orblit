/**************************************************************************/
/*  physics_shape_query_parameters_planar.h                                   */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_planar/physics_system_planar_types.h"

class Resource;
template <typename T>
class Ref;

class PhysicsShapeQueryParameters2D : public RefCounted {
	ORBLIT_CLASS(PhysicsShapeQueryParameters2D, RefCounted);

	PS2DT::ShapeParameters parameters;

	Ref<Resource> shape_ref;

protected:
	static void register_members();

public:
	const PS2DT::ShapeParameters &get_parameters() const { return parameters; }

	void set_shape(const Ref<Resource> &p_shape_ref);
	Ref<Resource> get_shape() const;

	void set_shape_rid(const RID &p_shape);
	RID get_shape_rid() const { return parameters.shape_rid; }

	void set_transform(const Transform2D &p_transform) { parameters.transform = p_transform; }
	const Transform2D &get_transform() const { return parameters.transform; }

	void set_motion(const Vector2 &p_motion) { parameters.motion = p_motion; }
	const Vector2 &get_motion() const { return parameters.motion; }

	void set_margin(real_t p_margin) { parameters.margin = p_margin; }
	real_t get_margin() const { return parameters.margin; }

	void set_collision_mask(uint32_t p_mask) { parameters.collision_mask = p_mask; }
	uint32_t get_collision_mask() const { return parameters.collision_mask; }

	void set_collide_with_bodies(bool p_enable) { parameters.collide_with_bodies = p_enable; }
	bool is_collide_with_bodies_enabled() const { return parameters.collide_with_bodies; }

	void set_collide_with_areas(bool p_enable) { parameters.collide_with_areas = p_enable; }
	bool is_collide_with_areas_enabled() const { return parameters.collide_with_areas; }

	void set_exclude(const TypedArray<RID> &p_exclude);
	TypedArray<RID> get_exclude() const;
};
