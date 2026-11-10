/**************************************************************************/
/*  text_system_extension.cpp                                             */

#include "text_system_extension.h"

#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.

void TextSystemExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_has_feature, "feature");
	ORBLIT_VIRTUAL_BIND(_get_name);
	ORBLIT_VIRTUAL_BIND(_get_short_name);
	ORBLIT_VIRTUAL_BIND(_get_features);

	ORBLIT_VIRTUAL_BIND(_free_rid, "rid");
	ORBLIT_VIRTUAL_BIND(_has, "rid");
	ORBLIT_VIRTUAL_BIND(_load_support_data, "filename");

	ORBLIT_VIRTUAL_BIND(_get_support_data_filename);
	ORBLIT_VIRTUAL_BIND(_get_support_data_info);
	ORBLIT_VIRTUAL_BIND(_save_support_data, "filename");
	ORBLIT_VIRTUAL_BIND(_get_support_data);
	ORBLIT_VIRTUAL_BIND(_is_locale_using_support_data, "locale");

	ORBLIT_VIRTUAL_BIND(_is_locale_right_to_left, "locale");

	ORBLIT_VIRTUAL_BIND(_name_to_tag, "name");
	ORBLIT_VIRTUAL_BIND(_tag_to_name, "tag");

	/* Font interface */

	ORBLIT_VIRTUAL_BIND(_create_font);
	ORBLIT_VIRTUAL_BIND(_create_font_linked_variation, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_data, "font_rid", "data");
	ORBLIT_VIRTUAL_BIND(_font_set_data_ptr, "font_rid", "data_ptr", "data_size");

	ORBLIT_VIRTUAL_BIND(_font_set_face_index, "font_rid", "face_index");
	ORBLIT_VIRTUAL_BIND(_font_get_face_index, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_get_face_count, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_style, "font_rid", "style");
	ORBLIT_VIRTUAL_BIND(_font_get_style, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_name, "font_rid", "name");
	ORBLIT_VIRTUAL_BIND(_font_get_name, "font_rid");
	ORBLIT_VIRTUAL_BIND(_font_get_ot_name_strings, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_style_name, "font_rid", "name_style");
	ORBLIT_VIRTUAL_BIND(_font_get_style_name, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_weight, "font_rid", "weight");
	ORBLIT_VIRTUAL_BIND(_font_get_weight, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_stretch, "font_rid", "stretch");
	ORBLIT_VIRTUAL_BIND(_font_get_stretch, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_antialiasing, "font_rid", "antialiasing");
	ORBLIT_VIRTUAL_BIND(_font_get_antialiasing, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_disable_embedded_bitmaps, "font_rid", "disable_embedded_bitmaps");
	ORBLIT_VIRTUAL_BIND(_font_get_disable_embedded_bitmaps, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_generate_mipmaps, "font_rid", "generate_mipmaps");
	ORBLIT_VIRTUAL_BIND(_font_get_generate_mipmaps, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_multichannel_signed_distance_field, "font_rid", "msdf");
	ORBLIT_VIRTUAL_BIND(_font_is_multichannel_signed_distance_field, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_msdf_pixel_range, "font_rid", "msdf_pixel_range");
	ORBLIT_VIRTUAL_BIND(_font_get_msdf_pixel_range, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_msdf_size, "font_rid", "msdf_size");
	ORBLIT_VIRTUAL_BIND(_font_get_msdf_size, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_fixed_size, "font_rid", "fixed_size");
	ORBLIT_VIRTUAL_BIND(_font_get_fixed_size, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_fixed_size_scale_mode, "font_rid", "fixed_size_scale_mode");
	ORBLIT_VIRTUAL_BIND(_font_get_fixed_size_scale_mode, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_allow_system_fallback, "font_rid", "allow_system_fallback");
	ORBLIT_VIRTUAL_BIND(_font_is_allow_system_fallback, "font_rid");
	ORBLIT_VIRTUAL_BIND(_font_clear_system_fallback_cache);

	ORBLIT_VIRTUAL_BIND(_font_set_force_autohinter, "font_rid", "force_autohinter");
	ORBLIT_VIRTUAL_BIND(_font_is_force_autohinter, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_modulate_color_glyphs, "font_rid", "modulate");
	ORBLIT_VIRTUAL_BIND(_font_is_modulate_color_glyphs, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_get_palette_count, "font_rid");
	ORBLIT_VIRTUAL_BIND(_font_get_palette_name, "font_rid", "index");
	ORBLIT_VIRTUAL_BIND(_font_get_palette_colors, "font_rid", "index");
	ORBLIT_VIRTUAL_BIND(_font_set_palette_custom_colors, "font_rid", "colors");
	ORBLIT_VIRTUAL_BIND(_font_get_palette_custom_colors, "font_rid");
	ORBLIT_VIRTUAL_BIND(_font_get_used_palette, "font_rid");
	ORBLIT_VIRTUAL_BIND(_font_set_used_palette, "font_rid", "index");

	ORBLIT_VIRTUAL_BIND(_font_set_hinting, "font_rid", "hinting");
	ORBLIT_VIRTUAL_BIND(_font_get_hinting, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_subpixel_positioning, "font_rid", "subpixel_positioning");
	ORBLIT_VIRTUAL_BIND(_font_get_subpixel_positioning, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_keep_rounding_remainders, "font_rid", "keep_rounding_remainders");
	ORBLIT_VIRTUAL_BIND(_font_get_keep_rounding_remainders, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_embolden, "font_rid", "strength");
	ORBLIT_VIRTUAL_BIND(_font_get_embolden, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_spacing, "font_rid", "spacing", "value");
	ORBLIT_VIRTUAL_BIND(_font_get_spacing, "font_rid", "spacing");

	ORBLIT_VIRTUAL_BIND(_font_set_baseline_offset, "font_rid", "baseline_offset");
	ORBLIT_VIRTUAL_BIND(_font_get_baseline_offset, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_transform, "font_rid", "transform");
	ORBLIT_VIRTUAL_BIND(_font_get_transform, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_variation_coordinates, "font_rid", "variation_coordinates");
	ORBLIT_VIRTUAL_BIND(_font_get_variation_coordinates, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_get_size_cache_list, "font_rid");
	ORBLIT_VIRTUAL_BIND(_font_clear_size_cache, "font_rid");
	ORBLIT_VIRTUAL_BIND(_font_remove_size_cache, "font_rid", "size");
	ORBLIT_VIRTUAL_BIND(_font_get_size_cache_info, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_ascent, "font_rid", "size", "ascent");
	ORBLIT_VIRTUAL_BIND(_font_get_ascent, "font_rid", "size");

	ORBLIT_VIRTUAL_BIND(_font_set_descent, "font_rid", "size", "descent");
	ORBLIT_VIRTUAL_BIND(_font_get_descent, "font_rid", "size");

	ORBLIT_VIRTUAL_BIND(_font_set_underline_position, "font_rid", "size", "underline_position");
	ORBLIT_VIRTUAL_BIND(_font_get_underline_position, "font_rid", "size");

	ORBLIT_VIRTUAL_BIND(_font_set_underline_thickness, "font_rid", "size", "underline_thickness");
	ORBLIT_VIRTUAL_BIND(_font_get_underline_thickness, "font_rid", "size");

	ORBLIT_VIRTUAL_BIND(_font_set_scale, "font_rid", "size", "scale");
	ORBLIT_VIRTUAL_BIND(_font_get_scale, "font_rid", "size");

	ORBLIT_VIRTUAL_BIND(_font_get_texture_count, "font_rid", "size");
	ORBLIT_VIRTUAL_BIND(_font_clear_textures, "font_rid", "size");
	ORBLIT_VIRTUAL_BIND(_font_remove_texture, "font_rid", "size", "texture_index");

	ORBLIT_VIRTUAL_BIND(_font_set_texture_image, "font_rid", "size", "texture_index", "image");
	ORBLIT_VIRTUAL_BIND(_font_get_texture_image, "font_rid", "size", "texture_index");

	ORBLIT_VIRTUAL_BIND(_font_set_texture_offsets, "font_rid", "size", "texture_index", "offset");
	ORBLIT_VIRTUAL_BIND(_font_get_texture_offsets, "font_rid", "size", "texture_index");

	ORBLIT_VIRTUAL_BIND(_font_get_glyph_list, "font_rid", "size");
	ORBLIT_VIRTUAL_BIND(_font_clear_glyphs, "font_rid", "size");
	ORBLIT_VIRTUAL_BIND(_font_remove_glyph, "font_rid", "size", "glyph");

	ORBLIT_VIRTUAL_BIND(_font_get_glyph_advance, "font_rid", "size", "glyph");
	ORBLIT_VIRTUAL_BIND(_font_set_glyph_advance, "font_rid", "size", "glyph", "advance");

	ORBLIT_VIRTUAL_BIND(_font_get_glyph_offset, "font_rid", "size", "glyph");
	ORBLIT_VIRTUAL_BIND(_font_set_glyph_offset, "font_rid", "size", "glyph", "offset");

	ORBLIT_VIRTUAL_BIND(_font_get_glyph_size, "font_rid", "size", "glyph");
	ORBLIT_VIRTUAL_BIND(_font_set_glyph_size, "font_rid", "size", "glyph", "gl_size");

	ORBLIT_VIRTUAL_BIND(_font_get_glyph_uv_rect, "font_rid", "size", "glyph");
	ORBLIT_VIRTUAL_BIND(_font_set_glyph_uv_rect, "font_rid", "size", "glyph", "uv_rect");

	ORBLIT_VIRTUAL_BIND(_font_get_glyph_texture_idx, "font_rid", "size", "glyph");
	ORBLIT_VIRTUAL_BIND(_font_set_glyph_texture_idx, "font_rid", "size", "glyph", "texture_idx");

	ORBLIT_VIRTUAL_BIND(_font_get_glyph_texture_rid, "font_rid", "size", "glyph");
	ORBLIT_VIRTUAL_BIND(_font_get_glyph_texture_size, "font_rid", "size", "glyph");

	ORBLIT_VIRTUAL_BIND(_font_get_glyph_contours, "font_rid", "size", "index");

	ORBLIT_VIRTUAL_BIND(_font_get_kerning_list, "font_rid", "size");
	ORBLIT_VIRTUAL_BIND(_font_clear_kerning_map, "font_rid", "size");
	ORBLIT_VIRTUAL_BIND(_font_remove_kerning, "font_rid", "size", "glyph_pair");

	ORBLIT_VIRTUAL_BIND(_font_set_kerning, "font_rid", "size", "glyph_pair", "kerning");
	ORBLIT_VIRTUAL_BIND(_font_get_kerning, "font_rid", "size", "glyph_pair");

	ORBLIT_VIRTUAL_BIND(_font_get_glyph_index, "font_rid", "size", "char", "variation_selector");
	ORBLIT_VIRTUAL_BIND(_font_get_char_from_glyph_index, "font_rid", "size", "glyph_index");

	ORBLIT_VIRTUAL_BIND(_font_has_char, "font_rid", "char");
	ORBLIT_VIRTUAL_BIND(_font_get_supported_chars, "font_rid");
	ORBLIT_VIRTUAL_BIND(_font_get_supported_glyphs, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_render_range, "font_rid", "size", "start", "end");
	ORBLIT_VIRTUAL_BIND(_font_render_glyph, "font_rid", "size", "index");

	ORBLIT_VIRTUAL_BIND(_font_draw_glyph, "font_rid", "canvas", "size", "pos", "index", "color", "oversampling");
	ORBLIT_VIRTUAL_BIND(_font_draw_glyph_outline, "font_rid", "canvas", "size", "outline_size", "pos", "index", "color", "oversampling");

	ORBLIT_VIRTUAL_BIND(_font_is_language_supported, "font_rid", "language");
	ORBLIT_VIRTUAL_BIND(_font_set_language_support_override, "font_rid", "language", "supported");
	ORBLIT_VIRTUAL_BIND(_font_get_language_support_override, "font_rid", "language");
	ORBLIT_VIRTUAL_BIND(_font_remove_language_support_override, "font_rid", "language");
	ORBLIT_VIRTUAL_BIND(_font_get_language_support_overrides, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_is_script_supported, "font_rid", "script");
	ORBLIT_VIRTUAL_BIND(_font_set_script_support_override, "font_rid", "script", "supported");
	ORBLIT_VIRTUAL_BIND(_font_get_script_support_override, "font_rid", "script");
	ORBLIT_VIRTUAL_BIND(_font_remove_script_support_override, "font_rid", "script");
	ORBLIT_VIRTUAL_BIND(_font_get_script_support_overrides, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_set_opentype_feature_overrides, "font_rid", "overrides");
	ORBLIT_VIRTUAL_BIND(_font_get_opentype_feature_overrides, "font_rid");

	ORBLIT_VIRTUAL_BIND(_font_supported_feature_list, "font_rid");
	ORBLIT_VIRTUAL_BIND(_font_supported_variation_list, "font_rid");

	ORBLIT_VIRTUAL_BIND(_reference_oversampling_level, "oversampling");
	ORBLIT_VIRTUAL_BIND(_unreference_oversampling_level, "oversampling");

	ORBLIT_VIRTUAL_BIND(_get_hex_code_box_size, "size", "index");
	ORBLIT_VIRTUAL_BIND(_draw_hex_code_box, "canvas", "size", "pos", "index", "color");

	/* Shaped text buffer interface */

	ORBLIT_VIRTUAL_BIND(_create_shaped_text, "direction", "orientation");

	ORBLIT_VIRTUAL_BIND(_shaped_text_clear, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_duplicate, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_set_direction, "shaped", "direction");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_direction, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_inferred_direction, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_set_bidi_override, "shaped", "override");

	ORBLIT_VIRTUAL_BIND(_shaped_text_set_custom_punctuation, "shaped", "punct");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_custom_punctuation, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_set_custom_ellipsis, "shaped", "char");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_custom_ellipsis, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_set_orientation, "shaped", "orientation");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_orientation, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_set_preserve_invalid, "shaped", "enabled");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_preserve_invalid, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_set_preserve_control, "shaped", "enabled");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_preserve_control, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_set_spacing, "shaped", "spacing", "value");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_spacing, "shaped", "spacing");

	ORBLIT_VIRTUAL_BIND(_shaped_text_add_string, "shaped", "text", "fonts", "size", "opentype_features", "language", "meta");
	ORBLIT_VIRTUAL_BIND(_shaped_text_add_object, "shaped", "key", "size", "inline_align", "length", "baseline");
	ORBLIT_VIRTUAL_BIND(_shaped_text_resize_object, "shaped", "key", "size", "inline_align", "baseline");
	ORBLIT_VIRTUAL_BIND(_shaped_text_has_object, "shaped", "key");
	ORBLIT_VIRTUAL_BIND(_shaped_get_text, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_get_span_count, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_get_span_meta, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_span_embedded_object, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_span_text, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_span_object, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_set_span_update_font, "shaped", "index", "fonts", "size", "opentype_features");

	ORBLIT_VIRTUAL_BIND(_shaped_get_run_count, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_get_run_text, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_run_range, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_run_glyph_range, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_run_font_rid, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_run_font_size, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_run_language, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_run_direction, "shaped", "index");
	ORBLIT_VIRTUAL_BIND(_shaped_get_run_object, "shaped", "index");

	ORBLIT_VIRTUAL_BIND(_shaped_text_substr, "shaped", "start", "length");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_parent, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_fit_to_width, "shaped", "width", "justification_flags");
	ORBLIT_VIRTUAL_BIND(_shaped_text_tab_align, "shaped", "tab_stops");

	ORBLIT_VIRTUAL_BIND(_shaped_text_shape, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_update_breaks, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_update_justification_ops, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_is_ready, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_glyphs, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_sort_logical, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_glyph_count, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_range, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_line_breaks_adv, "shaped", "width", "start", "once", "break_flags");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_line_breaks, "shaped", "width", "start", "break_flags");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_word_breaks, "shaped", "grapheme_flags", "skip_grapheme_flags");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_trim_pos, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_ellipsis_pos, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_ellipsis_glyph_count, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_ellipsis_glyphs, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_overrun_trim_to_width, "shaped", "width", "trim_flags");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_objects, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_object_rect, "shaped", "key");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_object_range, "shaped", "key");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_object_glyph, "shaped", "key");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_size, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_ascent, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_descent, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_width, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_underline_position, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_underline_thickness, "shaped");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_dominant_direction_in_range, "shaped", "start", "end");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_carets, "shaped", "position", "r_caret");
	ORBLIT_VIRTUAL_BIND(_shaped_text_get_selection, "shaped", "start", "end");

	ORBLIT_VIRTUAL_BIND(_shaped_text_hit_test_grapheme, "shaped", "coord");
	ORBLIT_VIRTUAL_BIND(_shaped_text_hit_test_position, "shaped", "coord");

	ORBLIT_VIRTUAL_BIND(_shaped_text_draw, "shaped", "canvas", "pos", "clip_l", "clip_r", "color", "oversampling");
	ORBLIT_VIRTUAL_BIND(_shaped_text_draw_outline, "shaped", "canvas", "pos", "clip_l", "clip_r", "outline_size", "color", "oversampling");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_grapheme_bounds, "shaped", "pos");
	ORBLIT_VIRTUAL_BIND(_shaped_text_next_grapheme_pos, "shaped", "pos");
	ORBLIT_VIRTUAL_BIND(_shaped_text_prev_grapheme_pos, "shaped", "pos");

	ORBLIT_VIRTUAL_BIND(_shaped_text_get_character_breaks, "shaped");
	ORBLIT_VIRTUAL_BIND(_shaped_text_next_character_pos, "shaped", "pos");
	ORBLIT_VIRTUAL_BIND(_shaped_text_prev_character_pos, "shaped", "pos");
	ORBLIT_VIRTUAL_BIND(_shaped_text_closest_character_pos, "shaped", "pos");

	ORBLIT_VIRTUAL_BIND(_strip_diacritics, "string");
	ORBLIT_VIRTUAL_BIND(_is_valid_identifier, "string");
	ORBLIT_VIRTUAL_BIND(_is_valid_letter, "unicode");

	ORBLIT_VIRTUAL_BIND(_string_get_word_breaks, "string", "language", "chars_per_line");
	ORBLIT_VIRTUAL_BIND(_string_get_character_breaks, "string", "language");

	ORBLIT_VIRTUAL_BIND(_is_confusable, "string", "dict");
	ORBLIT_VIRTUAL_BIND(_spoof_check, "string");

	ORBLIT_VIRTUAL_BIND(_string_to_upper, "string", "language");
	ORBLIT_VIRTUAL_BIND(_string_to_lower, "string", "language");
	ORBLIT_VIRTUAL_BIND(_string_to_title, "string", "language");

	ORBLIT_VIRTUAL_BIND(_parse_structured_text, "parser_type", "args", "text");

	ORBLIT_VIRTUAL_BIND(_cleanup);
}

