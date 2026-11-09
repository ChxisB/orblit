/**************************************************************************/
/*  signals_dock.cpp                                                      */

#include "signals_dock.h"

#include "editor/scene/connections_dialog.h"
#include "editor/settings/editor_command_palette.h"

void SignalsDock::update_lists() {
	connections->update_tree();
}

void SignalsDock::set_object(Object *p_object) {
	connections->set_object(p_object);
}

SignalsDock::SignalsDock() {
	singleton = this;
	set_name(TTRC("Signals"));
	set_icon_name("Signals");
	set_dock_shortcut(ED_SHORTCUT_AND_COMMAND("docks/open_signals", TTRC("Open Signals Dock")));
	set_default_slot(EditorDock::DOCK_SLOT_RIGHT_UL);

	connections = memnew(ConnectionsDock);
	connections->set_v_size_flags(SIZE_EXPAND_FILL);
	add_child(connections);
}

SignalsDock::~SignalsDock() {
	singleton = nullptr;
}
