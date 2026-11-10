/**************************************************************************/
/*  groups_dock.cpp                                                       */

#include "groups_dock.h"

#include "editor/settings/editor_command_palette.h"

void GroupsDock::set_selection(const Vector<Entity *> &p_nodes) {
	groups->set_selection(p_nodes);
}

GroupsDock::GroupsDock() {
	singleton = this;
	set_name(TTRC("Groups"));
	set_icon_name("Groups");
	set_dock_shortcut(ED_SHORTCUT_AND_COMMAND("docks/open_groups", TTRC("Open Groups Dock")));
	set_default_slot(EditorDock::DOCK_SLOT_RIGHT_UL);

	groups = memnew(GroupsEditor);
	groups->set_v_size_flags(SIZE_EXPAND_FILL);
	add_child(groups);
}

GroupsDock::~GroupsDock() {
	singleton = nullptr;
}
