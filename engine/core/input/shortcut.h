/**************************************************************************/
/*  shortcut.h                                                            */

#pragma once

#include "core/input/input_event.h"
#include "core/io/resource.h"

class Shortcut : public Resource {
	ORBLIT_CLASS(Shortcut, Resource);

	Array events;

protected:
	static void register_members();

public:
	void set_events(const Array &p_events);
	Array get_events() const;

	void set_events_list(const List<Ref<InputEvent>> *p_events);

	bool matches_event(const Ref<InputEvent> &p_event) const;
	bool has_valid_event() const;

	String get_as_text() const;

	static Ref<Shortcut> make_from_action(const StringName &p_action);

	static bool is_event_array_equal(const Array &p_event_array1, const Array &p_event_array2);
};