bool TextSystemExtension::has_feature(Feature p_feature) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_has_feature, p_feature, ret);
	return ret;
}

String TextSystemExtension::get_name() const {
	String ret = "Unknown";
	ORBLIT_VIRTUAL_CALL(_get_name, ret);
	return ret;
}

String TextSystemExtension::get_short_name() const {
	String ret = "unknown";
	ORBLIT_VIRTUAL_CALL(_get_short_name, ret);
	if (ret == "unknown") {
		// Fall back to a simplified version of the full name if not overridden.
		ret = get_name().to_lower().replace_char(' ', '_');
	}
	return ret;
}

int64_t TextSystemExtension::get_features() const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_get_features, ret);
	return ret;
}

void TextSystemExtension::free_rid(const RID &p_rid) {
	ORBLIT_VIRTUAL_CALL(_free_rid, p_rid);
}

bool TextSystemExtension::has(const RID &p_rid) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_has, p_rid, ret);
	return ret;
}

bool TextSystemExtension::load_support_data(const String &p_filename) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_load_support_data, p_filename, ret);
	return ret;
}

String TextSystemExtension::get_support_data_filename() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_support_data_filename, ret);
	return ret;
}

String TextSystemExtension::get_support_data_info() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_support_data_info, ret);
	return ret;
}

