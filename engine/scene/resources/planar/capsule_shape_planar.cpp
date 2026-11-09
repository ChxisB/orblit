/**************************************************************************/
/*  capsule_shape_planar.cpp                                                  */

#include "capsule_shape_planar.h"

#include "core/math/geometry_planar.h"
#include "core/object/class_registry.h"
#include "systems/physics_planar/physics_system_planar.h"
#include "systems/rendering/rendering_system.h"

Vector<Vector2> CapsuleShape2D::_get_points() const {
	Vector<Vector2> points;
	const real_t turn_step = Math::TAU / 24.0;
	for (int i = 0; i < 24; i++) {
		Vector2 ofs = Vector2(0, (i > 6 && i <= 18) ? -height * 0.5f + radius : height * 0.5f - radius);

		points.push_back(Vector2(Math::sin(i * turn_step), Math::cos(i * turn_step)) * radius + ofs);
		if (i == 6 || i == 18) {
			points.push_back(Vector2(Math::sin(i * turn_step), Math::cos(i * turn_step)) * radius - ofs);
		}
	}

	return points;
}

bool CapsuleShape2D::_edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const {
	return Geometry2D::is_point_in_polygon(p_point, _get_points());
}

void CapsuleShape2D::_update_shape() {
	PhysicsSystem2D::get_singleton()->shape_set_data(get_rid(), Vector2(radius, height));
	emit_changed();
}

void CapsuleShape2D::set_radius(real_t p_radius) {
	ERR_FAIL_COND_MSG(p_radius < 0.0f, "CapsuleShape2D radius cannot be negative.");
	if (radius == p_radius) {
		return;
	}
	radius = p_radius;
	if (height < radius * 2.0f) {
		height = radius * 2.0f;
	}
	_update_shape();
}

real_t CapsuleShape2D::get_radius() const {
	return radius;
}

void CapsuleShape2D::set_height(real_t p_height) {
	ERR_FAIL_COND_MSG(p_height < 0.0f, "CapsuleShape2D height cannot be negative.");
	if (height == p_height) {
		return;
	}
	height = p_height;
	if (radius > height * 0.5f) {
		radius = height * 0.5f;
	}
	_update_shape();
}

real_t CapsuleShape2D::get_height() const {
	return height;
}

void CapsuleShape2D::set_mid_height(real_t p_mid_height) {
	ERR_FAIL_COND_MSG(p_mid_height < 0.0f, "CapsuleShape2D mid-height cannot be negative.");
	height = p_mid_height + radius * 2.0f;
	_update_shape();
}

real_t CapsuleShape2D::get_mid_height() const {
	return height - radius * 2.0f;
}

void CapsuleShape2D::draw(const RID &p_to_rid, const Color &p_color) {
	Vector<Vector2> points = _get_points();
	Vector<Color> col = { p_color };
	RenderingSystem::get_singleton()->canvas_item_add_polygon(p_to_rid, points, col);

	if (is_collision_outline_enabled()) {
		points.push_back(points[0]);
		col = { Color(p_color, 1.0f) };
		RenderingSystem::get_singleton()->canvas_item_add_polyline(p_to_rid, points, col);
	}
}

Rect2 CapsuleShape2D::get_rect() const {
	const Vector2 half_size = Vector2(radius, height * 0.5f);
	return Rect2(-half_size, half_size * 2.0f);
}

real_t CapsuleShape2D::get_enclosing_radius() const {
	return height * 0.5f;
}

void CapsuleShape2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_radius", "radius"), &CapsuleShape2D::set_radius);
	ClassRegistry::bind_method(D_METHOD("get_radius"), &CapsuleShape2D::get_radius);

	ClassRegistry::bind_method(D_METHOD("set_height", "height"), &CapsuleShape2D::set_height);
	ClassRegistry::bind_method(D_METHOD("get_height"), &CapsuleShape2D::get_height);

	ClassRegistry::bind_method(D_METHOD("set_mid_height", "mid_height"), &CapsuleShape2D::set_mid_height);
	ClassRegistry::bind_method(D_METHOD("get_mid_height"), &CapsuleShape2D::get_mid_height);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "radius", PROPERTY_HINT_RANGE, "0.01,1024,0.01,or_greater,suffix:px"), "set_radius", "get_radius");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "height", PROPERTY_HINT_RANGE, "0.01,1024,0.01,or_greater,suffix:px"), "set_height", "get_height");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "mid_height", PROPERTY_HINT_RANGE, "0.01,1024,0.01,or_greater,suffix:px", PROPERTY_USAGE_NONE), "set_mid_height", "get_mid_height");
	ADD_LINKED_PROPERTY("radius", "height");
	ADD_LINKED_PROPERTY("height", "radius");
}

CapsuleShape2D::CapsuleShape2D() :
		Shape2D(PhysicsSystem2D::get_singleton()->capsule_shape_create()) {
	_update_shape();
}
