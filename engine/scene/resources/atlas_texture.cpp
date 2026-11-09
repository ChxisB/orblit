/**************************************************************************/
/*  atlas_texture.cpp                                                     */

#include "atlas_texture.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"

int AtlasTexture::get_width() const {
	if (rounded_region.size.width == 0) {
		if (atlas.is_valid()) {
			return atlas->get_width();
		}
		return 1;
	} else {
		return rounded_region.size.width + margin.size.width;
	}
}

int AtlasTexture::get_height() const {
	if (rounded_region.size.height == 0) {
		if (atlas.is_valid()) {
			return atlas->get_height();
		}
		return 1;
	} else {
		return rounded_region.size.height + margin.size.height;
	}
}

RID AtlasTexture::get_rid() const {
	if (atlas.is_valid()) {
		return atlas->get_rid();
	}

	return RID();
}

bool AtlasTexture::has_alpha() const {
	if (atlas.is_valid()) {
		return atlas->has_alpha();
	}

	return false;
}

void AtlasTexture::set_atlas(const Ref<Texture2D> &p_atlas) {
	ERR_FAIL_COND(p_atlas == this);
	if (atlas == p_atlas) {
		return;
	}
	// Support recursive AtlasTextures.
	if (Ref<AtlasTexture>(atlas).is_valid()) {
		atlas->disconnect_changed(callable_mp((Resource *)this, &AtlasTexture::emit_changed));
	}
	atlas = p_atlas;
	if (Ref<AtlasTexture>(atlas).is_valid()) {
		atlas->connect_changed(callable_mp((Resource *)this, &AtlasTexture::emit_changed));
	}

	emit_changed();
}

Ref<Texture2D> AtlasTexture::get_atlas() const {
	return atlas;
}

void AtlasTexture::set_region(const Rect2 &p_region) {
	if (region == p_region) {
		return;
	}
	region = p_region;
	rounded_region = Rect2(p_region.position, p_region.size.floor());
	emit_changed();
}

Rect2 AtlasTexture::get_region() const {
	return region;
}

void AtlasTexture::set_margin(const Rect2 &p_margin) {
	if (margin == p_margin) {
		return;
	}
	margin = p_margin;
	emit_changed();
}

Rect2 AtlasTexture::get_margin() const {
	return margin;
}

void AtlasTexture::set_filter_clip(const bool p_enable) {
	filter_clip = p_enable;
	emit_changed();
}

bool AtlasTexture::has_filter_clip() const {
	return filter_clip;
}

Rect2 AtlasTexture::_get_region_rect() const {
	Rect2 rc = rounded_region;
	if (atlas.is_valid()) {
		if (rc.size.width == 0) {
			rc.size.width = atlas->get_width();
		}
		if (rc.size.height == 0) {
			rc.size.height = atlas->get_height();
		}
	}
	return rc;
}

void AtlasTexture::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_atlas", "atlas"), &AtlasTexture::set_atlas);
	ClassRegistry::bind_method(D_METHOD("get_atlas"), &AtlasTexture::get_atlas);

	ClassRegistry::bind_method(D_METHOD("set_region", "region"), &AtlasTexture::set_region);
	ClassRegistry::bind_method(D_METHOD("get_region"), &AtlasTexture::get_region);

	ClassRegistry::bind_method(D_METHOD("set_margin", "margin"), &AtlasTexture::set_margin);
	ClassRegistry::bind_method(D_METHOD("get_margin"), &AtlasTexture::get_margin);

	ClassRegistry::bind_method(D_METHOD("set_filter_clip", "enable"), &AtlasTexture::set_filter_clip);
	ClassRegistry::bind_method(D_METHOD("has_filter_clip"), &AtlasTexture::has_filter_clip);

	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "atlas", PROPERTY_HINT_RESOURCE_TYPE, Texture2D::get_static_class_name()), "set_atlas", "get_atlas");
	ADD_PROPERTY(PropertyInfo(Variant::RECT2, "region", PROPERTY_HINT_NONE, "suffix:px"), "set_region", "get_region");
	ADD_PROPERTY(PropertyInfo(Variant::RECT2, "margin", PROPERTY_HINT_NONE, "suffix:px"), "set_margin", "get_margin");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "filter_clip"), "set_filter_clip", "has_filter_clip");
}

