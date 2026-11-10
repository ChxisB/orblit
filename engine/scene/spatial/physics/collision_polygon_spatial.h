/**************************************************************************/
/*  collision_polygon_spatial.h                                                */

#pragma once

#include "scene/spatial/entity_spatial.h"

class CollisionObject3D;
class CollisionPolygon3D : public Entity3D {
	ORBLIT_CLASS(CollisionPolygon3D, Entity3D);
	real_t margin = 0.04;

protected:
	real_t depth = 1.0;
	AABB aabb = AABB(Vector3(-1, -1, -1), Vector3(2, 2, 2));
	Vector<Point2> polygon;

	uint32_t owner_id = 0;
	CollisionObject3D *collision_object = nullptr;

	Color debug_color;
	bool debug_fill = true;

	Color _get_default_debug_color() const;

	bool disabled = false;

	void _build_polygon();

	void _update_in_shape_owner(bool p_xform_only = false);

	bool _is_editable_3d_polygon() const;

protected:
	void _notification(int p_what);
	static void register_members();

#ifdef DEBUG_ENABLED
	bool _property_can_revert(const StringName &p_name) const;
	bool _property_get_revert(const StringName &p_name, Variant &r_property) const;
	void _validate_property(PropertyInfo &p_property) const;
#endif // DEBUG_ENABLED

public:
	void set_depth(real_t p_depth);
	real_t get_depth() const;

	void set_polygon(const Vector<Point2> &p_polygon);
	Vector<Point2> get_polygon() const;

	void set_disabled(bool p_disabled);
	bool is_disabled() const;

	void set_debug_color(const Color &p_color);
	Color get_debug_color() const;

	void set_debug_fill_enabled(bool p_enable);
	bool get_debug_fill_enabled() const;

	virtual AABB get_item_rect() const;

	real_t get_margin() const;
	void set_margin(real_t p_margin);

	PackedStringArray get_configuration_warnings() const override;

	CollisionPolygon3D();
};
