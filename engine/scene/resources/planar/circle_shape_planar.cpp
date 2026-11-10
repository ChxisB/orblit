/**************************************************************************/
/*  circle_shape_planar.cpp                                                   */

#include "circle_shape_planar.h"

#include "core/object/class_registry.h"
#include "systems/physics_planar/physics_system_planar.h"
#include "systems/rendering/rendering_system.h"

bool CircleShape2D::_edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const {
	return p_point.length() < get_radius() + p_tolerance;
}

void CircleShape2D::_update_shape() {
	PhysicsSystem2D::get_singleton()->shape_set_data(get_rid(), radius);
	emit_changed();
}

void CircleShape2D::set_radius(real_t p_radius) {
	ERR_FAIL_COND_MSG(p_radius < 0, "CircleShape2D radius cannot be negative.");
	if (radius == p_radius) {
		return;
	}
	radius = p_radius;
	_update_shape();
}

real_t CircleShape2D::get_radius() const {
	return radius;
}

void CircleShape2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_radius", "radius"), &CircleShape2D::set_radius);
	ClassRegistry::bind_method(D_METHOD("get_radius"), &CircleShape2D::get_radius);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "radius", PROPERTY_HINT_RANGE, "0.01,1024,0.01,or_greater,suffix:px"), "set_radius", "get_radius");
}

Rect2 CircleShape2D::get_rect() const {
	Rect2 rect;
	rect.position = -Point2(get_radius(), get_radius());
	rect.size = Point2(get_radius(), get_radius()) * 2.0;
	return rect;
}

real_t CircleShape2D::get_enclosing_radius() const {
	return radius;
}

void CircleShape2D::draw(const RID &p_to_rid, const Color &p_color) {
	Vector<Vector2> points;
	points.resize(24);

	const real_t turn_step = Math::TAU / 24.0;
	for (int i = 0; i < 24; i++) {
		points.write[i] = Vector2(Math::cos(i * turn_step), Math::sin(i * turn_step)) * get_radius();
	}

	Vector<Color> col = { p_color };
	RenderingSystem::get_singleton()->canvas_item_add_polygon(p_to_rid, points, col);

	if (is_collision_outline_enabled()) {
		points.push_back(points[0]);
		col = { Color(p_color, 1.0) };
		RenderingSystem::get_singleton()->canvas_item_add_polyline(p_to_rid, points, col);
	}
}

CircleShape2D::CircleShape2D() :
		Shape2D(PhysicsSystem2D::get_singleton()->circle_shape_create()) {
	_update_shape();
}
