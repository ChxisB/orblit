/**************************************************************************/
/*  script_editor_navigation_marker.h                                     */

#pragma once

#include <cstdint>

class ScriptEditorNavigationMarker {
	static inline ScriptEditorNavigationMarker *singleton = nullptr;

private:
	bool init_in_progress = false;

	bool locate_in_progress = false;
	uint64_t locate_end_physics_frame = 0;
	uint64_t locate_end_process_frame = 0;

	bool traverse_in_progress = false;
	uint64_t traverse_end_physics_frame = 0;
	uint64_t traverse_end_process_frame = 0;

public:
	static ScriptEditorNavigationMarker *get_singleton();
	static void release_singleton();

	void init_begin();
	void init_end();
	void locate_begin();
	void locate_end();
	void traverse_begin();
	void traverse_end();

	bool is_initializing() const;
	bool is_locating() const;
	bool is_traversing() const;

	bool is_locate_just_occured() const;
	bool is_traverse_just_occured() const;
};
