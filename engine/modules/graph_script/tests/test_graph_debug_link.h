/**************************************************************************/
/*  test_graph_debug_link.h                                               */

#pragma once

#include "../graph_debug_link.h"

#include "core/config/project_settings.h"
#include "core/io/json.h"
#include "core/os/os.h"

namespace TestGraphDebugLink {

// The thread a running game reports when it stops, and the thread it listens on when it runs.
constexpr int STOPPED_THREAD = 5;
constexpr int MAIN_THREAD = 1;
constexpr int WAIT_TRIES = 400;
constexpr int WAIT_USEC = 5000;

// A game on the loopback address. It is the same peer class a running game uses, connected to the port the link listens on.
struct Game {
	Ref<GraphDebugLink> link;
	Ref<RemoteDebuggerPeer> peer;
	int settled = 0;

	static Ref<RemoteDebuggerPeer> connect(const Ref<GraphDebugLink> &p_link) {
		Ref<RemoteDebuggerPeer> peer = RemoteDebuggerPeerTCP::create_tcp(vformat("tcp://127.0.0.1:%d", p_link->get_port()));
		REQUIRE(peer.is_valid());
		return peer;
	}

	Game() {
		// The test run does not start the engine's own setup, which defines this limit. A peer reads it and an undefined limit is zero messages.
		GLOBAL_DEF("network/limits/debugger/max_queued_messages", 2048);
		link.instantiate();
		REQUIRE(link->listen(0) == OK);
		REQUIRE(link->get_port() != 0);
		peer = connect(link);
	}

	~Game() {
		peer->close();
	}

	Dictionary state() const {
		return JSON::parse_string(link->get_state());
	}

	template <typename Done>
	bool wait_for(Done p_done) {
		for (int i = 0; i < WAIT_TRIES; i++) {
			link->poll();
			if (p_done()) {
				return true;
			}
			OS::get_singleton()->delay_usec(WAIT_USEC);
		}
		return false;
	}

	bool wait_connected() {
		return wait_for([this]() { return bool(state()["connected"]); });
	}

	void say(const String &p_command, const Array &p_data) {
		REQUIRE(peer->put_message(Array{ p_command, MAIN_THREAD, p_data }) == OK);
	}

	// The next message the link sent the game. When none comes, a message with no command, so a check on it fails and does not index past the end.
	Array hear() {
		for (int i = 0; i < WAIT_TRIES; i++) {
			if (peer->has_message()) {
				return peer->get_message();
			}
			OS::get_singleton()->delay_usec(WAIT_USEC);
		}
		return Array{ "", 0, Array() };
	}

	// Messages are taken in order, so once the link shows a reply sent last, it has taken every message sent before it.
	void settle() {
		const String mark = vformat("settled %d", ++settled);
		say("graph_debug:wire", Array{ 0, 0, 0, mark });
		REQUIRE(wait_for([&]() { return Dictionary(state()["wire"]).get("value", "") == Variant(mark); }));
	}

	// The game stops at a breakpoint and the link has asked for the stack and the first level.
	void stop_at_breakpoint() {
		say("debug_enter", Array{ true, "Breakpoint", true, STOPPED_THREAD });
		REQUIRE(wait_for([this]() { return bool(state()["stopped"]); }));
		hear();
	}