bool TextSystemExtension::save_support_data(const String &p_filename) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_save_support_data, p_filename, ret);
	return ret;
}

PackedByteArray TextSystemExtension::get_support_data() const {
	PackedByteArray ret;
	ORBLIT_VIRTUAL_CALL(_get_support_data, ret);
	return ret;
}

bool TextSystemExtension::is_locale_using_support_data(const String &p_locale) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_is_locale_using_support_data, p_locale, ret);
	return ret;
}

bool TextSystemExtension::is_locale_right_to_left(const String &p_locale) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_is_locale_right_to_left, p_locale, ret);
	return ret;
}

int64_t TextSystemExtension::name_to_tag(const String &p_name) const {
	int64_t ret = 0;
	if (ORBLIT_VIRTUAL_CALL(_name_to_tag, p_name, ret)) {
		return ret;
	}
	return TextSystem::name_to_tag(p_name);
}

String TextSystemExtension::tag_to_name(int64_t p_tag) const {
	String ret;
	if (ORBLIT_VIRTUAL_CALL(_tag_to_name, p_tag, ret)) {
		return ret;
	}
	return TextSystem::tag_to_name(p_tag);
}

/*************************************************************************/
/* Font                                                                  */
/*************************************************************************/

RID TextSystemExtension::create_font() {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_create_font, ret);
	return ret;
}

RID TextSystemExtension::create_font_linked_variation(const RID &p_font_rid) {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_create_font_linked_variation, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_data(const RID &p_font_rid, const PackedByteArray &p_data) {
	ORBLIT_VIRTUAL_CALL(_font_set_data, p_font_rid, p_data);
}

void TextSystemExtension::font_set_data_ptr(const RID &p_font_rid, const uint8_t *p_data_ptr, int64_t p_data_size) {
	ORBLIT_VIRTUAL_CALL(_font_set_data_ptr, p_font_rid, p_data_ptr, p_data_size);
}

void TextSystemExtension::font_set_face_index(const RID &p_font_rid, int64_t p_index) {
	ORBLIT_VIRTUAL_CALL(_font_set_face_index, p_font_rid, p_index);
}

int64_t TextSystemExtension::font_get_face_index(const RID &p_font_rid) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_face_index, p_font_rid, ret);
	return ret;
}

