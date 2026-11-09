/**************************************************************************/
/*  segment_shape_planar.cpp                                                  */

#include "segment_shape_planar.h"

#include "core/math/geometry_planar.h"
#include "core/object/class_registry.h"
#include "systems/physics_planar/physics_system_planar.h"
#include "systems/rendering/rendering_system.h"

bool SegmentShape2D::_edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const {
	Vector2 closest = Geometry2D::get_closest_point_to_segment(p_point, a, b);
	return p_point.distance_to(closest) < p_tolerance;
}

void SegmentShape2D::_update_shape() {
	Rect2 r;
	r.position = a;
	r.size = b;
	PhysicsSystem2D::get_singleton()->shape_set_data(get_rid(), r);
	emit_changed();
}

void SegmentShape2D::set_a(const Vector2 &p_a) {
	if (a == p_a) {
		return;
	}
	a = p_a;
	_update_shape();
}

Vector2 SegmentShape2D::get_a() const {
	return a;
}

void SegmentShape2D::set_b(const Vector2 &p_b) {
	if (b == p_b) {
		return;
	}
	b = p_b;
	_update_shape();
}

Vector2 SegmentShape2D::get_b() const {
	return b;
}

void SegmentShape2D::draw(const RID &p_to_rid, const Color &p_color) {
	RenderingSystem::get_singleton()->canvas_item_add_line(p_to_rid, a, b, p_color, 3);
}

Rect2 SegmentShape2D::get_rect() const {
	Rect2 rect;
	rect.position = a;
	rect.expand_to(b);
	return rect;
}

real_t SegmentShape2D::get_enclosing_radius() const {
	return (a + b).length();
}

void SegmentShape2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_a", "a"), &SegmentShape2D::set_a);
	ClassRegistry::bind_method(D_METHOD("get_a"), &SegmentShape2D::get_a);

	ClassRegistry::bind_method(D_METHOD("set_b", "b"), &SegmentShape2D::set_b);
	ClassRegistry::bind_method(D_METHOD("get_b"), &SegmentShape2D::get_b);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "a", PROPERTY_HINT_NONE, "suffix:px"), "set_a", "get_a");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "b", PROPERTY_HINT_NONE, "suffix:px"), "set_b", "get_b");
}

SegmentShape2D::SegmentShape2D() :
		Shape2D(PhysicsSystem2D::get_singleton()->segment_shape_create()) {
	a = Vector2();
	b = Vector2(0, 10);
	_update_shape();
}
