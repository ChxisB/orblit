/**************************************************************************/
/*  style_box_line.cpp                                                    */

#include "style_box_line.h"

#include "core/object/class_registry.h"
#include "systems/rendering/rendering_system.h"

float StyleBoxLine::get_style_margin(Side p_side) const {
	ERR_FAIL_INDEX_V((int)p_side, 4, 0);

	if (vertical) {
		if (p_side == SIDE_LEFT || p_side == SIDE_RIGHT) {
			return thickness / 2.0;
		}
	} else if (p_side == SIDE_TOP || p_side == SIDE_BOTTOM) {
		return thickness / 2.0;
	}

	return 0;
}

void StyleBoxLine::set_color(const Color &p_color) {
	color = p_color;
	emit_changed();
}

Color StyleBoxLine::get_color() const {
	return color;
}

void StyleBoxLine::set_thickness(int p_thickness) {
	thickness = p_thickness;
	emit_changed();
}

int StyleBoxLine::get_thickness() const {
	return thickness;
}

void StyleBoxLine::set_vertical(bool p_vertical) {
	vertical = p_vertical;
	emit_changed();
}

bool StyleBoxLine::is_vertical() const {
	return vertical;
}

void StyleBoxLine::set_grow_end(float p_grow_end) {
	grow_end = p_grow_end;
	emit_changed();
}

float StyleBoxLine::get_grow_end() const {
	return grow_end;
}

void StyleBoxLine::set_grow_begin(float p_grow_begin) {
	grow_begin = p_grow_begin;
	emit_changed();
}

float StyleBoxLine::get_grow_begin() const {
	return grow_begin;
}

void StyleBoxLine::draw(RID p_canvas_item, const Rect2 &p_rect) const {
	RenderingSystem *vs = RenderingSystem::get_singleton();
	Rect2i r = p_rect;

	if (vertical) {
		r.position.y -= grow_begin;
		r.size.y += (grow_begin + grow_end);
		r.size.x = thickness;
	} else {
		r.position.x -= grow_begin;
		r.size.x += (grow_begin + grow_end);
		r.size.y = thickness;
	}

	vs->canvas_item_add_rect(p_canvas_item, r, color);
}

void StyleBoxLine::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_color", "color"), &StyleBoxLine::set_color);
	ClassRegistry::bind_method(D_METHOD("get_color"), &StyleBoxLine::get_color);
	ClassRegistry::bind_method(D_METHOD("set_thickness", "thickness"), &StyleBoxLine::set_thickness);
	ClassRegistry::bind_method(D_METHOD("get_thickness"), &StyleBoxLine::get_thickness);
	ClassRegistry::bind_method(D_METHOD("set_grow_begin", "offset"), &StyleBoxLine::set_grow_begin);
	ClassRegistry::bind_method(D_METHOD("get_grow_begin"), &StyleBoxLine::get_grow_begin);
	ClassRegistry::bind_method(D_METHOD("set_grow_end", "offset"), &StyleBoxLine::set_grow_end);
	ClassRegistry::bind_method(D_METHOD("get_grow_end"), &StyleBoxLine::get_grow_end);
	ClassRegistry::bind_method(D_METHOD("set_vertical", "vertical"), &StyleBoxLine::set_vertical);
	ClassRegistry::bind_method(D_METHOD("is_vertical"), &StyleBoxLine::is_vertical);

	ADD_PROPERTY(PropertyInfo(Variant::COLOR, "color"), "set_color", "get_color");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "grow_begin", PROPERTY_HINT_RANGE, "-300,300,1,suffix:px"), "set_grow_begin", "get_grow_begin");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "grow_end", PROPERTY_HINT_RANGE, "-300,300,1,suffix:px"), "set_grow_end", "get_grow_end");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "thickness", PROPERTY_HINT_RANGE, "0,100,suffix:px"), "set_thickness", "get_thickness");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "vertical"), "set_vertical", "is_vertical");
}