	void send_stack() {
		say("stack_dump", Array{ 6, "res://a.ograph#go", 3, "go", "res://a.ograph#helper", 2, "helper" });
		REQUIRE(wait_for([this]() { return Array(state()["stack"]).size() == 2; }));
	}
};

TEST_CASE("[GraphScript] A link that listens has a port, and a game that connects shows as connected") {
	Game game;
	CHECK(game.state()["connected"] == Variant(false));
	CHECK(game.wait_connected());
}

TEST_CASE("[GraphScript] A link that does not listen has no port and polls nothing") {
	Ref<GraphDebugLink> link;
	link.instantiate();
	CHECK(link->get_port() == 0);
	link->poll();
	link->send_command("continue");
	CHECK(JSON::parse_string(link->get_state()).operator Dictionary()["connected"] == Variant(false));
}

TEST_CASE("[GraphScript] A stop shows its reason and asks the game for the stack and the first level") {
	Game game;
	REQUIRE(game.wait_connected());
	game.say("debug_enter", Array{ true, "Breakpoint", true, STOPPED_THREAD });
	REQUIRE(game.wait_for([&]() { return bool(game.state()["stopped"]); }));
	CHECK(game.state()["reason"] == Variant("Breakpoint"));

	Array asked = game.hear();
	REQUIRE(asked.size() == 3);
	CHECK(asked[0] == Variant("get_stack_dump"));
	CHECK(asked[1] == Variant(STOPPED_THREAD));

	game.send_stack();
	Array frames = game.state()["stack"];
	Dictionary inner = frames[0];
	CHECK(inner["source"] == Variant("res://a.ograph#go"));
	CHECK(inner["line"] == Variant(3));
	CHECK(inner["function"] == Variant("go"));
	Dictionary outer = frames[1];
	CHECK(outer["function"] == Variant("helper"));

	asked = game.hear();
	CHECK(asked[0] == Variant("get_stack_frame_vars"));
	CHECK(asked[1] == Variant(STOPPED_THREAD));
	CHECK(Array(asked[2]) == Array{ 0 });
}

TEST_CASE("[GraphScript] The locals and members of a level replace the ones of the level before") {
	Game game;
	REQUIRE(game.wait_connected());
	game.stop_at_breakpoint();
	game.say("stack_frame_vars", Array{ 3 });
	game.say("stack_frame_var", Array{ "n", 0, 2, 5, 0 });
	game.say("stack_frame_var", Array{ "total", 1, 2, 7, 0 });
	game.say("stack_frame_var", Array{ "Engine", 2, 24, "x", 0 });
	game.settle();

	Array locals = game.state()["locals"];
	REQUIRE(locals.size() == 1);
	CHECK(Dictionary(locals[0])["name"] == Variant("n"));
	CHECK(Dictionary(locals[0])["value"] == Variant("5"));
	Array members = game.state()["members"];
	REQUIRE(members.size() == 1);
	CHECK(Dictionary(members[0])["name"] == Variant("total"));
	CHECK(Dictionary(members[0])["value"] == Variant("7"));

	game.say("stack_frame_vars", Array{ 1 });
	game.say("stack_frame_var", Array{ "m", 0, 2, 1, 0 });
	game.settle();
	CHECK(Array(game.state()["members"]).is_empty());
	Array second = game.state()["locals"];
	REQUIRE(second.size() == 1);
	CHECK(Dictionary(second[0])["name"] == Variant("m"));
}

TEST_CASE("[GraphScript] A request for a level asks the game for that level and remembers it") {
	Game game;
	REQUIRE(game.wait_connected());
	game.stop_at_breakpoint();
	game.link->request_variables(1);

	Array asked = game.hear();
	CHECK(asked[0] == Variant("get_stack_frame_vars"));
	CHECK(Array(asked[2]) == Array{ 1 });
	CHECK(game.state()["level"] == Variant(1));
}

TEST_CASE("[GraphScript] A command goes to the thread that waits, and to the main thread when none does") {
	Game game;
	REQUIRE(game.wait_connected());
	game.stop_at_breakpoint();

	game.link->send_command("step");
	Array stepped = game.hear();
	CHECK(stepped[0] == Variant("step"));
	CHECK(stepped[1] == Variant(STOPPED_THREAD));
	CHECK(Array(stepped[2]).is_empty());

	game.say("debug_exit", Array());
	REQUIRE(game.wait_for([&]() { return !bool(game.state()["stopped"]); }));
	game.link->send_command("break");
	Array broke = game.hear();
	CHECK(broke[0] == Variant("break"));
	CHECK(broke[1] == Variant(MAIN_THREAD));
}

TEST_CASE("[GraphScript] Each debug command reaches the game by its name") {
	Game game;
	REQUIRE(game.wait_connected());
	for (const char *command : { "step", "next", "out", "continue", "break" }) {
		game.link->send_command(command);
		CHECK(game.hear()[0] == Variant(command));
	}
}

TEST_CASE("[GraphScript] A game that connects replaces the one attached, and the stop of the first goes with it") {
	Game game;
	REQUIRE(game.wait_connected());
	game.stop_at_breakpoint();
	game.send_stack();

	Ref<RemoteDebuggerPeer> second = Game::connect(game.link);
	REQUIRE(game.wait_for([&]() { return !bool(game.state()["stopped"]); }));
	CHECK(game.state()["connected"] == Variant(true));
	CHECK(Array(game.state()["stack"]).is_empty());

	game.link->send_command("continue");
	for (int i = 0; i < WAIT_TRIES && !second->has_message(); i++) {
		OS::get_singleton()->delay_usec(WAIT_USEC);
	}
	REQUIRE(second->has_message());
	CHECK(second->get_message()[0] == Variant("continue"));
	CHECK(!game.peer->has_message());
	second->close();
}

TEST_CASE("[GraphScript] A command the game does not know is not sent") {
	Game game;
	REQUIRE(game.wait_connected());
	ERR_PRINT_OFF;
	game.link->send_command("explode");
	ERR_PRINT_ON;
	game.link->send_command("continue");
	CHECK(game.hear()[0] == Variant("continue"));
}

TEST_CASE("[GraphScript] A breakpoint goes to the game as a source, a step and a flag") {
	Game game;
	REQUIRE(game.wait_connected());
	game.link->set_breakpoint("res://a.ograph#go", 3, true);
	game.link->set_breakpoint("res://a.ograph#go", 3, false);

	Array set = game.hear();
	CHECK(set[0] == Variant("breakpoint"));
	CHECK(Array(set[2]) == Array{ "res://a.ograph#go", 3, true });
	CHECK(Array(game.hear()[2]) == Array{ "res://a.ograph#go", 3, false });
}

TEST_CASE("[GraphScript] A wire value comes back with the step and port it was asked for") {
	Game game;
	REQUIRE(game.wait_connected());
	game.stop_at_breakpoint();
	game.link->request_wire(0, 3, 1);

	Array asked = game.hear();
	CHECK(asked[0] == Variant("graph_debug:wire"));
	CHECK(Array(asked[2]) == Array{ 0, 3, 1 });

	game.say("graph_debug:wire", Array{ 0, 3, 1, "12" });
	REQUIRE(game.wait_for([&]() { return !Dictionary(game.state()["wire"]).is_empty(); }));
	Dictionary wire = game.state()["wire"];
	CHECK(wire["step"] == Variant(3));
	CHECK(wire["port"] == Variant(1));
	CHECK(wire["value"] == Variant("12"));
}

TEST_CASE("[GraphScript] A game that runs on leaves nothing of the stop behind") {
	Game game;
	REQUIRE(game.wait_connected());
	game.stop_at_breakpoint();
	game.send_stack();
	game.say("graph_debug:wire", Array{ 0, 3, 1, "12" });
	REQUIRE(game.wait_for([&]() { return !Dictionary(game.state()["wire"]).is_empty(); }));

	game.say("debug_exit", Array());
	REQUIRE(game.wait_for([&]() { return !bool(game.state()["stopped"]); }));
	Dictionary state = game.state();
	CHECK(state["connected"] == Variant(true));
	CHECK(state["reason"] == Variant(""));
	CHECK(Array(state["stack"]).is_empty());
	CHECK(Array(state["locals"]).is_empty());
	CHECK(Dictionary(state["wire"]).is_empty());
}

TEST_CASE("[GraphScript] A game that goes away shows as not connected, and the next game takes its place") {
	Game game;
	REQUIRE(game.wait_connected());
	game.stop_at_breakpoint();
	game.peer->close();
	REQUIRE(game.wait_for([&]() { return !bool(game.state()["connected"]); }));
	CHECK(game.state()["stopped"] == Variant(false));
	game.link->send_command("continue");

	game.peer = Game::connect(game.link);
	CHECK(game.wait_connected());
	game.link->send_command("continue");
	CHECK(game.hear()[0] == Variant("continue"));
}

TEST_CASE("[GraphScript] A message that does not fit its command is ignored") {
	Game game;
	REQUIRE(game.wait_connected());
	ERR_PRINT_OFF;
	REQUIRE(game.peer->put_message(Array{ "debug_enter" }) == OK);
	REQUIRE(game.peer->put_message(Array{ 5, MAIN_THREAD, Array() }) == OK);
	game.say("debug_enter", Array{ true });
	game.say("stack_dump", Array{ 6, "res://a.ograph#go" });
	game.say("graph_debug:wire", Array{ 0 });
	game.say("stack_frame_var", Array{ "n" });
	game.say("unknown_command", Array());
	game.say("debug_enter", Array{ true, "Breakpoint", true, STOPPED_THREAD });
	const bool stopped = game.wait_for([&]() { return bool(game.state()["stopped"]); });
	ERR_PRINT_ON;

	REQUIRE(stopped);
	Dictionary state = game.state();
	CHECK(state["reason"] == Variant("Breakpoint"));
	CHECK(Array(state["stack"]).is_empty());
	CHECK(Dictionary(state["wire"]).is_empty());
}

} // namespace TestGraphDebugLink
