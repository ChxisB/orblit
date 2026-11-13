/**************************************************************************/
/*  graph_debug_link.cpp                                                  */

#include "graph_debug_link.h"

#include "core/io/ip_address.h"
#include "core/io/json.h"
#include "core/object/class_registry.h"

namespace {

// The commands the game's debug loop answers by running on, or by stopping.
const char *const COMMANDS[] = { "step", "next", "out", "continue", "break" };

// A variable arrives as [name, scope, type, value, hint]. Scope 0 is a local and 1 is a member.
constexpr int LOCAL_SCOPE = 0;
constexpr int MEMBER_SCOPE = 1;

Dictionary make_row(const String &p_name, const Variant &p_value) {
	Dictionary row;
	row["name"] = p_name;
	row["value"] = p_value.stringify();
	return row;
}

} // namespace

void GraphDebugLink::register_members() {
	ClassRegistry::bind_method(D_METHOD("listen", "port"), &GraphDebugLink::listen);
	ClassRegistry::bind_method(D_METHOD("get_port"), &GraphDebugLink::get_port);
	ClassRegistry::bind_method(D_METHOD("poll"), &GraphDebugLink::poll);
	ClassRegistry::bind_method(D_METHOD("send_command", "command"), &GraphDebugLink::send_command);
	ClassRegistry::bind_method(D_METHOD("set_breakpoint", "source", "step", "set"), &GraphDebugLink::set_breakpoint);
	ClassRegistry::bind_method(D_METHOD("request_variables", "level"), &GraphDebugLink::request_variables);
	ClassRegistry::bind_method(D_METHOD("request_wire", "level", "step", "port"), &GraphDebugLink::request_wire);
	ClassRegistry::bind_method(D_METHOD("get_state"), &GraphDebugLink::get_state);
}

void GraphDebugLink::_reset_state() {
	thread = Thread::MAIN_ID;
	state.clear();
	state["connected"] = peer.is_valid();
	state["stopped"] = false;
	state["reason"] = "";
	state["stack"] = Array();
	state["level"] = 0;
	state["locals"] = Array();
	state["members"] = Array();
	state["wire"] = Dictionary();
}

GraphDebugLink::GraphDebugLink() {
	_reset_state();
}

Error GraphDebugLink::listen(int p_port) {
	// A bind that fails must not drop the server that already listens.
	Ref<TCPServer> next;
	next.instantiate();
	const Error error = next->listen(p_port, IPAddress("127.0.0.1"));
	if (error == OK) {
		server = next;
	}
	return error;
}

int GraphDebugLink::get_port() const {
	return server.is_valid() ? server->get_local_port() : 0;
}

// One game at a time. A game that connects while another is attached replaces it.
void GraphDebugLink::_accept(const Ref<StreamPeerSocket> &p_socket) {
	peer = Ref<RemoteDebuggerPeerTCP>(memnew(RemoteDebuggerPeerTCP(p_socket)));
	_reset_state();
}

void GraphDebugLink::poll() {
	if (server.is_valid() && server->is_connection_available()) {
		_accept(server->take_connection());
	}
	if (peer.is_null()) {
		return;
	}
	while (peer->has_message()) {
		_handle(peer->get_message());
	}
	if (!peer->is_peer_connected()) {
		peer.unref();
		_reset_state();
	}
}

void GraphDebugLink::_send(const String &p_command, const Array &p_data) {
	if (peer.is_valid()) {
		peer->put_message(Array{ p_command, thread, p_data });
	}
}

void GraphDebugLink::_handle(const Array &p_message) {
	if (p_message.size() != 3 || p_message[0].get_type() != Variant::STRING || p_message[2].get_type() != Variant::ARRAY) {
		return;
	}
	const String command = p_message[0];
	const Array data = p_message[2];
	if (command == "debug_enter") {
		_on_enter(data);
	} else if (command == "stack_dump") {
		_on_stack_dump(data);
	} else if (command == "stack_frame_vars") {
		state["locals"] = Array();
		state["members"] = Array();
	} else if (command == "stack_frame_var") {
		_on_variable(data);
	} else if (command == "debug_exit") {
		_reset_state();
	} else if (command == "graph_debug:wire") {
		_on_wire(data);
	}
}

// The game stopped. It is waiting for the commands that ask what it holds, so the stack and the
// innermost level come first.
void GraphDebugLink::_on_enter(const Array &p_data) {
	ERR_FAIL_COND(p_data.size() != 4);
	thread = p_data[3];
	state["stopped"] = true;
	state["reason"] = p_data[1];
	_send("get_stack_dump", Array());
}

void GraphDebugLink::_on_stack_dump(const Array &p_data) {
	ERR_FAIL_COND(p_data.is_empty());
	const int count = int(p_data[0]) / 3;
	ERR_FAIL_COND(p_data.size() != 1 + count * 3);
	Array stack;
	for (int i = 0; i < count; i++) {
		Dictionary frame;
		frame["source"] = p_data[1 + i * 3];
		frame["line"] = p_data[2 + i * 3];
		frame["function"] = p_data[3 + i * 3];
		stack.push_back(frame);
	}
	state["stack"] = stack;
	request_variables(0);
}

void GraphDebugLink::_on_variable(const Array &p_data) {
	ERR_FAIL_COND(p_data.size() != 5);
	const int scope = p_data[1];
	if (scope == LOCAL_SCOPE || scope == MEMBER_SCOPE) {
		Array rows = state[scope == LOCAL_SCOPE ? "locals" : "members"];
		rows.push_back(make_row(p_data[0], p_data[3]));
	}
}

void GraphDebugLink::_on_wire(const Array &p_data) {
	ERR_FAIL_COND(p_data.size() != 4);
	Dictionary wire;
	wire["level"] = p_data[0];
	wire["step"] = p_data[1];
	wire["port"] = p_data[2];
	wire["value"] = p_data[3];
	state["wire"] = wire;
}

void GraphDebugLink::send_command(const String &p_command) {
	for (const char *known : COMMANDS) {
		if (p_command == known) {
			_send(p_command, Array());
			return;
		}
	}
	ERR_PRINT(vformat("Unknown debug command '%s'.", p_command));
}

void GraphDebugLink::set_breakpoint(const String &p_source, int p_step, bool p_set) {
	_send("breakpoint", Array{ p_source, p_step, p_set });
}

void GraphDebugLink::request_variables(int p_level) {
	state["level"] = p_level;
	_send("get_stack_frame_vars", Array{ p_level });
}

void GraphDebugLink::request_wire(int p_level, int p_step, int p_port) {
	_send("graph_debug:wire", Array{ p_level, p_step, p_port });
}

String GraphDebugLink::get_state() const {
	return JSON::stringify(state);
}
