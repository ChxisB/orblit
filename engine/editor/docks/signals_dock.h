/**************************************************************************/
/*  signals_dock.h                                                        */

#pragma once

#include "editor/docks/editor_dock.h"

class ConnectionsDock;

class SignalsDock : public EditorDock {
	ORBLIT_CLASS(SignalsDock, EditorDock);

	ConnectionsDock *connections = nullptr;

	static inline SignalsDock *singleton = nullptr;

public:
	static SignalsDock *get_singleton() { return singleton; }

	void set_object(Object *p_object);

	void update_lists();

	SignalsDock();
	~SignalsDock();
};