int64_t TextSystemExtension::font_get_face_count(const RID &p_font_rid) const {
	int64_t ret = 1;
	ORBLIT_VIRTUAL_CALL(_font_get_face_count, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_style(const RID &p_font_rid, BitField<TextSystem::FontStyle> p_style) {
	ORBLIT_VIRTUAL_CALL(_font_set_style, p_font_rid, p_style);
}

BitField<TextSystem::FontStyle> TextSystemExtension::font_get_style(const RID &p_font_rid) const {
	BitField<TextSystem::FontStyle> ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_style, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_style_name(const RID &p_font_rid, const String &p_name) {
	ORBLIT_VIRTUAL_CALL(_font_set_style_name, p_font_rid, p_name);
}

String TextSystemExtension::font_get_style_name(const RID &p_font_rid) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_font_get_style_name, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_weight(const RID &p_font_rid, int64_t p_weight) {
	ORBLIT_VIRTUAL_CALL(_font_set_weight, p_font_rid, p_weight);
}

int64_t TextSystemExtension::font_get_weight(const RID &p_font_rid) const {
	int64_t ret = 400;
	ORBLIT_VIRTUAL_CALL(_font_get_weight, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_stretch(const RID &p_font_rid, int64_t p_stretch) {
	ORBLIT_VIRTUAL_CALL(_font_set_stretch, p_font_rid, p_stretch);
}

int64_t TextSystemExtension::font_get_stretch(const RID &p_font_rid) const {
	int64_t ret = 100;
	ORBLIT_VIRTUAL_CALL(_font_get_stretch, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_name(const RID &p_font_rid, const String &p_name) {
	ORBLIT_VIRTUAL_CALL(_font_set_name, p_font_rid, p_name);
}

String TextSystemExtension::font_get_name(const RID &p_font_rid) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_font_get_name, p_font_rid, ret);
	return ret;
}

Dictionary TextSystemExtension::font_get_ot_name_strings(const RID &p_font_rid) const {
	Dictionary ret;
	ORBLIT_VIRTUAL_CALL(_font_get_ot_name_strings, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_antialiasing(const RID &p_font_rid, TextSystem::FontAntialiasing p_antialiasing) {
	ORBLIT_VIRTUAL_CALL(_font_set_antialiasing, p_font_rid, p_antialiasing);
}

TextSystem::FontAntialiasing TextSystemExtension::font_get_antialiasing(const RID &p_font_rid) const {
	TextSystem::FontAntialiasing ret = TextSystem::FONT_ANTIALIASING_NONE;
	ORBLIT_VIRTUAL_CALL(_font_get_antialiasing, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_disable_embedded_bitmaps(const RID &p_font_rid, bool p_disable_embedded_bitmaps) {
	ORBLIT_VIRTUAL_CALL(_font_set_disable_embedded_bitmaps, p_font_rid, p_disable_embedded_bitmaps);
}

bool TextSystemExtension::font_get_disable_embedded_bitmaps(const RID &p_font_rid) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_get_disable_embedded_bitmaps, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_generate_mipmaps(const RID &p_font_rid, bool p_generate_mipmaps) {
	ORBLIT_VIRTUAL_CALL(_font_set_generate_mipmaps, p_font_rid, p_generate_mipmaps);
}

bool TextSystemExtension::font_get_generate_mipmaps(const RID &p_font_rid) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_get_generate_mipmaps, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_multichannel_signed_distance_field(const RID &p_font_rid, bool p_msdf) {
	ORBLIT_VIRTUAL_CALL(_font_set_multichannel_signed_distance_field, p_font_rid, p_msdf);
}

bool TextSystemExtension::font_is_multichannel_signed_distance_field(const RID &p_font_rid) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_is_multichannel_signed_distance_field, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_msdf_pixel_range(const RID &p_font_rid, int64_t p_msdf_pixel_range) {
	ORBLIT_VIRTUAL_CALL(_font_set_msdf_pixel_range, p_font_rid, p_msdf_pixel_range);
}

int64_t TextSystemExtension::font_get_msdf_pixel_range(const RID &p_font_rid) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_msdf_pixel_range, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_msdf_size(const RID &p_font_rid, int64_t p_msdf_size) {
	ORBLIT_VIRTUAL_CALL(_font_set_msdf_size, p_font_rid, p_msdf_size);
}

int64_t TextSystemExtension::font_get_msdf_size(const RID &p_font_rid) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_msdf_size, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_fixed_size(const RID &p_font_rid, int64_t p_fixed_size) {
	ORBLIT_VIRTUAL_CALL(_font_set_fixed_size, p_font_rid, p_fixed_size);
}

int64_t TextSystemExtension::font_get_fixed_size(const RID &p_font_rid) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_fixed_size, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_fixed_size_scale_mode(const RID &p_font_rid, TextSystem::FixedSizeScaleMode p_fixed_size_scale_mode) {
	ORBLIT_VIRTUAL_CALL(_font_set_fixed_size_scale_mode, p_font_rid, p_fixed_size_scale_mode);
}

TextSystem::FixedSizeScaleMode TextSystemExtension::font_get_fixed_size_scale_mode(const RID &p_font_rid) const {
	FixedSizeScaleMode ret = FIXED_SIZE_SCALE_DISABLE;
	ORBLIT_VIRTUAL_CALL(_font_get_fixed_size_scale_mode, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_allow_system_fallback(const RID &p_font_rid, bool p_allow_system_fallback) {
	ORBLIT_VIRTUAL_CALL(_font_set_allow_system_fallback, p_font_rid, p_allow_system_fallback);
}

bool TextSystemExtension::font_is_allow_system_fallback(const RID &p_font_rid) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_is_allow_system_fallback, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_clear_system_fallback_cache() {
	ORBLIT_VIRTUAL_CALL(_font_clear_system_fallback_cache);
}

void TextSystemExtension::font_set_force_autohinter(const RID &p_font_rid, bool p_force_autohinter) {
	ORBLIT_VIRTUAL_CALL(_font_set_force_autohinter, p_font_rid, p_force_autohinter);
}

bool TextSystemExtension::font_is_force_autohinter(const RID &p_font_rid) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_is_force_autohinter, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_modulate_color_glyphs(const RID &p_font_rid, bool p_modulate) {
	ORBLIT_VIRTUAL_CALL(_font_set_modulate_color_glyphs, p_font_rid, p_modulate);
}

bool TextSystemExtension::font_is_modulate_color_glyphs(const RID &p_font_rid) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_is_modulate_color_glyphs, p_font_rid, ret);
	return ret;
}

int64_t TextSystemExtension::font_get_palette_count(const RID &p_font_rid) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_palette_count, p_font_rid, ret);
	return ret;
}

String TextSystemExtension::font_get_palette_name(const RID &p_font_rid, int64_t p_index) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_font_get_palette_name, p_font_rid, p_index, ret);
	return ret;
}
Vector<Color> TextSystemExtension::font_get_palette_colors(const RID &p_font_rid, int64_t p_index) const {
	Vector<Color> ret;
	ORBLIT_VIRTUAL_CALL(_font_get_palette_colors, p_font_rid, p_index, ret);
	return ret;
}

void TextSystemExtension::font_set_palette_custom_colors(const RID &p_font_rid, const Vector<Color> &p_colors) {
	ORBLIT_VIRTUAL_CALL(_font_set_palette_custom_colors, p_font_rid, p_colors);
}

Vector<Color> TextSystemExtension::font_get_palette_custom_colors(const RID &p_font_rid) const {
	Vector<Color> ret;
	ORBLIT_VIRTUAL_CALL(_font_get_palette_custom_colors, p_font_rid, ret);
	return ret;
}

int64_t TextSystemExtension::font_get_used_palette(const RID &p_font_rid) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_used_palette, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_used_palette(const RID &p_font_rid, int64_t p_index) {
	ORBLIT_VIRTUAL_CALL(_font_set_used_palette, p_font_rid, p_index);
}

void TextSystemExtension::font_set_hinting(const RID &p_font_rid, TextSystem::Hinting p_hinting) {
	ORBLIT_VIRTUAL_CALL(_font_set_hinting, p_font_rid, p_hinting);
}

TextSystem::Hinting TextSystemExtension::font_get_hinting(const RID &p_font_rid) const {
	TextSystem::Hinting ret = TextSystem::HINTING_NONE;
	ORBLIT_VIRTUAL_CALL(_font_get_hinting, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_subpixel_positioning(const RID &p_font_rid, TextSystem::SubpixelPositioning p_subpixel) {
	ORBLIT_VIRTUAL_CALL(_font_set_subpixel_positioning, p_font_rid, p_subpixel);
}

TextSystem::SubpixelPositioning TextSystemExtension::font_get_subpixel_positioning(const RID &p_font_rid) const {
	TextSystem::SubpixelPositioning ret = TextSystem::SUBPIXEL_POSITIONING_DISABLED;
	ORBLIT_VIRTUAL_CALL(_font_get_subpixel_positioning, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_keep_rounding_remainders(const RID &p_font_rid, bool p_keep_rounding_remainders) {
	ORBLIT_VIRTUAL_CALL(_font_set_keep_rounding_remainders, p_font_rid, p_keep_rounding_remainders);
}

bool TextSystemExtension::font_get_keep_rounding_remainders(const RID &p_font_rid) const {
	bool ret = true;
	ORBLIT_VIRTUAL_CALL(_font_get_keep_rounding_remainders, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_embolden(const RID &p_font_rid, double p_strength) {
	ORBLIT_VIRTUAL_CALL(_font_set_embolden, p_font_rid, p_strength);
}

double TextSystemExtension::font_get_embolden(const RID &p_font_rid) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_embolden, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_spacing(const RID &p_font_rid, SpacingType p_spacing, int64_t p_value) {
	ORBLIT_VIRTUAL_CALL(_font_set_spacing, p_font_rid, p_spacing, p_value);
}

int64_t TextSystemExtension::font_get_spacing(const RID &p_font_rid, SpacingType p_spacing) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_spacing, p_font_rid, p_spacing, ret);
	return ret;
}

void TextSystemExtension::font_set_baseline_offset(const RID &p_font_rid, double p_baseline_offset) {
	ORBLIT_VIRTUAL_CALL(_font_set_baseline_offset, p_font_rid, p_baseline_offset);
}

double TextSystemExtension::font_get_baseline_offset(const RID &p_font_rid) const {
	double ret = 0.0;
	ORBLIT_VIRTUAL_CALL(_font_get_baseline_offset, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_transform(const RID &p_font_rid, const Transform2D &p_transform) {
	ORBLIT_VIRTUAL_CALL(_font_set_transform, p_font_rid, p_transform);
}

Transform2D TextSystemExtension::font_get_transform(const RID &p_font_rid) const {
	Transform2D ret;
	ORBLIT_VIRTUAL_CALL(_font_get_transform, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_variation_coordinates(const RID &p_font_rid, const Dictionary &p_variation_coordinates) {
	ORBLIT_VIRTUAL_CALL(_font_set_variation_coordinates, p_font_rid, p_variation_coordinates);
}

Dictionary TextSystemExtension::font_get_variation_coordinates(const RID &p_font_rid) const {
	Dictionary ret;
	ORBLIT_VIRTUAL_CALL(_font_get_variation_coordinates, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_oversampling(const RID &p_font_rid, double p_oversampling) {
	ORBLIT_VIRTUAL_CALL(_font_set_oversampling, p_font_rid, p_oversampling);
}

double TextSystemExtension::font_get_oversampling(const RID &p_font_rid) const {
	double ret = -1.0;
	ORBLIT_VIRTUAL_CALL(_font_get_oversampling, p_font_rid, ret);
	return ret;
}

TypedArray<Vector2i> TextSystemExtension::font_get_size_cache_list(const RID &p_font_rid) const {
	TypedArray<Vector2i> ret;
	ORBLIT_VIRTUAL_CALL(_font_get_size_cache_list, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_clear_size_cache(const RID &p_font_rid) {
	ORBLIT_VIRTUAL_CALL(_font_clear_size_cache, p_font_rid);
}

void TextSystemExtension::font_remove_size_cache(const RID &p_font_rid, const Vector2i &p_size) {
	ORBLIT_VIRTUAL_CALL(_font_remove_size_cache, p_font_rid, p_size);
}

TypedArray<Dictionary> TextSystemExtension::font_get_size_cache_info(const RID &p_font_rid) const {
	TypedArray<Dictionary> ret;
	ORBLIT_VIRTUAL_CALL(_font_get_size_cache_info, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_ascent(const RID &p_font_rid, int64_t p_size, double p_ascent) {
	ORBLIT_VIRTUAL_CALL(_font_set_ascent, p_font_rid, p_size, p_ascent);
}

double TextSystemExtension::font_get_ascent(const RID &p_font_rid, int64_t p_size) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_ascent, p_font_rid, p_size, ret);
	return ret;
}

void TextSystemExtension::font_set_descent(const RID &p_font_rid, int64_t p_size, double p_descent) {
	ORBLIT_VIRTUAL_CALL(_font_set_descent, p_font_rid, p_size, p_descent);
}

double TextSystemExtension::font_get_descent(const RID &p_font_rid, int64_t p_size) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_descent, p_font_rid, p_size, ret);
	return ret;
}

void TextSystemExtension::font_set_underline_position(const RID &p_font_rid, int64_t p_size, double p_underline_position) {
	ORBLIT_VIRTUAL_CALL(_font_set_underline_position, p_font_rid, p_size, p_underline_position);
}

double TextSystemExtension::font_get_underline_position(const RID &p_font_rid, int64_t p_size) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_underline_position, p_font_rid, p_size, ret);
	return ret;
}

void TextSystemExtension::font_set_underline_thickness(const RID &p_font_rid, int64_t p_size, double p_underline_thickness) {
	ORBLIT_VIRTUAL_CALL(_font_set_underline_thickness, p_font_rid, p_size, p_underline_thickness);
}

double TextSystemExtension::font_get_underline_thickness(const RID &p_font_rid, int64_t p_size) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_underline_thickness, p_font_rid, p_size, ret);
	return ret;
}

void TextSystemExtension::font_set_scale(const RID &p_font_rid, int64_t p_size, double p_scale) {
	ORBLIT_VIRTUAL_CALL(_font_set_scale, p_font_rid, p_size, p_scale);
}

double TextSystemExtension::font_get_scale(const RID &p_font_rid, int64_t p_size) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_scale, p_font_rid, p_size, ret);
	return ret;
}

int64_t TextSystemExtension::font_get_texture_count(const RID &p_font_rid, const Vector2i &p_size) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_texture_count, p_font_rid, p_size, ret);
	return ret;
}

void TextSystemExtension::font_clear_textures(const RID &p_font_rid, const Vector2i &p_size) {
	ORBLIT_VIRTUAL_CALL(_font_clear_textures, p_font_rid, p_size);
}

void TextSystemExtension::font_remove_texture(const RID &p_font_rid, const Vector2i &p_size, int64_t p_texture_index) {
	ORBLIT_VIRTUAL_CALL(_font_remove_texture, p_font_rid, p_size, p_texture_index);
}

void TextSystemExtension::font_set_texture_image(const RID &p_font_rid, const Vector2i &p_size, int64_t p_texture_index, const Ref<Image> &p_image) {
	ORBLIT_VIRTUAL_CALL(_font_set_texture_image, p_font_rid, p_size, p_texture_index, p_image);
}

Ref<Image> TextSystemExtension::font_get_texture_image(const RID &p_font_rid, const Vector2i &p_size, int64_t p_texture_index) const {
	Ref<Image> ret;
	ORBLIT_VIRTUAL_CALL(_font_get_texture_image, p_font_rid, p_size, p_texture_index, ret);
	return ret;
}

void TextSystemExtension::font_set_texture_offsets(const RID &p_font_rid, const Vector2i &p_size, int64_t p_texture_index, const PackedInt32Array &p_offset) {
	ORBLIT_VIRTUAL_CALL(_font_set_texture_offsets, p_font_rid, p_size, p_texture_index, p_offset);
}

PackedInt32Array TextSystemExtension::font_get_texture_offsets(const RID &p_font_rid, const Vector2i &p_size, int64_t p_texture_index) const {
	PackedInt32Array ret;
	ORBLIT_VIRTUAL_CALL(_font_get_texture_offsets, p_font_rid, p_size, p_texture_index, ret);
	return ret;
}

PackedInt32Array TextSystemExtension::font_get_glyph_list(const RID &p_font_rid, const Vector2i &p_size) const {
	PackedInt32Array ret;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_list, p_font_rid, p_size, ret);
	return ret;
}

void TextSystemExtension::font_clear_glyphs(const RID &p_font_rid, const Vector2i &p_size) {
	ORBLIT_VIRTUAL_CALL(_font_clear_glyphs, p_font_rid, p_size);
}

void TextSystemExtension::font_remove_glyph(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph) {
	ORBLIT_VIRTUAL_CALL(_font_remove_glyph, p_font_rid, p_size, p_glyph);
}

Vector2 TextSystemExtension::font_get_glyph_advance(const RID &p_font_rid, int64_t p_size, int64_t p_glyph) const {
	Vector2 ret;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_advance, p_font_rid, p_size, p_glyph, ret);
	return ret;
}

void TextSystemExtension::font_set_glyph_advance(const RID &p_font_rid, int64_t p_size, int64_t p_glyph, const Vector2 &p_advance) {
	ORBLIT_VIRTUAL_CALL(_font_set_glyph_advance, p_font_rid, p_size, p_glyph, p_advance);
}

Vector2 TextSystemExtension::font_get_glyph_offset(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph) const {
	Vector2 ret;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_offset, p_font_rid, p_size, p_glyph, ret);
	return ret;
}

void TextSystemExtension::font_set_glyph_offset(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph, const Vector2 &p_offset) {
	ORBLIT_VIRTUAL_CALL(_font_set_glyph_offset, p_font_rid, p_size, p_glyph, p_offset);
}

Vector2 TextSystemExtension::font_get_glyph_size(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph) const {
	Vector2 ret;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_size, p_font_rid, p_size, p_glyph, ret);
	return ret;
}

void TextSystemExtension::font_set_glyph_size(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph, const Vector2 &p_gl_size) {
	ORBLIT_VIRTUAL_CALL(_font_set_glyph_size, p_font_rid, p_size, p_glyph, p_gl_size);
}

Rect2 TextSystemExtension::font_get_glyph_uv_rect(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph) const {
	Rect2 ret;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_uv_rect, p_font_rid, p_size, p_glyph, ret);
	return ret;
}

void TextSystemExtension::font_set_glyph_uv_rect(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph, const Rect2 &p_uv_rect) {
	ORBLIT_VIRTUAL_CALL(_font_set_glyph_uv_rect, p_font_rid, p_size, p_glyph, p_uv_rect);
}

int64_t TextSystemExtension::font_get_glyph_texture_idx(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_texture_idx, p_font_rid, p_size, p_glyph, ret);
	return ret;
}

void TextSystemExtension::font_set_glyph_texture_idx(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph, int64_t p_texture_idx) {
	ORBLIT_VIRTUAL_CALL(_font_set_glyph_texture_idx, p_font_rid, p_size, p_glyph, p_texture_idx);
}

RID TextSystemExtension::font_get_glyph_texture_rid(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph) const {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_texture_rid, p_font_rid, p_size, p_glyph, ret);
	return ret;
}

