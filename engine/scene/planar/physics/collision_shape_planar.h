/**************************************************************************/
/*  collision_shape_planar.h                                                  */

#pragma once

#include "scene/planar/entity_planar.h"
#include "scene/resources/planar/shape_planar.h"

class CollisionObject2D;

class CollisionShape2D : public Entity2D {
	ORBLIT_CLASS(CollisionShape2D, Entity2D);
	Ref<Shape2D> shape;
	Rect2 rect = Rect2(-Point2(10, 10), Point2(20, 20));
	uint32_t owner_id = 0;
	CollisionObject2D *collision_object = nullptr;
	bool disabled = false;
	bool one_way_collision = false;
	real_t one_way_collision_margin = 1.0;
	Vector2 one_way_collision_direction = Vector2(0.0, 1.0);

	void _shape_changed();
	void _update_in_shape_owner(bool p_xform_only = false);

	// Not wrapped in `#ifdef DEBUG_ENABLED` as it is used for rendering.
	Color debug_color = Color(0.0, 0.0, 0.0, 0.0);

	Color _get_default_debug_color() const;

protected:
	void _notification(int p_what);

#ifdef DEBUG_ENABLED
	bool _property_can_revert(const StringName &p_name) const;
	bool _property_get_revert(const StringName &p_name, Variant &r_property) const;
	void _validate_property(PropertyInfo &p_property) const;
#endif // DEBUG_ENABLED

	static void register_members();

public:
#ifdef DEBUG_ENABLED
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;
#else
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const;
#endif // DEBUG_ENABLED

	void set_shape(const Ref<Shape2D> &p_shape);
	Ref<Shape2D> get_shape() const;

	void set_disabled(bool p_disabled);
	bool is_disabled() const;

	void set_one_way_collision(bool p_enable);
	bool is_one_way_collision_enabled() const;

	void set_one_way_collision_margin(real_t p_margin);
	real_t get_one_way_collision_margin() const;

	void set_one_way_collision_direction(const Vector2 &p_direction);
	Vector2 get_one_way_collision_direction() const;

	void set_debug_color(const Color &p_color);
	Color get_debug_color() const;

	PackedStringArray get_configuration_warnings() const override;

	CollisionShape2D();
};
