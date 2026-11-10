/**************************************************************************/
/*  rectangle_shape_planar.cpp                                                */

#include "rectangle_shape_planar.h"

#include "core/object/class_registry.h"
#include "systems/physics_planar/physics_system_planar.h"
#include "systems/rendering/rendering_system.h"
void RectangleShape2D::_update_shape() {
	PhysicsSystem2D::get_singleton()->shape_set_data(get_rid(), size * 0.5);
	emit_changed();
}

void RectangleShape2D::set_size(const Size2 &p_size) {
	ERR_FAIL_COND_MSG(p_size.x < 0 || p_size.y < 0, "RectangleShape2D size cannot be negative.");
	if (size == p_size) {
		return;
	}
	size = p_size;
	_update_shape();
}

Size2 RectangleShape2D::get_size() const {
	return size;
}

void RectangleShape2D::draw(const RID &p_to_rid, const Color &p_color) {
	RenderingSystem::get_singleton()->canvas_item_add_rect(p_to_rid, Rect2(-size * 0.5, size), p_color);
	if (is_collision_outline_enabled()) {
		// Draw an outlined rectangle to make individual shapes easier to distinguish.
		Vector<Vector2> stroke_points;
		stroke_points.resize(5);
		stroke_points.write[0] = -size * 0.5;
		stroke_points.write[1] = Vector2(size.x, -size.y) * 0.5;
		stroke_points.write[2] = size * 0.5;
		stroke_points.write[3] = Vector2(-size.x, size.y) * 0.5;
		stroke_points.write[4] = -size * 0.5;

		Vector<Color> stroke_colors = { Color(p_color, 1.0) };

		RenderingSystem::get_singleton()->canvas_item_add_polyline(p_to_rid, stroke_points, stroke_colors);
	}
}

Rect2 RectangleShape2D::get_rect() const {
	return Rect2(-size * 0.5, size);
}

real_t RectangleShape2D::get_enclosing_radius() const {
	return size.length() / 2;
}

void RectangleShape2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_size", "size"), &RectangleShape2D::set_size);
	ClassRegistry::bind_method(D_METHOD("get_size"), &RectangleShape2D::get_size);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "size", PROPERTY_HINT_NONE, "suffix:px"), "set_size", "get_size");
}

RectangleShape2D::RectangleShape2D() :
		Shape2D(PhysicsSystem2D::get_singleton()->rectangle_shape_create()) {
	size = Size2(20, 20);
	_update_shape();
}
