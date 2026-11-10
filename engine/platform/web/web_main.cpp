/**************************************************************************/
/*  web_main.cpp                                                          */

#include "display_system_web.h"
#include "js.h"
#include "os_web.h"
#include "web_script_proxy.h"

#include "core/config/engine.h"
#include "core/io/resource_loader.h"
#include "core/os/main_loop.h"
#include "core/os/os.h"
#include "core/profiling/profiling.h"
#include "startup/main.h"

#ifdef TOOLS_ENABLED
#include "core/io/file_access.h"
#include "editor/editor_debugger_server_messageport.h"
#include "editor/editor_node.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h" // Stage only forward declares it.
#endif

#include <emscripten/emscripten.h>

#include <cstdlib>

static OS_Web *os = nullptr;
#ifndef PROXY_TO_PTHREAD_ENABLED
static uint64_t target_ticks = 0;
#endif

static bool main_started = false;
static bool shutdown_complete = false;

void exit_callback() {
	if (!shutdown_complete) {
		return; // Still waiting.
	}
	if (main_started) {
		web_script_proxy_unregister();
		Main::cleanup();
		main_started = false;
	}
	int exit_code = OS_Web::get_singleton()->get_exit_code();
	delete os; // We used a normal new, so it needs a normal delete.
	os = nullptr;
	cleanup_profiler();
	emscripten_force_exit(exit_code); // Exit runtime.
}

void cleanup_after_sync() {
	shutdown_complete = true;
}

void main_loop_callback() {
#ifndef PROXY_TO_PTHREAD_ENABLED
	uint64_t current_ticks = os->get_ticks_usec();
#endif

	bool force_draw = DisplaySystemWeb::get_singleton()->check_size_force_redraw();
	if (force_draw) {
		Main::force_redraw();
#ifndef PROXY_TO_PTHREAD_ENABLED
	} else if (current_ticks < target_ticks) {
		return; // Skip frame.
#endif
	}

#ifndef PROXY_TO_PTHREAD_ENABLED
	int max_fps = Engine::get_singleton()->get_max_fps();
	if (max_fps > 0) {
		if (current_ticks - target_ticks > 1000000) {
			// When the window loses focus, we stop getting updates and accumulate delay.
			// For this reason, if the difference is too big, we reset target ticks to the current ticks.
			target_ticks = current_ticks;
		}
		target_ticks += (uint64_t)(1000000 / max_fps);
	}
#endif

	if (os->main_loop_iterate()) {
		emscripten_cancel_main_loop(); // Cancel current loop and set the cleanup one.
		emscripten_set_main_loop(exit_callback, -1, false);
		orblit_js_os_finish_async(cleanup_after_sync);
	}
}

void print_web_header() {
	// Emscripten.
	char *emscripten_version_char = orblit_js_emscripten_get_version();
	String emscripten_version = vformat("Emscripten %s", emscripten_version_char);
	// `free()` is used here because it's not memory that was allocated by Orblit.
	free(emscripten_version_char);

	// Build features.
	String thread_support = OS::get_singleton()->has_feature("threads")
			? "multi-threaded"
			: "single-threaded";
	String extensions_support = OS::get_singleton()->has_feature("web_extensions")
			? "NativeExtension support"
			: "no NativeExtension support";

	Vector<String> build_configuration = { emscripten_version, thread_support, extensions_support };
	print_line(vformat("Build configuration: %s.", String(", ").join(build_configuration)));
}

/// When calling main, it is assumed FS is setup and synced.
extern EMSCRIPTEN_KEEPALIVE int orblit_web_main(int argc, char *argv[]) {
	init_profiler();

	os = new OS_Web();

#ifdef TOOLS_ENABLED
	EditorNode::add_init_callback(&EditorDebuggerServerMessagePort::initialize);
#endif

	// We must override main when testing is enabled
	TEST_MAIN_OVERRIDE

	Error err = Main::setup(argv[0], argc - 1, &argv[1]);

	// Proper shutdown in case of setup failure.
	if (err != OK) {
		// Will only exit after sync.
		emscripten_set_main_loop(exit_callback, -1, false);
		orblit_js_os_finish_async(cleanup_after_sync);
		if (err == ERR_HELP) { // Returned by --help and --version, so success.
			return EXIT_SUCCESS;
		}
		return EXIT_FAILURE;
	}

	print_web_header();

	main_started = true;

	// Ease up compatibility.
	ResourceLoader::set_abort_on_missing_resources(false);

	web_script_proxy_register();

	int ret = Main::start();
	os->set_exit_code(ret);
	os->get_main_loop()->initialize();
#ifdef TOOLS_ENABLED
	if (Engine::get_singleton()->is_project_manager_hint() && FileAccess::exists("/tmp/preload.zip")) {
		PackedStringArray ps;
		ps.push_back("/tmp/preload.zip");
		Stage::get_singleton()->get_root()->emit_signal(SNAME("files_dropped"), ps);
	}
#endif
	emscripten_set_main_loop(main_loop_callback, -1, false);
	// Immediately run the first iteration.
	// We are inside an animation frame, we want to immediately draw on the newly setup canvas.
	main_loop_callback();

	return os->get_exit_code();
}
