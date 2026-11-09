/**************************************************************************/
/*  project_tag.cpp                                                       */

#include "project_tag.h"

#include "editor/themes/editor_scale.h"
#include "scene/ui/button.h"
#include "scene/ui/color_rect.h"

void ProjectTag::_notification(int p_what) {
	if (display_close && p_what == NOTIFICATION_THEME_CHANGED) {
		button->set_button_icon(get_theme_icon(SNAME("close"), SNAME("TabBar")));
	}
	// HACK: Can't be set in constructor because `get_size()` would return empty.
	// This logic should be migrated once `Button` utilizes internal labels.
	if (p_what == NOTIFICATION_START) {
		button->set_custom_minimum_size(button->get_size());
		button->set_text_overrun_behavior(TextSystem::OverrunBehavior::OVERRUN_TRIM_ELLIPSIS);
	}
}

void ProjectTag::connect_button_to(const Callable &p_callable) {
	button->connect(SceneStringName(pressed), p_callable, CONNECT_DEFERRED);
}

const String ProjectTag::get_tag() const {
	return tag_string;
}

ProjectTag::ProjectTag(const String &p_text, bool p_display_close) {
	add_theme_constant_override(SNAME("separation"), 0);
	set_v_size_flags(SIZE_SHRINK_CENTER);
	set_custom_maximum_size(Vector2(336 * EDSCALE, -1));
	tag_string = p_text;
	display_close = p_display_close;

	Color tag_color = Color(1, 0, 0);
	tag_color.set_ok_hsl_s(0.8);
	tag_color.set_ok_hsl_h(float(p_text.hash() * 10001 % UINT32_MAX) / float(UINT32_MAX));
	set_self_modulate(tag_color);

	ColorRect *cr = memnew(ColorRect);
	add_child(cr);
	cr->set_custom_minimum_size(Vector2(4, 0) * EDSCALE);
	cr->set_color(tag_color);
	cr->set_mouse_filter(MOUSE_FILTER_PASS);

	button = memnew(Button);
	add_child(button);
	button->set_auto_translate_mode(AUTO_TRANSLATE_MODE_DISABLED);
	button->set_text(p_text.capitalize());
	button->set_tooltip_text(p_text.capitalize());
	button->set_focus_mode(FOCUS_ACCESSIBILITY);
	button->set_accessibility_name(vformat(TTR("Project Tag: %s"), p_text));
	button->set_icon_alignment(HORIZONTAL_ALIGNMENT_RIGHT);
	button->set_theme_type_variation(SNAME("ProjectTagButton"));
	button->set_mouse_filter(MOUSE_FILTER_PASS);
}
