/**************************************************************************/
/*  shape_planar.h                                                            */

#pragma once

#include "core/io/resource.h"

class Shape2D : public Resource {
	ORBLIT_CLASS(Shape2D, Resource);
	OBJ_SAVE_TYPE(Shape2D);

	RID shape;
	real_t custom_bias = 0.0;

protected:
	static void register_members();
	Shape2D(const RID &p_rid);

public:
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const { return get_rect().has_point(p_point); }

	void set_custom_solver_bias(real_t p_bias);
	real_t get_custom_solver_bias() const;

	bool collide_with_motion(const Transform2D &p_local_xform, const Vector2 &p_local_motion, RequiredParam<Shape2D> p_shape, const Transform2D &p_shape_xform, const Vector2 &p_shape_motion);
	bool collide(const Transform2D &p_local_xform, RequiredParam<Shape2D> p_shape, const Transform2D &p_shape_xform);

	PackedVector2Array collide_with_motion_and_get_contacts(const Transform2D &p_local_xform, const Vector2 &p_local_motion, RequiredParam<Shape2D> p_shape, const Transform2D &p_shape_xform, const Vector2 &p_shape_motion);
	PackedVector2Array collide_and_get_contacts(const Transform2D &p_local_xform, RequiredParam<Shape2D> p_shape, const Transform2D &p_shape_xform);

	virtual void draw(const RID &p_to_rid, const Color &p_color) {}
	virtual Rect2 get_rect() const { return Rect2(); }
	/// Returns the radius of a circle that fully enclose this shape
	virtual real_t get_enclosing_radius() const = 0;
	virtual RID get_rid() const override;

	static bool is_collision_outline_enabled();

	~Shape2D();
};
