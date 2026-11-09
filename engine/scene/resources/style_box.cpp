/**************************************************************************/
/*  style_box.cpp                                                         */

#include "style_box.h"

#include "core/object/class_registry.h"
#include "scene/stage/canvas_item.h"

Size2 StyleBox::get_minimum_size() const {
	Size2 min_size = Size2(get_margin(SIDE_LEFT) + get_margin(SIDE_RIGHT), get_margin(SIDE_TOP) + get_margin(SIDE_BOTTOM));
	Size2 custom_size;
	ORBLIT_VIRTUAL_CALL(_get_minimum_size, custom_size);

	if (min_size.x < custom_size.x) {
		min_size.x = custom_size.x;
	}
	if (min_size.y < custom_size.y) {
		min_size.y = custom_size.y;
	}

	return min_size;
}

void StyleBox::set_content_margin(Side p_side, float p_value) {
	ERR_FAIL_INDEX((int)p_side, 4);

	content_margin[p_side] = p_value;
	emit_changed();
}

void StyleBox::set_content_margin_all(float p_value) {
	for (int i = 0; i < 4; i++) {
		content_margin[i] = p_value;
	}
	emit_changed();
}

void StyleBox::set_content_margin_individual(float p_left, float p_top, float p_right, float p_bottom) {
	content_margin[SIDE_LEFT] = p_left;
	content_margin[SIDE_TOP] = p_top;
	content_margin[SIDE_RIGHT] = p_right;
	content_margin[SIDE_BOTTOM] = p_bottom;
	emit_changed();
}

float StyleBox::get_content_margin(Side p_side) const {
	ERR_FAIL_INDEX_V((int)p_side, 4, 0.0);

	return content_margin[p_side];
}

float StyleBox::get_margin(Side p_side) const {
	ERR_FAIL_INDEX_V((int)p_side, 4, 0.0);

	if (content_margin[p_side] < 0) {
		return get_style_margin(p_side);
	} else {
		return content_margin[p_side];
	}
}

Point2 StyleBox::get_offset() const {
	return Point2(get_margin(SIDE_LEFT), get_margin(SIDE_TOP));
}

void StyleBox::draw(RID p_canvas_item, const Rect2 &p_rect) const {
	ORBLIT_VIRTUAL_CALL(_draw, p_canvas_item, p_rect);
}

Rect2 StyleBox::get_draw_rect(const Rect2 &p_rect) const {
	Rect2 ret;
	if (ORBLIT_VIRTUAL_CALL(_get_draw_rect, p_rect, ret)) {
		return ret;
	}
	return p_rect;
}

CanvasItem *StyleBox::get_current_item_drawn() const {
	return CanvasItem::get_current_item_drawn();
}

bool StyleBox::test_mask(const Point2 &p_point, const Rect2 &p_rect) const {
	bool ret = true;
	ORBLIT_VIRTUAL_CALL(_test_mask, p_point, p_rect, ret);
	return ret;
}

void StyleBox::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_minimum_size"), &StyleBox::get_minimum_size);

	ClassRegistry::bind_method(D_METHOD("set_content_margin", "margin", "offset"), &StyleBox::set_content_margin);
	ClassRegistry::bind_method(D_METHOD("set_content_margin_all", "offset"), &StyleBox::set_content_margin_all);
	ClassRegistry::bind_method(D_METHOD("get_content_margin", "margin"), &StyleBox::get_content_margin);

	ClassRegistry::bind_method(D_METHOD("get_margin", "margin"), &StyleBox::get_margin);
	ClassRegistry::bind_method(D_METHOD("get_offset"), &StyleBox::get_offset);

	ClassRegistry::bind_method(D_METHOD("draw", "canvas_item", "rect"), &StyleBox::draw);
	ClassRegistry::bind_method(D_METHOD("get_current_item_drawn"), &StyleBox::get_current_item_drawn);

	ClassRegistry::bind_method(D_METHOD("test_mask", "point", "rect"), &StyleBox::test_mask);

	ADD_GROUP("Content Margins", "content_margin_");
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "content_margin_left", PROPERTY_HINT_RANGE, "-1,2048,1,suffix:px"), "set_content_margin", "get_content_margin", SIDE_LEFT);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "content_margin_top", PROPERTY_HINT_RANGE, "-1,2048,1,suffix:px"), "set_content_margin", "get_content_margin", SIDE_TOP);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "content_margin_right", PROPERTY_HINT_RANGE, "-1,2048,1,suffix:px"), "set_content_margin", "get_content_margin", SIDE_RIGHT);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "content_margin_bottom", PROPERTY_HINT_RANGE, "-1,2048,1,suffix:px"), "set_content_margin", "get_content_margin", SIDE_BOTTOM);

	ORBLIT_VIRTUAL_BIND(_draw, "to_canvas_item", "rect")
	ORBLIT_VIRTUAL_BIND(_get_draw_rect, "rect")
	ORBLIT_VIRTUAL_BIND(_get_minimum_size)
	ORBLIT_VIRTUAL_BIND(_test_mask, "point", "rect")
}

StyleBox::StyleBox() {
	for (int i = 0; i < 4; i++) {
		content_margin[i] = -1;
	}
}
