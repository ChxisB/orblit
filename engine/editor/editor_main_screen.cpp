/**************************************************************************/
/*  editor_main_screen.cpp                                                */

#include "editor_main_screen.h"

#include "core/object/callable_mp.h"
#include "editor/docks/editor_dock.h"
#include "editor/docks/editor_dock_manager.h"
#include "editor/editor_node.h"
#include "editor/plugins/editor_plugin.h"
#include "editor/scene/spatial/entity_spatial_editor_plugin.h"
#include "editor/script/script_editor_plugin.h"
#include "editor/settings/editor_settings.h"
#include "editor/themes/editor_scale.h"

void EditorMainScreen::_on_tab_changed(int p_tab) {
	EditorNode::get_singleton()->update_distraction_free_mode();

	EditorDock *dock = get_dock(p_tab);
	if (!dock) {
		return;
	}
	const String new_main_screen = dock->get_display_title();

	EditorData &editor_data = EditorNode::get_editor_data();
	int plugin_count = editor_data.get_editor_plugin_count();
	for (int i = 0; i < plugin_count; i++) {
		editor_data.get_editor_plugin(i)->notify_main_screen_changed(new_main_screen);
	}
}

void EditorMainScreen::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_POSTINITIALIZE: {
			set_tab_alignment(TabBar::ALIGNMENT_CENTER);
			get_internal_container()->set_alignment(BoxContainer::ALIGNMENT_CENTER);
			connect("tab_changed", callable_mp(this, &EditorMainScreen::_on_tab_changed));
		} break;

		case NOTIFICATION_START: {
			set_popup(nullptr);

			for (int i = 0; i < get_tab_count(); i++) {
				EditorDock *dock = get_dock(i);
				if (dock == Entity3DEditor::get_singleton()) {
					dock->make_visible();
					break;
				}
			}
		} break;
	}
}

DockTabContainer::TabStyle EditorMainScreen::get_tab_style() const {
	return (TabStyle)EDITOR_GET("interface/editor/docks/main_screen_dock_tab_style").operator int();
}

Rect2 EditorMainScreen::get_drag_hint_rect() const {
	const Rect2 tab_rect = get_internal_container()->get_global_rect();
	const Rect2 content_rect = get_global_rect();
	Rect2 final_rect = tab_rect.merge(content_rect);
	if (get_tab_count() > 0) {
		float tab_x = get_tab_bar()->get_global_transform().xform(get_tab_bar()->get_tab_rect(0).position).x;
		final_rect.position.x = MIN(tab_x, content_rect.position.x);
		tab_x = get_tab_bar()->get_global_transform().xform(get_tab_bar()->get_tab_rect(-1).get_end()).x;
		final_rect.set_end(Vector2(MAX(tab_x, content_rect.get_end().x), final_rect.get_end().y));
	}
	return final_rect;
}

bool EditorMainScreen::can_switch_dock() const {
	if (get_current_tab() == -1) {
		return true;
	}
	EditorDock *dock = get_dock(get_current_tab());
	return dock->is_allow_switch_screen();
}

void EditorMainScreen::select_next() {
	if (get_tab_count() == 0) {
		return;
	}
	if (get_current_tab() < get_tab_count() - 1) {
		set_current_tab(get_current_tab() + 1);
	} else {
		set_current_tab(0);
	}
}

void EditorMainScreen::select_prev() {
	if (get_tab_count() == 0) {
		return;
	}
	if (get_current_tab() > 0) {
		set_current_tab(get_current_tab() - 1);
	} else {
		set_current_tab(get_tab_count() - 1);
	}
}

EditorMainScreen::EditorMainScreen() :
		DockTabContainer(EditorDock::DOCK_SLOT_MAIN_SCREEN) {
	layout = EditorDock::DOCK_LAYOUT_MAIN_SCREEN;
	grid_rect = Rect2i(2, 0, 4, 4);

	set_theme_type_variation("MainScreenContainer");
	set_custom_minimum_size(Size2(0, 80) * EDSCALE);
	set_v_size_flags(Control::SIZE_EXPAND_FILL);
	set_draw_behind_parent(true);
	get_tab_bar()->set_mouse_filter(Control::MOUSE_FILTER_PASS);

	Ref<StyleBoxEmpty> sb;
	sb.instantiate();
	add_theme_style_override(SceneStringName(panel), sb);
}
