/**************************************************************************/
/*  editor_run.h                                                          */

#pragma once

#include "core/math/vector2i.h"
#include "core/os/process_id.h"
#include "core/string/ustring.h"
#include "core/containers/list.h"
#include "core/variant/callable.h"

#include <climits> // INT_MAX

typedef void (*EditorRunInstanceStarting)(int p_index, List<String> &r_arguments);
typedef bool (*EditorRunInstanceRequestScreenshot)(const Callable &p_callback);

class EditorRun {
public:
	enum Status {
		STATUS_PLAY,
		STATUS_PAUSED,
		STATUS_STOP
	};

	List<ProcessID> pids;

	struct WindowPlacement {
		int screen = 0;
		Point2i position = Point2i(INT_MAX, INT_MAX);
		Size2i size;
		bool force_maximized = false;
		bool force_fullscreen = false;
	};

private:
	Status status;
	String running_scene;

public:
	inline static EditorRunInstanceStarting instance_starting_callback = nullptr;
	inline static EditorRunInstanceRequestScreenshot instance_rq_screenshot_callback = nullptr;

	Status get_status() const;
	String get_running_scene() const;

	Error run(const String &p_scene, const String &p_write_movie = "", const Vector<String> &p_run_args = Vector<String>());
	void run_native_notify() { status = STATUS_PLAY; }
	void stop();

	void stop_child_process(ProcessID p_pid);
	bool has_child_process(ProcessID p_pid) const;
	int get_child_process_count() const { return pids.size(); }
	ProcessID get_current_process() const;

	static bool request_screenshot(const Callable &p_callback);

	static WindowPlacement get_window_placement();

	EditorRun();
};
