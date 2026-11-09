/**************************************************************************/
/*  debugger_helpers.cpp                                                  */

#ifdef DEBUG_ENABLED
#include "debugger_helpers.h"

#include "core/input/input.h"
#include "core/input/shortcut.h"
#include "scene/stage/entity.h"

bool DebuggerHelpers::is_shortcut_pressed(const int p_idx, const HashMap<int, Ref<Shortcut>> &p_shortcuts, bool p_true_if_empty) {
	ERR_FAIL_COND_V(!p_shortcuts.has(p_idx), p_true_if_empty);
	if (is_shortcut_empty(p_idx, p_shortcuts)) {
		return p_true_if_empty;
	}

	Ref<Shortcut> shortcut = p_shortcuts[p_idx];
	for (Ref<InputEventKey> k : shortcut->get_events()) {
		if (k.is_valid() && (Input::get_singleton()->is_key_pressed(k->get_keycode()) || Input::get_singleton()->is_physical_key_pressed(k->get_physical_keycode()))) {
			return true;
		}
	}

	return false;
}

bool DebuggerHelpers::SelectResult::operator<(const SelectResult &p_rr) const {
	if (order != p_rr.order) {
		return p_rr.order < order;
	}

	if (item && p_rr.item) {
		return item->is_greater_than(p_rr.item);
	}

	if (item || p_rr.item) {
		return item != nullptr;
	}

	return false;
}

bool DebuggerHelpers::is_shortcut_empty(const int p_idx, const HashMap<int, Ref<Shortcut>> &p_shortcuts) {
	ERR_FAIL_COND_V(!p_shortcuts.has(p_idx), true);
	Ref<Shortcut> shortcut = p_shortcuts[p_idx];
	return shortcut.is_null() || shortcut->get_events().is_empty();
}

#endif // DEBUG_ENABLED
