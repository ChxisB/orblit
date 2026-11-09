/**************************************************************************/
/*  editor_toolbar_group.cpp                                              */

#include "editor/widgets/editor_toolbar_group.h"

#include "editor/settings/editor_settings.h"
#include "editor/themes/editor_scale.h"
#include "scene/ui/box_container.h"

EditorToolbarGroup::EditorToolbarGroup() {
	set_theme_type_variation("PanelContainerButtonGroup");
	set_v_size_flags(Control::SizeFlags::SIZE_SHRINK_CENTER);

	hbox = memnew(HBoxContainer);
	hbox->set_anchors_and_offsets_preset(Control::PRESET_FULL_RECT);
	add_child(hbox);
}

HBoxContainer *EditorToolbarGroup::create(Control *p_parent) {
	EditorToolbarGroup *group = memnew(EditorToolbarGroup);
	p_parent->add_child(group);

	return group->get_hbox();
}

void EditorToolbarGroup::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_THEME_CHANGED: {
			// 32 roughly matches the height of the toolbars prior to the toolbar redesign.
			// The default font size of the editor is 14,
			// and the default base spacing * 2 is 8,
			// thus adding 10 will give us the minimum of 32.
			// which allows increased font sizes to scale the toolbar.
			const int font_size = get_theme_default_font_size();
			int padding = int(EDITOR_GET("interface/theme/base_spacing")) * 2;
			const int max_size = MAX(32 * EDSCALE, font_size + (padding + 10) * EDSCALE);
			set_custom_minimum_size(Size2(-1, max_size));
			set_custom_maximum_size(Size2(-1, max_size));
		} break;
	}
}
