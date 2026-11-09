/**************************************************************************/
/*  script_editor_navigation_marker.cpp                                   */

#include "script_editor_navigation_marker.h"

#include "core/config/engine.h"
#include "core/os/memory.h"

ScriptEditorNavigationMarker *ScriptEditorNavigationMarker::get_singleton() {
	if (!singleton) {
		singleton = memnew(ScriptEditorNavigationMarker);
	}
	return singleton;
}

void ScriptEditorNavigationMarker::release_singleton() {
	if (!singleton) {
		return;
	}
	memdelete(singleton);
	singleton = nullptr;
}

void ScriptEditorNavigationMarker::init_begin() {
	init_in_progress = true;
}

void ScriptEditorNavigationMarker::init_end() {
	init_in_progress = false;
}

void ScriptEditorNavigationMarker::locate_begin() {
	locate_in_progress = true;
}

void ScriptEditorNavigationMarker::locate_end() {
	locate_in_progress = false;
	locate_end_physics_frame = Engine::get_singleton()->get_physics_frames();
	locate_end_process_frame = Engine::get_singleton()->get_process_frames();
}

void ScriptEditorNavigationMarker::traverse_begin() {
	traverse_in_progress = true;
}

void ScriptEditorNavigationMarker::traverse_end() {
	traverse_in_progress = false;
	traverse_end_physics_frame = Engine::get_singleton()->get_physics_frames();
	traverse_end_process_frame = Engine::get_singleton()->get_process_frames();
}

bool ScriptEditorNavigationMarker::is_initializing() const {
	return init_in_progress;
}

bool ScriptEditorNavigationMarker::is_locating() const {
	return locate_in_progress;
}

bool ScriptEditorNavigationMarker::is_traversing() const {
	return traverse_in_progress;
}

bool ScriptEditorNavigationMarker::is_locate_just_occured() const {
	if (Engine::get_singleton()->is_in_physics_frame()) {
		return locate_end_physics_frame == Engine::get_singleton()->get_physics_frames() || locate_end_physics_frame == Engine::get_singleton()->get_physics_frames() - 1;
	} else {
		return locate_end_process_frame == Engine::get_singleton()->get_process_frames();
	}
}

bool ScriptEditorNavigationMarker::is_traverse_just_occured() const {
	if (Engine::get_singleton()->is_in_physics_frame()) {
		return traverse_end_physics_frame == Engine::get_singleton()->get_physics_frames() || traverse_end_physics_frame == Engine::get_singleton()->get_physics_frames() - 1;
	} else {
		return traverse_end_process_frame == Engine::get_singleton()->get_process_frames();
	}
}