Size2 TextSystemExtension::font_get_glyph_texture_size(const RID &p_font_rid, const Vector2i &p_size, int64_t p_glyph) const {
	Size2 ret;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_texture_size, p_font_rid, p_size, p_glyph, ret);
	return ret;
}

Dictionary TextSystemExtension::font_get_glyph_contours(const RID &p_font_rid, int64_t p_size, int64_t p_index) const {
	Dictionary ret;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_contours, p_font_rid, p_size, p_index, ret);
	return ret;
}

TypedArray<Vector2i> TextSystemExtension::font_get_kerning_list(const RID &p_font_rid, int64_t p_size) const {
	TypedArray<Vector2i> ret;
	ORBLIT_VIRTUAL_CALL(_font_get_kerning_list, p_font_rid, p_size, ret);
	return ret;
}

void TextSystemExtension::font_clear_kerning_map(const RID &p_font_rid, int64_t p_size) {
	ORBLIT_VIRTUAL_CALL(_font_clear_kerning_map, p_font_rid, p_size);
}

void TextSystemExtension::font_remove_kerning(const RID &p_font_rid, int64_t p_size, const Vector2i &p_glyph_pair) {
	ORBLIT_VIRTUAL_CALL(_font_remove_kerning, p_font_rid, p_size, p_glyph_pair);
}

void TextSystemExtension::font_set_kerning(const RID &p_font_rid, int64_t p_size, const Vector2i &p_glyph_pair, const Vector2 &p_kerning) {
	ORBLIT_VIRTUAL_CALL(_font_set_kerning, p_font_rid, p_size, p_glyph_pair, p_kerning);
}

Vector2 TextSystemExtension::font_get_kerning(const RID &p_font_rid, int64_t p_size, const Vector2i &p_glyph_pair) const {
	Vector2 ret;
	ORBLIT_VIRTUAL_CALL(_font_get_kerning, p_font_rid, p_size, p_glyph_pair, ret);
	return ret;
}

int64_t TextSystemExtension::font_get_glyph_index(const RID &p_font_rid, int64_t p_size, int64_t p_char, int64_t p_variation_selector) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_glyph_index, p_font_rid, p_size, p_char, p_variation_selector, ret);
	return ret;
}

int64_t TextSystemExtension::font_get_char_from_glyph_index(const RID &p_font_rid, int64_t p_size, int64_t p_glyph_index) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_font_get_char_from_glyph_index, p_font_rid, p_size, p_glyph_index, ret);
	return ret;
}

bool TextSystemExtension::font_has_char(const RID &p_font_rid, int64_t p_char) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_has_char, p_font_rid, p_char, ret);
	return ret;
}

String TextSystemExtension::font_get_supported_chars(const RID &p_font_rid) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_font_get_supported_chars, p_font_rid, ret);
	return ret;
}

