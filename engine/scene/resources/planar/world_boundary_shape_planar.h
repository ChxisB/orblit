/**************************************************************************/
/*  world_boundary_shape_planar.h                                             */

#pragma once

#include "scene/resources/planar/shape_planar.h"

class WorldBoundaryShape2D : public Shape2D {
	ORBLIT_CLASS(WorldBoundaryShape2D, Shape2D);

	// WorldBoundaryShape2D is often used for one-way platforms, where the normal pointing up makes sense.
	Vector2 normal = Vector2(0, -1);
	real_t distance = 0.0;

	void _update_shape();

protected:
	static void register_members();

public:
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;

	void set_normal(const Vector2 &p_normal);
	void set_distance(real_t p_distance);

	Vector2 get_normal() const;
	real_t get_distance() const;

	virtual void draw(const RID &p_to_rid, const Color &p_color) override;
	virtual Rect2 get_rect() const override;
	virtual real_t get_enclosing_radius() const override;

	WorldBoundaryShape2D();
};
