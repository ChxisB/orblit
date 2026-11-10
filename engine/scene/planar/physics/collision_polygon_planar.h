/**************************************************************************/
/*  collision_polygon_planar.h                                                */

#pragma once

#include "scene/planar/entity_planar.h"

class CollisionObject2D;

class CollisionPolygon2D : public Entity2D {
	ORBLIT_CLASS(CollisionPolygon2D, Entity2D);

public:
	enum BuildMode {
		BUILD_SOLIDS,
		BUILD_SEGMENTS,
	};

protected:
	Rect2 aabb = Rect2(-10, -10, 20, 20);
	BuildMode build_mode = BUILD_SOLIDS;
	Vector<Point2> polygon;
	uint32_t owner_id = 0;
	CollisionObject2D *collision_object = nullptr;
	bool disabled = false;
	bool one_way_collision = false;
	real_t one_way_collision_margin = 1.0;
	Vector2 one_way_collision_direction = Vector2(0.0, 1.0);

	Vector<Vector<Vector2>> _decompose_in_convex();

	void _build_polygon();

	void _update_in_shape_owner(bool p_xform_only = false);

protected:
	void _notification(int p_what);
	static void register_members();

public:
#ifdef DEBUG_ENABLED
	virtual Rect2 _edit_get_rect() const override;
	virtual bool _edit_use_rect() const override;
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;
#endif

	void set_build_mode(BuildMode p_mode);
	BuildMode get_build_mode() const;

	void set_polygon(const Vector<Point2> &p_polygon);
	Vector<Point2> get_polygon() const;

	PackedStringArray get_configuration_warnings() const override;

	void set_disabled(bool p_disabled);
	bool is_disabled() const;

	void set_one_way_collision(bool p_enable);
	bool is_one_way_collision_enabled() const;

	void set_one_way_collision_margin(real_t p_margin);
	real_t get_one_way_collision_margin() const;

	void set_one_way_collision_direction(const Vector2 &p_direction);
	Vector2 get_one_way_collision_direction() const;

	CollisionPolygon2D();
};

VARIANT_ENUM_CAST(CollisionPolygon2D::BuildMode);