PackedInt32Array TextSystemExtension::font_get_supported_glyphs(const RID &p_font_rid) const {
	PackedInt32Array ret;
	ORBLIT_VIRTUAL_CALL(_font_get_supported_glyphs, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_render_range(const RID &p_font_rid, const Vector2i &p_size, int64_t p_start, int64_t p_end) {
	ORBLIT_VIRTUAL_CALL(_font_render_range, p_font_rid, p_size, p_start, p_end);
}

void TextSystemExtension::font_render_glyph(const RID &p_font_rid, const Vector2i &p_size, int64_t p_index) {
	ORBLIT_VIRTUAL_CALL(_font_render_glyph, p_font_rid, p_size, p_index);
}

void TextSystemExtension::font_draw_glyph(const RID &p_font_rid, const RID &p_canvas, int64_t p_size, const Vector2 &p_pos, int64_t p_index, const Color &p_color, float p_oversampling) const {
	ORBLIT_VIRTUAL_CALL(_font_draw_glyph, p_font_rid, p_canvas, p_size, p_pos, p_index, p_color, p_oversampling);
}

void TextSystemExtension::font_draw_glyph_outline(const RID &p_font_rid, const RID &p_canvas, int64_t p_size, int64_t p_outline_size, const Vector2 &p_pos, int64_t p_index, const Color &p_color, float p_oversampling) const {
	ORBLIT_VIRTUAL_CALL(_font_draw_glyph_outline, p_font_rid, p_canvas, p_size, p_outline_size, p_pos, p_index, p_color, p_oversampling);
}

bool TextSystemExtension::font_is_language_supported(const RID &p_font_rid, const String &p_language) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_is_language_supported, p_font_rid, p_language, ret);
	return ret;
}

void TextSystemExtension::font_set_language_support_override(const RID &p_font_rid, const String &p_language, bool p_supported) {
	ORBLIT_VIRTUAL_CALL(_font_set_language_support_override, p_font_rid, p_language, p_supported);
}

bool TextSystemExtension::font_get_language_support_override(const RID &p_font_rid, const String &p_language) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_get_language_support_override, p_font_rid, p_language, ret);
	return ret;
}

void TextSystemExtension::font_remove_language_support_override(const RID &p_font_rid, const String &p_language) {
	ORBLIT_VIRTUAL_CALL(_font_remove_language_support_override, p_font_rid, p_language);
}

PackedStringArray TextSystemExtension::font_get_language_support_overrides(const RID &p_font_rid) {
	PackedStringArray ret;
	ORBLIT_VIRTUAL_CALL(_font_get_language_support_overrides, p_font_rid, ret);
	return ret;
}

bool TextSystemExtension::font_is_script_supported(const RID &p_font_rid, const String &p_script) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_is_script_supported, p_font_rid, p_script, ret);
	return ret;
}

void TextSystemExtension::font_set_script_support_override(const RID &p_font_rid, const String &p_script, bool p_supported) {
	ORBLIT_VIRTUAL_CALL(_font_set_script_support_override, p_font_rid, p_script, p_supported);
}

bool TextSystemExtension::font_get_script_support_override(const RID &p_font_rid, const String &p_script) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_font_get_script_support_override, p_font_rid, p_script, ret);
	return ret;
}

void TextSystemExtension::font_remove_script_support_override(const RID &p_font_rid, const String &p_script) {
	ORBLIT_VIRTUAL_CALL(_font_remove_script_support_override, p_font_rid, p_script);
}

PackedStringArray TextSystemExtension::font_get_script_support_overrides(const RID &p_font_rid) {
	PackedStringArray ret;
	ORBLIT_VIRTUAL_CALL(_font_get_script_support_overrides, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::font_set_opentype_feature_overrides(const RID &p_font_rid, const Dictionary &p_overrides) {
	ORBLIT_VIRTUAL_CALL(_font_set_opentype_feature_overrides, p_font_rid, p_overrides);
}

Dictionary TextSystemExtension::font_get_opentype_feature_overrides(const RID &p_font_rid) const {
	Dictionary ret;
	ORBLIT_VIRTUAL_CALL(_font_get_opentype_feature_overrides, p_font_rid, ret);
	return ret;
}

Dictionary TextSystemExtension::font_supported_feature_list(const RID &p_font_rid) const {
	Dictionary ret;
	ORBLIT_VIRTUAL_CALL(_font_supported_feature_list, p_font_rid, ret);
	return ret;
}

Dictionary TextSystemExtension::font_supported_variation_list(const RID &p_font_rid) const {
	Dictionary ret;
	ORBLIT_VIRTUAL_CALL(_font_supported_variation_list, p_font_rid, ret);
	return ret;
}

void TextSystemExtension::reference_oversampling_level(double p_oversampling) {
	ORBLIT_VIRTUAL_CALL(_reference_oversampling_level, p_oversampling);
}

void TextSystemExtension::unreference_oversampling_level(double p_oversampling) {
	ORBLIT_VIRTUAL_CALL(_unreference_oversampling_level, p_oversampling);
}

Vector2 TextSystemExtension::get_hex_code_box_size(int64_t p_size, int64_t p_index) const {
	Vector2 ret;
	if (ORBLIT_VIRTUAL_CALL(_get_hex_code_box_size, p_size, p_index, ret)) {
		return ret;
	}
	return TextSystem::get_hex_code_box_size(p_size, p_index);
}

void TextSystemExtension::draw_hex_code_box(const RID &p_canvas, int64_t p_size, const Vector2 &p_pos, int64_t p_index, const Color &p_color) const {
	if (!ORBLIT_VIRTUAL_CALL(_draw_hex_code_box, p_canvas, p_size, p_pos, p_index, p_color)) {
		TextSystem::draw_hex_code_box(p_canvas, p_size, p_pos, p_index, p_color);
	}
}

/*************************************************************************/
/* Shaped text buffer interface                                          */
/*************************************************************************/

RID TextSystemExtension::create_shaped_text(TextSystem::Direction p_direction, TextSystem::Orientation p_orientation) {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_create_shaped_text, p_direction, p_orientation, ret);
	return ret;
}

void TextSystemExtension::shaped_text_clear(const RID &p_shaped) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_clear, p_shaped);
}

RID TextSystemExtension::shaped_text_duplicate(const RID &p_shaped) {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_duplicate, p_shaped, ret);
	return ret;
}

void TextSystemExtension::shaped_text_set_direction(const RID &p_shaped, TextSystem::Direction p_direction) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_set_direction, p_shaped, p_direction);
}

TextSystem::Direction TextSystemExtension::shaped_text_get_direction(const RID &p_shaped) const {
	TextSystem::Direction ret = TextSystem::DIRECTION_AUTO;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_direction, p_shaped, ret);
	return ret;
}

TextSystem::Direction TextSystemExtension::shaped_text_get_inferred_direction(const RID &p_shaped) const {
	TextSystem::Direction ret = TextSystem::DIRECTION_LTR;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_inferred_direction, p_shaped, ret);
	return ret;
}

void TextSystemExtension::shaped_text_set_orientation(const RID &p_shaped, TextSystem::Orientation p_orientation) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_set_orientation, p_shaped, p_orientation);
}

TextSystem::Orientation TextSystemExtension::shaped_text_get_orientation(const RID &p_shaped) const {
	TextSystem::Orientation ret = TextSystem::ORIENTATION_HORIZONTAL;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_orientation, p_shaped, ret);
	return ret;
}

void TextSystemExtension::shaped_text_set_bidi_override(const RID &p_shaped, const Array &p_override) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_set_bidi_override, p_shaped, p_override);
}

void TextSystemExtension::shaped_text_set_custom_punctuation(const RID &p_shaped, const String &p_punct) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_set_custom_punctuation, p_shaped, p_punct);
}

String TextSystemExtension::shaped_text_get_custom_punctuation(const RID &p_shaped) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_custom_punctuation, p_shaped, ret);
	return ret;
}

void TextSystemExtension::shaped_text_set_custom_ellipsis(const RID &p_shaped, int64_t p_char) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_set_custom_ellipsis, p_shaped, p_char);
}

int64_t TextSystemExtension::shaped_text_get_custom_ellipsis(const RID &p_shaped) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_custom_ellipsis, p_shaped, ret);
	return ret;
}

void TextSystemExtension::shaped_text_set_preserve_invalid(const RID &p_shaped, bool p_enabled) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_set_preserve_invalid, p_shaped, p_enabled);
}

bool TextSystemExtension::shaped_text_get_preserve_invalid(const RID &p_shaped) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_preserve_invalid, p_shaped, ret);
	return ret;
}

void TextSystemExtension::shaped_text_set_preserve_control(const RID &p_shaped, bool p_enabled) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_set_preserve_control, p_shaped, p_enabled);
}

bool TextSystemExtension::shaped_text_get_preserve_control(const RID &p_shaped) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_preserve_control, p_shaped, ret);
	return ret;
}

void TextSystemExtension::shaped_text_set_spacing(const RID &p_shaped, TextSystem::SpacingType p_spacing, int64_t p_value) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_set_spacing, p_shaped, p_spacing, p_value);
}

int64_t TextSystemExtension::shaped_text_get_spacing(const RID &p_shaped, TextSystem::SpacingType p_spacing) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_spacing, p_shaped, p_spacing, ret);
	return ret;
}

bool TextSystemExtension::shaped_text_add_string(const RID &p_shaped, const String &p_text, const TypedArray<RID> &p_fonts, int64_t p_size, const Dictionary &p_opentype_features, const String &p_language, const Variant &p_meta) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_add_string, p_shaped, p_text, p_fonts, p_size, p_opentype_features, p_language, p_meta, ret);
	return ret;
}

bool TextSystemExtension::shaped_text_add_object(const RID &p_shaped, const Variant &p_key, const Size2 &p_size, InlineAlignment p_inline_align, int64_t p_length, double p_baseline) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_add_object, p_shaped, p_key, p_size, p_inline_align, p_length, p_baseline, ret);
	return ret;
}