void AtlasTexture::draw(RID p_canvas_item, const Point2 &p_pos, const Color &p_modulate, bool p_transpose) const {
	if (atlas.is_null()) {
		return;
	}
	const Rect2 rc = _get_region_rect();
	atlas->draw_rect_region(p_canvas_item, Rect2(p_pos + margin.position, rc.size), rc, p_modulate, p_transpose, filter_clip);
}

void AtlasTexture::draw_rect(RID p_canvas_item, const Rect2 &p_rect, bool p_tile, const Color &p_modulate, bool p_transpose) const {
	if (atlas.is_null()) {
		return;
	}

	Rect2 src_rect = Rect2(0, 0, get_width(), get_height());

	Rect2 dr;
	Rect2 src_c;
	if (get_rect_region(p_rect, src_rect, dr, src_c)) {
		atlas->draw_rect_region(p_canvas_item, dr, src_c, p_modulate, p_transpose, filter_clip);
	}
}

void AtlasTexture::draw_rect_region(RID p_canvas_item, const Rect2 &p_rect, const Rect2 &p_src_rect, const Color &p_modulate, bool p_transpose, bool p_clip_uv) const {
	// This might not necessarily work well if using a rect, needs to be fixed properly.
	if (atlas.is_null()) {
		return;
	}

	Rect2 dr;
	Rect2 src_c;
	if (get_rect_region(p_rect, p_src_rect, dr, src_c)) {
		atlas->draw_rect_region(p_canvas_item, dr, src_c, p_modulate, p_transpose, filter_clip);
	}
}

bool AtlasTexture::get_rect_region(const Rect2 &p_rect, const Rect2 &p_src_rect, Rect2 &r_rect, Rect2 &r_src_rect) const {
	if (atlas.is_null()) {
		return false;
	}

	Rect2 src = p_src_rect;
	if (src.size == Size2()) {
		src.size = rounded_region.size;
	}
	if (src.size == Size2() && atlas.is_valid()) {
		src.size = atlas->get_size();
	}
	Vector2 scale = p_rect.size / src.size;

	src.position += (rounded_region.position - margin.position);
	Rect2 src_clipped = _get_region_rect().intersection(src);
	if (src_clipped.size == Size2()) {
		return false;
	}

	Vector2 ofs = (src_clipped.position - src.position);
	if (scale.x < 0) {
		ofs.x += (src_clipped.size.x - src.size.x);
	}
	if (scale.y < 0) {
		ofs.y += (src_clipped.size.y - src.size.y);
	}

	r_rect = Rect2(p_rect.position + ofs * scale, src_clipped.size * scale);
	r_src_rect = src_clipped;
	return true;
}

bool AtlasTexture::is_pixel_opaque(int p_x, int p_y) const {
	if (atlas.is_null()) {
		return true;
	}

	int x = p_x + rounded_region.position.x - margin.position.x;
	int y = p_y + rounded_region.position.y - margin.position.y;

	// Margin edge may outside of atlas.
	if (x < 0 || x >= atlas->get_width()) {
		return false;
	}
	if (y < 0 || y >= atlas->get_height()) {
		return false;
	}

	return atlas->is_pixel_opaque(x, y);
}

Ref<Image> AtlasTexture::get_image() const {
	if (atlas.is_null()) {
		return Ref<Image>();
	}

	const Ref<Image> &atlas_image = atlas->get_image();
	if (atlas_image.is_null()) {
		return Ref<Image>();
	}

	return atlas_image->get_region(_get_region_rect());
}
