/**************************************************************************/
/*  debug_adapter_server.h                                                */

#pragma once

#include "editor/debugger/debug_adapter/debug_adapter_protocol.h"
#include "editor/plugins/editor_plugin.h"

class DebugAdapterServer : public EditorPlugin {
	ORBLIT_CLASS(DebugAdapterServer, EditorPlugin);

	DebugAdapterProtocol protocol;

	int remote_port = 6006;
	bool started = false;
	bool polling = false;
	static void thread_func(void *p_userdata);

private:
	void _notification(int p_what);

public:
	static int port_override;
	DebugAdapterServer();
	void start();
	void stop();
};