bool TextSystemExtension::shaped_text_resize_object(const RID &p_shaped, const Variant &p_key, const Size2 &p_size, InlineAlignment p_inline_align, double p_baseline) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_resize_object, p_shaped, p_key, p_size, p_inline_align, p_baseline, ret);
	return ret;
}

bool TextSystemExtension::shaped_text_has_object(const RID &p_shaped, const Variant &p_key) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_has_object, p_shaped, p_key, ret);
	return ret;
}

String TextSystemExtension::shaped_get_text(const RID &p_shaped) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_text, p_shaped, ret);
	return ret;
}

int64_t TextSystemExtension::shaped_get_span_count(const RID &p_shaped) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_get_span_count, p_shaped, ret);
	return ret;
}

Variant TextSystemExtension::shaped_get_span_meta(const RID &p_shaped, int64_t p_index) const {
	Variant ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_span_meta, p_shaped, p_index, ret);
	return ret;
}

Variant TextSystemExtension::shaped_get_span_embedded_object(const RID &p_shaped, int64_t p_index) const {
	Variant ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_span_embedded_object, p_shaped, p_index, ret);
	return ret;
}

String TextSystemExtension::shaped_get_span_text(const RID &p_shaped, int64_t p_index) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_span_text, p_shaped, p_index, ret);
	return ret;
}

Variant TextSystemExtension::shaped_get_span_object(const RID &p_shaped, int64_t p_index) const {
	Variant ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_get_span_object, p_shaped, p_index, ret);
	return ret;
}

void TextSystemExtension::shaped_set_span_update_font(const RID &p_shaped, int64_t p_index, const TypedArray<RID> &p_fonts, int64_t p_size, const Dictionary &p_opentype_features) {
	ORBLIT_VIRTUAL_CALL(_shaped_set_span_update_font, p_shaped, p_index, p_fonts, p_size, p_opentype_features);
}

int64_t TextSystemExtension::shaped_get_run_count(const RID &p_shaped) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_get_run_count, p_shaped, ret);
	return ret;
}

String TextSystemExtension::shaped_get_run_text(const RID &p_shaped, int64_t p_index) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_run_text, p_shaped, p_index, ret);
	return ret;
}

Vector2i TextSystemExtension::shaped_get_run_range(const RID &p_shaped, int64_t p_index) const {
	Vector2i ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_run_range, p_shaped, p_index, ret);
	return ret;
}

Vector2i TextSystemExtension::shaped_get_run_glyph_range(const RID &p_shaped, int64_t p_index) const {
	Vector2i ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_run_glyph_range, p_shaped, p_index, ret);
	return ret;
}

RID TextSystemExtension::shaped_get_run_font_rid(const RID &p_shaped, int64_t p_index) const {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_run_font_rid, p_shaped, p_index, ret);
	return ret;
}

int TextSystemExtension::shaped_get_run_font_size(const RID &p_shaped, int64_t p_index) const {
	int ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_get_run_font_size, p_shaped, p_index, ret);
	return ret;
}

String TextSystemExtension::shaped_get_run_language(const RID &p_shaped, int64_t p_index) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_run_language, p_shaped, p_index, ret);
	return ret;
}

TextSystem::Direction TextSystemExtension::shaped_get_run_direction(const RID &p_shaped, int64_t p_index) const {
	TextSystem::Direction ret = TextSystem::DIRECTION_LTR;
	ORBLIT_VIRTUAL_CALL(_shaped_get_run_direction, p_shaped, p_index, ret);
	return ret;
}

Variant TextSystemExtension::shaped_get_run_object(const RID &p_shaped, int64_t p_index) const {
	Variant ret;
	ORBLIT_VIRTUAL_CALL(_shaped_get_run_object, p_shaped, p_index, ret);
	return ret;
}

RID TextSystemExtension::shaped_text_substr(const RID &p_shaped, int64_t p_start, int64_t p_length) const {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_substr, p_shaped, p_start, p_length, ret);
	return ret;
}

RID TextSystemExtension::shaped_text_get_parent(const RID &p_shaped) const {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_parent, p_shaped, ret);
	return ret;
}

double TextSystemExtension::shaped_text_fit_to_width(const RID &p_shaped, double p_width, BitField<TextSystem::JustificationFlag> p_jst_flags) {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_fit_to_width, p_shaped, p_width, p_jst_flags, ret);
	return ret;
}

double TextSystemExtension::shaped_text_tab_align(const RID &p_shaped, const PackedFloat32Array &p_tab_stops) {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_tab_align, p_shaped, p_tab_stops, ret);
	return ret;
}

bool TextSystemExtension::shaped_text_shape(const RID &p_shaped) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_shape, p_shaped, ret);
	return ret;
}

bool TextSystemExtension::shaped_text_update_breaks(const RID &p_shaped) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_update_breaks, p_shaped, ret);
	return ret;
}

bool TextSystemExtension::shaped_text_update_justification_ops(const RID &p_shaped) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_update_justification_ops, p_shaped, ret);
	return ret;
}

bool TextSystemExtension::shaped_text_is_ready(const RID &p_shaped) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_shaped_text_is_ready, p_shaped, ret);
	return ret;
}

const Glyph *TextSystemExtension::shaped_text_get_glyphs(const RID &p_shaped) const {
	NativeExtensionPtr<const Glyph> ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_glyphs, p_shaped, ret);
	return ret;
}

const Glyph *TextSystemExtension::shaped_text_sort_logical(const RID &p_shaped) {
	NativeExtensionPtr<const Glyph> ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_sort_logical, p_shaped, ret);
	return ret;
}

int64_t TextSystemExtension::shaped_text_get_glyph_count(const RID &p_shaped) const {
	int64_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_glyph_count, p_shaped, ret);
	return ret;
}

Vector2i TextSystemExtension::shaped_text_get_range(const RID &p_shaped) const {
	Vector2i ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_range, p_shaped, ret);
	return ret;
}

PackedInt32Array TextSystemExtension::shaped_text_get_line_breaks_adv(const RID &p_shaped, const PackedFloat32Array &p_width, int64_t p_start, bool p_once, BitField<TextSystem::LineBreakFlag> p_break_flags) const {
	PackedInt32Array ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_get_line_breaks_adv, p_shaped, p_width, p_start, p_once, p_break_flags, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_get_line_breaks_adv(p_shaped, p_width, p_start, p_once, p_break_flags);
}

PackedInt32Array TextSystemExtension::shaped_text_get_line_breaks(const RID &p_shaped, double p_width, int64_t p_start, BitField<TextSystem::LineBreakFlag> p_break_flags) const {
	PackedInt32Array ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_get_line_breaks, p_shaped, p_width, p_start, p_break_flags, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_get_line_breaks(p_shaped, p_width, p_start, p_break_flags);
}

PackedInt32Array TextSystemExtension::shaped_text_get_word_breaks(const RID &p_shaped, BitField<TextSystem::GraphemeFlag> p_grapheme_flags, BitField<TextSystem::GraphemeFlag> p_skip_grapheme_flags) const {
	PackedInt32Array ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_get_word_breaks, p_shaped, p_grapheme_flags, p_skip_grapheme_flags, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_get_word_breaks(p_shaped, p_grapheme_flags, p_skip_grapheme_flags);
}

int64_t TextSystemExtension::shaped_text_get_trim_pos(const RID &p_shaped) const {
	int64_t ret = -1;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_trim_pos, p_shaped, ret);
	return ret;
}

int64_t TextSystemExtension::shaped_text_get_ellipsis_pos(const RID &p_shaped) const {
	int64_t ret = -1;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_ellipsis_pos, p_shaped, ret);
	return ret;
}

const Glyph *TextSystemExtension::shaped_text_get_ellipsis_glyphs(const RID &p_shaped) const {
	NativeExtensionPtr<const Glyph> ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_ellipsis_glyphs, p_shaped, ret);
	return ret;
}

int64_t TextSystemExtension::shaped_text_get_ellipsis_glyph_count(const RID &p_shaped) const {
	int64_t ret = -1;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_ellipsis_glyph_count, p_shaped, ret);
	return ret;
}

void TextSystemExtension::shaped_text_overrun_trim_to_width(const RID &p_shaped_line, double p_width, BitField<TextSystem::TextOverrunFlag> p_trim_flags) {
	ORBLIT_VIRTUAL_CALL(_shaped_text_overrun_trim_to_width, p_shaped_line, p_width, p_trim_flags);
}

Array TextSystemExtension::shaped_text_get_objects(const RID &p_shaped) const {
	Array ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_objects, p_shaped, ret);
	return ret;
}

Rect2 TextSystemExtension::shaped_text_get_object_rect(const RID &p_shaped, const Variant &p_key) const {
	Rect2 ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_object_rect, p_shaped, p_key, ret);
	return ret;
}

Vector2i TextSystemExtension::shaped_text_get_object_range(const RID &p_shaped, const Variant &p_key) const {
	Vector2i ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_object_range, p_shaped, p_key, ret);
	return ret;
}

