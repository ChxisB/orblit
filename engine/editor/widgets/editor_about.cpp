/**************************************************************************/
/*  editor_about.cpp                                                      */

#include "editor_about.h"

#include "core/license.gen.h"
#include "editor/editor_node.h"
#include "editor/editor_string_names.h"
#include "editor/themes/editor_scale.h"
#include "editor/widgets/editor_version_button.h"
#include "scene/ui/rich_text_label.h"
#include "scene/ui/tab_container.h"
#include "scene/ui/texture_rect.h"

void EditorAbout::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_THEME_CHANGED: {
			const Ref<Font> font = get_theme_font(SNAME("source"), EditorStringName(EditorFonts));
			const int font_size = get_theme_font_size(SNAME("source_size"), EditorStringName(EditorFonts));

			license_text_label->begin_bulk_theme_override();
			license_text_label->add_theme_font_override("normal_font", font);
			license_text_label->add_theme_font_size_override("normal_font_size", font_size);
			license_text_label->add_theme_constant_override(SceneStringName(line_separation), 4 * EDSCALE);
			license_text_label->end_bulk_theme_override();

			_logo->set_texture(get_editor_theme_icon(SNAME("Logo")));
		} break;
	}
}

EditorAbout::EditorAbout() {
	set_title(TTRC("Thanks from the Orblit community!"));
	set_hide_on_ok(true);

	VBoxContainer *vbc = memnew(VBoxContainer);
	add_child(vbc);

	HBoxContainer *hbc = memnew(HBoxContainer);
	hbc->set_h_size_flags(Control::SIZE_EXPAND_FILL);
	hbc->set_alignment(BoxContainer::ALIGNMENT_CENTER);
	hbc->add_theme_constant_override("separation", 30 * EDSCALE);
	vbc->add_child(hbc);

	_logo = memnew(TextureRect);
	_logo->set_stretch_mode(TextureRect::STRETCH_KEEP_ASPECT_CENTERED);
	hbc->add_child(_logo);

	VBoxContainer *version_info_vbc = memnew(VBoxContainer);

	// Add a dummy control node for spacing.
	Control *v_spacer = memnew(Control);
	version_info_vbc->add_child(v_spacer);

	version_info_vbc->add_child(memnew(EditorVersionButton(EditorVersionButton::FORMAT_WITH_NAME_AND_BUILD)));

	_about_text_label = memnew(Label);
	_about_text_label->set_focus_mode(Control::FOCUS_ACCESSIBILITY);
	_about_text_label->set_v_size_flags(Control::SIZE_SHRINK_CENTER);
	_about_text_label->set_auto_translate_mode(AUTO_TRANSLATE_MODE_DISABLED);
	_about_text_label->set_text(String(U"© 2026-present Orblit Engine contributors."));
	version_info_vbc->add_child(_about_text_label);

	hbc->add_child(version_info_vbc);

	TabContainer *tc = memnew(TabContainer);
	tc->set_tab_alignment(TabBar::ALIGNMENT_CENTER);
	tc->set_custom_minimum_size(Size2(400, 200) * EDSCALE);
	tc->set_v_size_flags(Control::SIZE_EXPAND_FILL);
	tc->set_theme_type_variation("TabContainerOdd");
	vbc->add_child(tc);

	// License.

	license_text_label = memnew(RichTextLabel);
	license_text_label->set_auto_translate_mode(AUTO_TRANSLATE_MODE_DISABLED);
	license_text_label->set_threaded(true);
	license_text_label->set_name(TTRC("License"));
	license_text_label->set_h_size_flags(Control::SIZE_EXPAND_FILL);
	license_text_label->set_v_size_flags(Control::SIZE_EXPAND_FILL);
	license_text_label->set_text(String::utf8(ORBLIT_LICENSE_TEXT));
	tc->add_child(license_text_label);
}
