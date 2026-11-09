/**************************************************************************/
/*  editor_event_search_bar.cpp                                           */

#include "editor_event_search_bar.h"

#include "core/object/callable_mp.h"
#include "editor/settings/event_listener_line_edit.h"
#include "scene/ui/button.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/line_edit.h"

void EditorEventSearchBar::_on_event_changed(const Ref<InputEvent> &p_event) {
	if (p_event.is_valid() && (!p_event->is_pressed() || p_event->is_echo())) {
		return;
	}
	_value_changed();
}

void EditorEventSearchBar::_on_clear_all() {
	search_by_name->set_block_signals(true);
	search_by_name->clear();
	search_by_name->set_block_signals(false);

	search_by_event->set_block_signals(true);
	search_by_event->clear_event();
	search_by_event->set_block_signals(false);

	_value_changed();
}

void EditorEventSearchBar::_value_changed() {
	clear_all->set_disabled(!is_searching());
	emit_signal(SceneStringName(value_changed));
}

bool EditorEventSearchBar::is_searching() const {
	return !get_name().is_empty() || get_event().is_valid();
}

String EditorEventSearchBar::get_name() const {
	return search_by_name->get_text().strip_edges();
}

Ref<InputEvent> EditorEventSearchBar::get_event() const {
	return search_by_event->get_event();
}

void EditorEventSearchBar::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_THEME_CHANGED: {
			search_by_name->set_right_icon(get_editor_theme_icon(SNAME("Search")));
		} break;
	}
}

void EditorEventSearchBar::register_members() {
	ADD_SIGNAL(MethodInfo("value_changed"));
}

EditorEventSearchBar::EditorEventSearchBar() {
	set_h_size_flags(Control::SIZE_EXPAND_FILL);

	search_by_name = memnew(LineEdit);
	search_by_name->set_h_size_flags(Control::SIZE_EXPAND_FILL);
	search_by_name->set_placeholder(TTRC("Filter by Name"));
	search_by_name->set_accessibility_name(TTRC("Filter by Name"));
	search_by_name->set_clear_button_enabled(true);
	search_by_name->connect(SceneStringName(text_changed), callable_mp(this, &EditorEventSearchBar::_value_changed).unbind(1));
	add_child(search_by_name);

	search_by_event = memnew(EventListenerLineEdit);
	search_by_event->set_h_size_flags(Control::SIZE_EXPAND_FILL);
	search_by_event->set_stretch_ratio(0.75);
	search_by_event->set_accessibility_name(TTRC("Action Event"));
	search_by_event->connect("event_changed", callable_mp(this, &EditorEventSearchBar::_on_event_changed));
	add_child(search_by_event);

	clear_all = memnew(Button(TTRC("Clear All")));
	clear_all->set_tooltip_text(TTRC("Clear all search filters."));
	clear_all->connect(SceneStringName(pressed), callable_mp(this, &EditorEventSearchBar::_on_clear_all));
	clear_all->set_disabled(true);
	add_child(clear_all);
}