int64_t TextSystemExtension::shaped_text_get_object_glyph(const RID &p_shaped, const Variant &p_key) const {
	int64_t ret = -1;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_object_glyph, p_shaped, p_key, ret);
	return ret;
}

Size2 TextSystemExtension::shaped_text_get_size(const RID &p_shaped) const {
	Size2 ret;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_size, p_shaped, ret);
	return ret;
}

double TextSystemExtension::shaped_text_get_ascent(const RID &p_shaped) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_ascent, p_shaped, ret);
	return ret;
}

double TextSystemExtension::shaped_text_get_descent(const RID &p_shaped) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_descent, p_shaped, ret);
	return ret;
}

double TextSystemExtension::shaped_text_get_width(const RID &p_shaped) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_width, p_shaped, ret);
	return ret;
}

double TextSystemExtension::shaped_text_get_underline_position(const RID &p_shaped) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_underline_position, p_shaped, ret);
	return ret;
}

double TextSystemExtension::shaped_text_get_underline_thickness(const RID &p_shaped) const {
	double ret = 0;
	ORBLIT_VIRTUAL_CALL(_shaped_text_get_underline_thickness, p_shaped, ret);
	return ret;
}

TextSystem::Direction TextSystemExtension::shaped_text_get_dominant_direction_in_range(const RID &p_shaped, int64_t p_start, int64_t p_end) const {
	int64_t ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_get_dominant_direction_in_range, p_shaped, p_start, p_end, ret)) {
		return (TextSystem::Direction)ret;
	}
	return TextSystem::shaped_text_get_dominant_direction_in_range(p_shaped, p_start, p_end);
}

CaretInfo TextSystemExtension::shaped_text_get_carets(const RID &p_shaped, int64_t p_position) const {
	CaretInfo ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_get_carets, p_shaped, p_position, &ret)) {
		return ret;
	}
	return TextSystem::shaped_text_get_carets(p_shaped, p_position);
}

Vector<Vector2> TextSystemExtension::shaped_text_get_selection(const RID &p_shaped, int64_t p_start, int64_t p_end) const {
	Vector<Vector2> ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_get_selection, p_shaped, p_start, p_end, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_get_selection(p_shaped, p_start, p_end);
}

int64_t TextSystemExtension::shaped_text_hit_test_grapheme(const RID &p_shaped, double p_coords) const {
	int64_t ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_hit_test_grapheme, p_shaped, p_coords, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_hit_test_grapheme(p_shaped, p_coords);
}

int64_t TextSystemExtension::shaped_text_hit_test_position(const RID &p_shaped, double p_coords) const {
	int64_t ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_hit_test_position, p_shaped, p_coords, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_hit_test_position(p_shaped, p_coords);
}

void TextSystemExtension::shaped_text_draw(const RID &p_shaped, const RID &p_canvas, const Vector2 &p_pos, double p_clip_l, double p_clip_r, const Color &p_color, float p_oversampling) const {
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_draw, p_shaped, p_canvas, p_pos, p_clip_l, p_clip_r, p_color, p_oversampling)) {
		return;
	}
	TextSystem::shaped_text_draw(p_shaped, p_canvas, p_pos, p_clip_l, p_clip_r, p_color, p_oversampling);
}

void TextSystemExtension::shaped_text_draw_outline(const RID &p_shaped, const RID &p_canvas, const Vector2 &p_pos, double p_clip_l, double p_clip_r, int64_t p_outline_size, const Color &p_color, float p_oversampling) const {
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_draw_outline, p_shaped, p_canvas, p_pos, p_clip_l, p_clip_r, p_outline_size, p_color, p_oversampling)) {
		return;
	}
	TextSystem::shaped_text_draw_outline(p_shaped, p_canvas, p_pos, p_clip_l, p_clip_r, p_outline_size, p_color, p_oversampling);
}

Vector2 TextSystemExtension::shaped_text_get_grapheme_bounds(const RID &p_shaped, int64_t p_pos) const {
	Vector2 ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_get_grapheme_bounds, p_shaped, p_pos, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_get_grapheme_bounds(p_shaped, p_pos);
}

int64_t TextSystemExtension::shaped_text_next_grapheme_pos(const RID &p_shaped, int64_t p_pos) const {
	int64_t ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_next_grapheme_pos, p_shaped, p_pos, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_next_grapheme_pos(p_shaped, p_pos);
}

int64_t TextSystemExtension::shaped_text_prev_grapheme_pos(const RID &p_shaped, int64_t p_pos) const {
	int64_t ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_prev_grapheme_pos, p_shaped, p_pos, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_prev_grapheme_pos(p_shaped, p_pos);
}

PackedInt32Array TextSystemExtension::shaped_text_get_character_breaks(const RID &p_shaped) const {
	PackedInt32Array ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_get_character_breaks, p_shaped, ret)) {
		return ret;
	}
	return PackedInt32Array();
}

int64_t TextSystemExtension::shaped_text_next_character_pos(const RID &p_shaped, int64_t p_pos) const {
	int64_t ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_next_character_pos, p_shaped, p_pos, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_next_character_pos(p_shaped, p_pos);
}

int64_t TextSystemExtension::shaped_text_prev_character_pos(const RID &p_shaped, int64_t p_pos) const {
	int64_t ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_prev_character_pos, p_shaped, p_pos, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_prev_character_pos(p_shaped, p_pos);
}

int64_t TextSystemExtension::shaped_text_closest_character_pos(const RID &p_shaped, int64_t p_pos) const {
	int64_t ret;
	if (ORBLIT_VIRTUAL_CALL(_shaped_text_closest_character_pos, p_shaped, p_pos, ret)) {
		return ret;
	}
	return TextSystem::shaped_text_closest_character_pos(p_shaped, p_pos);
}

bool TextSystemExtension::is_valid_identifier(const String &p_string) const {
	bool ret;
	if (ORBLIT_VIRTUAL_CALL(_is_valid_identifier, p_string, ret)) {
		return ret;
	}
	return TextSystem::is_valid_identifier(p_string);
}

bool TextSystemExtension::is_valid_letter(uint64_t p_unicode) const {
	bool ret;
	if (ORBLIT_VIRTUAL_CALL(_is_valid_letter, p_unicode, ret)) {
		return ret;
	}
	return TextSystem::is_valid_letter(p_unicode);
}

String TextSystemExtension::strip_diacritics(const String &p_string) const {
	String ret;
	if (ORBLIT_VIRTUAL_CALL(_strip_diacritics, p_string, ret)) {
		return ret;
	}
	return TextSystem::strip_diacritics(p_string);
}

String TextSystemExtension::string_to_upper(const String &p_string, const String &p_language) const {
	String ret;
	if (ORBLIT_VIRTUAL_CALL(_string_to_upper, p_string, p_language, ret)) {
		return ret;
	}
	return p_string;
}

String TextSystemExtension::string_to_title(const String &p_string, const String &p_language) const {
	String ret;
	if (ORBLIT_VIRTUAL_CALL(_string_to_title, p_string, p_language, ret)) {
		return ret;
	}
	return p_string;
}

String TextSystemExtension::string_to_lower(const String &p_string, const String &p_language) const {
	String ret;
	if (ORBLIT_VIRTUAL_CALL(_string_to_lower, p_string, p_language, ret)) {
		return ret;
	}
	return p_string;
}

TypedArray<Vector3i> TextSystemExtension::parse_structured_text(StructuredTextParser p_parser_type, const Array &p_args, const String &p_text) const {
	TypedArray<Vector3i> ret;
	if (ORBLIT_VIRTUAL_CALL(_parse_structured_text, p_parser_type, p_args, p_text, ret)) {
		return ret;
	}
	return TextSystem::parse_structured_text(p_parser_type, p_args, p_text);
}

PackedInt32Array TextSystemExtension::string_get_word_breaks(const String &p_string, const String &p_language, int64_t p_chars_per_line) const {
	PackedInt32Array ret;
	ORBLIT_VIRTUAL_CALL(_string_get_word_breaks, p_string, p_language, p_chars_per_line, ret);
	return ret;
}

PackedInt32Array TextSystemExtension::string_get_character_breaks(const String &p_string, const String &p_language) const {
	PackedInt32Array ret;
	if (ORBLIT_VIRTUAL_CALL(_string_get_character_breaks, p_string, p_language, ret)) {
		return ret;
	}
	return TextSystem::string_get_character_breaks(p_string, p_language);
}

int64_t TextSystemExtension::is_confusable(const String &p_string, const PackedStringArray &p_dict) const {
	int64_t ret;
	if (ORBLIT_VIRTUAL_CALL(_is_confusable, p_string, p_dict, ret)) {
		return ret;
	}
	return TextSystem::is_confusable(p_string, p_dict);
}

bool TextSystemExtension::spoof_check(const String &p_string) const {
	bool ret;
	if (ORBLIT_VIRTUAL_CALL(_spoof_check, p_string, ret)) {
		return ret;
	}
	return TextSystem::spoof_check(p_string);
}

void TextSystemExtension::cleanup() {
	ORBLIT_VIRTUAL_CALL(_cleanup);
}

TextSystemExtension::TextSystemExtension() {
	//NOP
}

TextSystemExtension::~TextSystemExtension() {
	//NOP
}
