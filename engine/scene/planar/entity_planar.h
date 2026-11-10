/**************************************************************************/
/*  entity_planar.h                                                           */

#pragma once

#include "scene/stage/canvas_item.h"

class Entity2D : public CanvasItem {
	ORBLIT_CLASS(Entity2D, CanvasItem);

	mutable MTFlag xform_dirty;
	mutable Point2 position;
	mutable real_t rotation = 0.0;
	mutable Size2 scale = Vector2(1, 1);
	mutable real_t skew = 0.0;

	Transform2D transform;

	_FORCE_INLINE_ bool _is_xform_dirty() const { return is_group_processing() ? xform_dirty.mt.is_set() : xform_dirty.st; }
	void _set_xform_dirty(bool p_dirty) const;

	void _update_transform();

	void _update_xform_values() const;

protected:
	void _notification(int p_notification);
	static void register_members();

public:
	static constexpr AncestralClass static_ancestral_class = AncestralClass::NODE_2D;

#ifdef DEBUG_ENABLED
	virtual Dictionary _edit_get_state() const override;
	virtual void _edit_set_state(const Dictionary &p_state) override;

	virtual void _edit_set_position(const Point2 &p_position) override;
	virtual Point2 _edit_get_position() const override;

	virtual void _edit_set_scale(const Size2 &p_scale) override;
	virtual Size2 _edit_get_scale() const override;

	virtual void _edit_set_rotation(real_t p_rotation) override;
	virtual real_t _edit_get_rotation() const override;
	virtual bool _edit_use_rotation() const override;

	virtual void _edit_set_rect(const Rect2 &p_edit_rect) override;
#endif // DEBUG_ENABLED

	virtual void reparent(RequiredParam<Entity> p_parent, bool p_keep_global_transform = true) override;

	void set_position(const Point2 &p_pos);
	void set_rotation(real_t p_radians);
	void set_rotation_degrees(real_t p_degrees);
	void set_skew(real_t p_radians);
	void set_scale(const Size2 &p_scale);

	void rotate(real_t p_radians);
	void move_x(real_t p_delta, bool p_scaled = false);
	void move_y(real_t p_delta, bool p_scaled = false);
	void translate(const Vector2 &p_amount);
	void global_translate(const Vector2 &p_amount);
	void apply_scale(const Size2 &p_amount);

	Point2 get_position() const;
	real_t get_rotation() const;
	real_t get_rotation_degrees() const;
	real_t get_skew() const;
	Size2 get_scale() const;

	Point2 get_global_position() const;
	real_t get_global_rotation() const;
	real_t get_global_rotation_degrees() const;
	real_t get_global_skew() const;
	Size2 get_global_scale() const;

	void set_transform(const Transform2D &p_transform);
	void set_global_transform(const Transform2D &p_transform);
	void set_global_position(const Point2 &p_pos);
	void set_global_rotation(const real_t p_radians);
	void set_global_rotation_degrees(const real_t p_degrees);
	void set_global_skew(const real_t p_radians);
	void set_global_scale(const Size2 &p_scale);

	void look_at(const Vector2 &p_pos);
	real_t get_angle_to(const Vector2 &p_pos) const;

	Point2 to_local(const Point2 &p_global) const;
	Point2 to_global(const Point2 &p_local) const;

	Transform2D get_relative_transform_to_parent(RequiredParam<const Entity> p_parent) const;

	Transform2D get_transform() const override;

	Entity2D();
};
