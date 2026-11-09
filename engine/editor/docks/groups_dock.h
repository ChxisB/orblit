/**************************************************************************/
/*  groups_dock.h                                                         */

#pragma once

#include "editor/docks/editor_dock.h"
#include "editor/docks/groups_editor.h"

class GroupsDock : public EditorDock {
	ORBLIT_CLASS(GroupsDock, EditorDock);

	GroupsEditor *groups = nullptr;

	static inline GroupsDock *singleton = nullptr;

public:
	static GroupsDock *get_singleton() { return singleton; }

	void set_selection(const Vector<Entity *> &p_nodes);

	GroupsDock();
	~GroupsDock();
};
