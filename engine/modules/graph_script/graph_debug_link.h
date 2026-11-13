/**************************************************************************/
/*  graph_debug_link.h                                                    */

#pragma once

#include "core/debugger/remote_debugger_peer.h"
#include "core/io/tcp_server.h"
#include "core/object/ref_counted.h"
#include "core/variant/dictionary.h"

// The editor's end of the debug channel to a running game. The game connects out to it, as it does
// to any editor, when it starts with --remote-debug tcp://127.0.0.1:<port>. The link keeps what the
// game last said as one state and sends the game commands. It listens on the loopback address only.
//
// The editor calls poll often. A poll takes in what the game said since the last one.
class GraphDebugLink : public RefCounted {
	ORBLIT_CLASS(GraphDebugLink, RefCounted);

	Ref<TCPServer> server;
	Ref<RemoteDebuggerPeerTCP> peer;
	// The thread that waits at the stop. Commands go to it, and to the main thread when none waits.
	int64_t thread = Thread::MAIN_ID;
	Dictionary state;

	void _reset_state();
	void _accept(const Ref<StreamPeerSocket> &p_socket);
	void _send(const String &p_command, const Array &p_data);
	void _handle(const Array &p_message);
	void _on_enter(const Array &p_data);
	void _on_stack_dump(const Array &p_data);
	void _on_variable(const Array &p_data);
	void _on_wire(const Array &p_data);

protected:
	static void register_members();

public:
	// Starts listening on the loopback address. Port 0 lets the system pick one, and get_port says which.
	Error listen(int p_port);
	// The port the game connects to, or 0 when the link does not listen.
	int get_port() const;
	void poll();

	// Sends step, next, out, continue or break. Step goes into a call, next goes over it and out
	// leaves it. Break stops a game that is running.
	void send_command(const String &p_command);
	void set_breakpoint(const String &p_source, int p_step, bool p_set);
	// Asks for the locals and members of a level of the stack, the innermost first.
	void request_variables(int p_level);
	// Asks for the value on an input of a step at a level.
	void request_wire(int p_level, int p_step, int p_port);

	// What the game last said, as JSON.
	String get_state() const;

	GraphDebugLink();
};
