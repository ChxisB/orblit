/**************************************************************************/
/*  rich_text_effect.cpp                                                  */

#include "rich_text_effect.h"

#include "core/object/class_registry.h"
#include "core/object/script_language.h"

CharFXTransform::CharFXTransform() {
}

CharFXTransform::~CharFXTransform() {
	environment.clear();
}

void RichTextEffect::register_members(){
	ORBLIT_VIRTUAL_BIND(_process_custom_fx, "char_fx")
}

Variant RichTextEffect::get_bbcode() const {
	Variant r;
	if (get_script_instance()) {
		if (!get_script_instance()->get("bbcode", r)) {
			String path = get_script_instance()->get_script()->get_path();
			r = path.get_file().get_basename();
		}
	}
	return r;
}

bool RichTextEffect::_process_effect_impl(Ref<CharFXTransform> p_cfx) {
	bool return_value = false;
	ORBLIT_VIRTUAL_CALL(_process_custom_fx, p_cfx, return_value);
	return return_value;
}

RichTextEffect::RichTextEffect() {
}

void CharFXTransform::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_transform"), &CharFXTransform::get_transform);
	ClassRegistry::bind_method(D_METHOD("set_transform", "transform"), &CharFXTransform::set_transform);

	ClassRegistry::bind_method(D_METHOD("get_range"), &CharFXTransform::get_range);
	ClassRegistry::bind_method(D_METHOD("set_range", "range"), &CharFXTransform::set_range);

	ClassRegistry::bind_method(D_METHOD("get_elapsed_time"), &CharFXTransform::get_elapsed_time);
	ClassRegistry::bind_method(D_METHOD("set_elapsed_time", "time"), &CharFXTransform::set_elapsed_time);

	ClassRegistry::bind_method(D_METHOD("is_visible"), &CharFXTransform::is_visible);
	ClassRegistry::bind_method(D_METHOD("set_visibility", "visibility"), &CharFXTransform::set_visibility);

	ClassRegistry::bind_method(D_METHOD("is_outline"), &CharFXTransform::is_outline);
	ClassRegistry::bind_method(D_METHOD("set_outline", "outline"), &CharFXTransform::set_outline);

	ClassRegistry::bind_method(D_METHOD("get_offset"), &CharFXTransform::get_offset);
	ClassRegistry::bind_method(D_METHOD("set_offset", "offset"), &CharFXTransform::set_offset);

	ClassRegistry::bind_method(D_METHOD("get_color"), &CharFXTransform::get_color);
	ClassRegistry::bind_method(D_METHOD("set_color", "color"), &CharFXTransform::set_color);

	ClassRegistry::bind_method(D_METHOD("get_environment"), &CharFXTransform::get_environment);
	ClassRegistry::bind_method(D_METHOD("set_environment", "environment"), &CharFXTransform::set_environment);

	ClassRegistry::bind_method(D_METHOD("get_glyph_index"), &CharFXTransform::get_glyph_index);
	ClassRegistry::bind_method(D_METHOD("set_glyph_index", "glyph_index"), &CharFXTransform::set_glyph_index);

	ClassRegistry::bind_method(D_METHOD("get_relative_index"), &CharFXTransform::get_relative_index);
	ClassRegistry::bind_method(D_METHOD("set_relative_index", "relative_index"), &CharFXTransform::set_relative_index);

	ClassRegistry::bind_method(D_METHOD("get_glyph_count"), &CharFXTransform::get_glyph_count);
	ClassRegistry::bind_method(D_METHOD("set_glyph_count", "glyph_count"), &CharFXTransform::set_glyph_count);

	ClassRegistry::bind_method(D_METHOD("get_glyph_flags"), &CharFXTransform::get_glyph_flags);
	ClassRegistry::bind_method(D_METHOD("set_glyph_flags", "glyph_flags"), &CharFXTransform::set_glyph_flags);

	ClassRegistry::bind_method(D_METHOD("get_font"), &CharFXTransform::get_font);
	ClassRegistry::bind_method(D_METHOD("set_font", "font"), &CharFXTransform::set_font);

	ADD_PROPERTY(PropertyInfo(Variant::TRANSFORM2D, "transform"), "set_transform", "get_transform");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2I, "range"), "set_range", "get_range");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "elapsed_time"), "set_elapsed_time", "get_elapsed_time");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "visible"), "set_visibility", "is_visible");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "outline"), "set_outline", "is_outline");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "offset"), "set_offset", "get_offset");
	ADD_PROPERTY(PropertyInfo(Variant::COLOR, "color"), "set_color", "get_color");
	ADD_PROPERTY(PropertyInfo(Variant::DICTIONARY, "env"), "set_environment", "get_environment");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "glyph_index"), "set_glyph_index", "get_glyph_index");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "glyph_count"), "set_glyph_count", "get_glyph_count");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "glyph_flags"), "set_glyph_flags", "get_glyph_flags");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "relative_index"), "set_relative_index", "get_relative_index");
	ADD_PROPERTY(PropertyInfo(Variant::RID, "font"), "set_font", "get_font");
}
