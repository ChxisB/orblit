/**************************************************************************/
/*  debugger_helpers.h                                                    */

#pragma once

#ifdef DEBUG_ENABLED
#include "core/input/input_event.h"

class Entity;

class DebuggerHelpers {
public:
	struct SelectResult {
		Entity *item = nullptr;
		int order = 0;
		bool operator<(const SelectResult &p_rr) const;
	};

	static bool is_shortcut_pressed(const int p_idx, const HashMap<int, Ref<Shortcut>> &p_shortcuts, bool p_true_if_empty = false);
	static bool is_shortcut_empty(const int p_idx, const HashMap<int, Ref<Shortcut>> &p_shortcuts);
};
#endif // DEBUG_ENABLED
