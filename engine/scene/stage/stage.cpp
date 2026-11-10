/**************************************************************************/
/*  stage.cpp                                                             */

#include "stage.h"

STATIC_ASSERT_INCOMPLETE_TYPE(class, RenderingSystem);

#include "core/config/engine.h"
#include "core/config/project_settings.h"
#include "core/input/input.h"
#include "core/io/image_loader.h"
#include "core/io/resource_loader.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "core/object/worker_thread_pool.h"
#include "core/os/os.h"
#include "core/profiling/profiling.h"
#include "scene/animation/tween.h"
#include "scene/audio/audio_stream_player.h"
#include "scene/debugger/scene_debugger.h"
#include "scene/resources/environment.h"
#include "scene/resources/image_texture.h"
#include "scene/resources/material.h"
#include "scene/resources/mesh.h"
#include "scene/resources/prefab.h"
#include "scene/resources/world_planar.h"
#include "scene/stage/entity.h"
#include "scene/stage/multiplayer_api.h"
#include "scene/stage/viewport.h"
#include "scene/stage/window.h"
#include "scene/ui/control.h"
#include "systems/display/accessibility_system.h"
#include "systems/display/display_system.h"
#include "systems/rendering/rendering_system.h"

#ifndef SPATIAL_DISABLED
#include "scene/resources/spatial/world_spatial.h"
#include "scene/spatial/entity_spatial.h"
#endif // SPATIAL_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
#include "systems/physics_planar/physics_system_planar.h"
#endif // PHYSICS_PLANAR_DISABLED

#ifndef PHYSICS_SPATIAL_DISABLED
#include "systems/physics_spatial/physics_system_spatial.h"
#endif // PHYSICS_SPATIAL_DISABLED

void StageTimer::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_time_left", "time"), &StageTimer::set_time_left);
	ClassRegistry::bind_method(D_METHOD("get_time_left"), &StageTimer::get_time_left);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "time_left", PROPERTY_HINT_NONE, "suffix:s"), "set_time_left", "get_time_left");

	ADD_SIGNAL(MethodInfo("timeout"));
}

void StageTimer::set_time_left(double p_time) {
	time_left = p_time;
}

double StageTimer::get_time_left() const {
	return MAX(time_left, 0.0);
}

void StageTimer::set_process_always(bool p_process_always) {
	process_always = p_process_always;
}

bool StageTimer::is_process_always() {
	return process_always;
}

void StageTimer::set_process_in_physics(bool p_process_in_physics) {
	process_in_physics = p_process_in_physics;
}

bool StageTimer::is_process_in_physics() {
	return process_in_physics;
}

void StageTimer::set_ignore_time_scale(bool p_ignore) {
	ignore_time_scale = p_ignore;
}

bool StageTimer::is_ignoring_time_scale() {
	return ignore_time_scale;
}

void StageTimer::release_connections() {
	List<Connection> signal_connections;
	get_all_signal_connections(&signal_connections);

	for (const Connection &connection : signal_connections) {
		disconnect(connection.signal.get_name(), connection.callable);
	}
}

bool Stage::_physics_interpolation_enabled = false;
bool Stage::_physics_interpolation_enabled_in_project = false;

void Stage::stage_changed() {
	emit_signal(stage_changed_name);
}

void Stage::entity_added(Entity *p_node) {
	emit_signal(node_added_name, p_node);
}

void Stage::entity_removed(Entity *p_node) {
	// Nodes can only be removed from the main thread.
	if (current_scene == p_node) {
		current_scene = nullptr;
	}
	emit_signal(node_removed_name, p_node);
	if (nodes_removed_on_group_call_lock) {
		nodes_removed_on_group_call.insert(p_node);
	}
}

void Stage::entity_renamed(Entity *p_node) {
	emit_signal(node_renamed_name, p_node);
}

StageGroup *Stage::add_to_group(const StringName &p_group, Entity *p_node) {
	_THREAD_SAFE_METHOD_

	HashMap<StringName, StageGroup>::Iterator E = group_map.find(p_group);
	if (!E) {
		E = group_map.insert(p_group, StageGroup());
	}

	ERR_FAIL_COND_V_MSG(E->value.nodes.has(p_node), &E->value, "Already in group: " + p_group + ".");
	E->value.nodes.push_back(p_node);
	E->value.changed = true;
	return &E->value;
}

void Stage::remove_from_group(const StringName &p_group, Entity *p_node) {
	_THREAD_SAFE_METHOD_

	HashMap<StringName, StageGroup>::Iterator E = group_map.find(p_group);
	ERR_FAIL_COND(!E);

	E->value.nodes.erase(p_node);
	if (E->value.nodes.is_empty()) {
		group_map.remove(E);
	}
}

void Stage::flush_transform_notifications() {
	_THREAD_SAFE_METHOD_

	SelfList<Entity> *n = xform_change_list.first();
	while (n) {
		Entity *node = n->self();
		SelfList<Entity> *nx = n->next();
		xform_change_list.remove(n);
		n = nx;
		node->notification(NOTIFICATION_TRANSFORM_CHANGED);
	}
}

bool Stage::is_accessibility_enabled() const {
	if (!DisplaySystem::get_singleton()->has_feature(DisplaySystemEnums::FEATURE_ACCESSIBILITY_SCREEN_READER)) {
		return false;
	}

	AccessibilitySystemEnums::AccessibilityMode accessibility_mode = AccessibilitySystem::get_singleton()->get_mode();
	int screen_reader_active = DisplaySystem::get_singleton()->accessibility_screen_reader_active();
	if ((accessibility_mode == AccessibilitySystemEnums::AccessibilityMode::ACCESSIBILITY_DISABLED) || ((accessibility_mode == AccessibilitySystemEnums::AccessibilityMode::ACCESSIBILITY_AUTO) && (screen_reader_active != 1))) {
		return false;
	}
	return true;
}

bool Stage::is_accessibility_supported() const {
	if (!DisplaySystem::get_singleton()->has_feature(DisplaySystemEnums::FEATURE_ACCESSIBILITY_SCREEN_READER)) {
		return false;
	}

	AccessibilitySystemEnums::AccessibilityMode accessibility_mode = AccessibilitySystem::get_singleton()->get_mode();
	if (accessibility_mode == AccessibilitySystemEnums::AccessibilityMode::ACCESSIBILITY_DISABLED) {
		return false;
	}
	return true;
}

void Stage::_accessibility_force_update() {
	accessibility_force_update = true;
}

void Stage::_accessibility_notify_change(const Entity *p_node, bool p_remove) {
	if (p_node) {
		if (p_remove) {
			accessibility_change_queue.erase(p_node->get_instance_id());
		} else {
			accessibility_change_queue.insert(p_node->get_instance_id());
		}
	}
}

void Stage::_process_accessibility_changes(DisplaySystemEnums::WindowID p_window_id) {
	// Process NOTIFICATION_ACCESSIBILITY_UPDATE.
	Vector<ObjectID> processed;
	for (const ObjectID &id : accessibility_change_queue) {
		Entity *node = Object::cast_to<Entity>(ObjectRegistry::get_instance(id));
		if (!node || !node->get_non_popup_window() || !node->get_window()->is_visible()) {
			processed.push_back(id);
			continue; // Invalid node, remove from list and skip.
		} else if (node->get_non_popup_window()->get_window_id() != p_window_id) {
			continue; // Another window, skip.
		}
		node->notification(Entity::NOTIFICATION_ACCESSIBILITY_UPDATE);
		processed.push_back(id);
	}

	// Track focus change.
	// Note: Do not use `Window::get_focused_window()`, it returns both native and embedded windows, and we only care about focused element in the currently processed native window.
	// Native window focus is handled in the DisplaySystem, or AccessKit subclassing adapter.
	ObjectID oid = DisplaySystem::get_singleton()->window_get_attached_instance_id(p_window_id);
	Window *w_this = (Window *)ObjectRegistry::get_instance(oid);
	if (w_this) {
		Window *w_focus = w_this->get_focused_subwindow();
		if (w_focus && !w_focus->is_part_of_edited_scene()) {
			w_this = w_focus;
		}

		// Popups have no native window focus, but have focused element.
		DisplaySystemEnums::WindowID popup_id = DisplaySystem::get_singleton()->window_get_active_popup();
		if (popup_id != DisplaySystemEnums::INVALID_WINDOW_ID) {
			Window *popup_w = Window::get_from_id(popup_id);
			if (popup_w && w_this->is_ancestor_of(popup_w)) {
				w_this = popup_w;
			}
		}

		RID new_focus_element;
		Control *n_focus = w_this->gui_get_focus_owner();
		if (n_focus && !n_focus->is_part_of_edited_scene()) {
			new_focus_element = n_focus->get_focused_accessibility_element();
		} else {
			new_focus_element = w_this->get_focused_accessibility_element();
		}

		AccessibilitySystem::get_singleton()->update_set_focus(new_focus_element);
	}

	// Cleanup.
	for (const ObjectID &id : processed) {
		accessibility_change_queue.erase(id);
	}
}

void Stage::_flush_accessibility_changes() {
	if (is_accessibility_enabled()) {
		uint64_t time = OS::get_singleton()->get_ticks_msec();
		if (!accessibility_force_update) {
			if (time - accessibility_last_update < 1000 / accessibility_upd_per_sec) {
				return;
			}
		}
		accessibility_force_update = false;
		accessibility_last_update = time;

		// Push update to the accessibility driver.
		AccessibilitySystem::get_singleton()->update_if_active(callable_mp(this, &Stage::_process_accessibility_changes));
	}
}

void Stage::_flush_ugc() {
	ugc_locked = true;

	while (unique_group_calls.size()) {
		HashMap<UGCall, Vector<Variant>, UGCall>::Iterator E = unique_group_calls.begin();

		const Variant **argptrs = (const Variant **)alloca(E->value.size() * sizeof(Variant *));

		for (int i = 0; i < E->value.size(); i++) {
			argptrs[i] = &E->value[i];
		}

		call_group_flagsp(GROUP_CALL_DEFAULT, E->key.group, E->key.call, argptrs, E->value.size());

		unique_group_calls.remove(E);
	}

	ugc_locked = false;
}

void Stage::_update_group_order(StageGroup &g) {
	if (!g.changed) {
		return;
	}
	if (g.nodes.is_empty()) {
		return;
	}

	Entity **gr_nodes = g.nodes.ptrw();
	int gr_node_count = g.nodes.size();

	SortArray<Entity *, Entity::Comparator> node_sort;
	node_sort.sort(gr_nodes, gr_node_count);

	g.changed = false;
}

RequiredResult<Window> Stage::get_root() const {
	return root;
}

void Stage::call_group_flagsp(uint32_t p_call_flags, const StringName &p_group, const StringName &p_function, const Variant **p_args, int p_argcount) {
	Vector<Entity *> nodes_copy;

	{
		_THREAD_SAFE_METHOD_

		HashMap<StringName, StageGroup>::Iterator E = group_map.find(p_group);
		if (!E) {
			return;
		}
		StageGroup &g = E->value;
		if (g.nodes.is_empty()) {
			return;
		}

		if (p_call_flags & GROUP_CALL_UNIQUE && p_call_flags & GROUP_CALL_DEFERRED) {
			ERR_FAIL_COND(ugc_locked);

			UGCall ug;
			ug.call = p_function;
			ug.group = p_group;

			if (unique_group_calls.has(ug)) {
				return;
			}

			Vector<Variant> args;
			for (int i = 0; i < p_argcount; i++) {
				args.push_back(*p_args[i]);
			}

			unique_group_calls[ug] = args;
			return;
		}

		_update_group_order(g);
		nodes_copy = g.nodes;
	}

	Entity **gr_nodes = nodes_copy.ptrw();
	int gr_node_count = nodes_copy.size();

	{
		_THREAD_SAFE_METHOD_
		nodes_removed_on_group_call_lock++;
	}

	if (p_call_flags & GROUP_CALL_REVERSE) {
		for (int i = gr_node_count - 1; i >= 0; i--) {
			if (nodes_removed_on_group_call_lock && nodes_removed_on_group_call.has(gr_nodes[i])) {
				continue;
			}

			Entity *node = gr_nodes[i];
			if (!(p_call_flags & GROUP_CALL_DEFERRED)) {
				Callable::CallError ce;
				node->callp(p_function, p_args, p_argcount, ce);
				if (unlikely(ce.error != Callable::CallError::CALL_OK && ce.error != Callable::CallError::CALL_ERROR_INVALID_METHOD)) {
					ERR_PRINT(vformat("Error calling group method on node \"%s\": %s.", node->get_name(), Variant::get_callable_error_text(Callable(node, p_function), p_args, p_argcount, ce)));
				}
			} else {
				MessageQueue::get_singleton()->push_callp(node, p_function, p_args, p_argcount);
			}
		}

	} else {
		for (int i = 0; i < gr_node_count; i++) {
			if (nodes_removed_on_group_call_lock && nodes_removed_on_group_call.has(gr_nodes[i])) {
				continue;
			}

			Entity *node = gr_nodes[i];
			if (!(p_call_flags & GROUP_CALL_DEFERRED)) {
				Callable::CallError ce;
				node->callp(p_function, p_args, p_argcount, ce);
				if (unlikely(ce.error != Callable::CallError::CALL_OK && ce.error != Callable::CallError::CALL_ERROR_INVALID_METHOD)) {
					ERR_PRINT(vformat("Error calling group method on node \"%s\": %s.", node->get_name(), Variant::get_callable_error_text(Callable(node, p_function), p_args, p_argcount, ce)));
				}
			} else {
				MessageQueue::get_singleton()->push_callp(node, p_function, p_args, p_argcount);
			}
		}
	}

	{
		_THREAD_SAFE_METHOD_
		nodes_removed_on_group_call_lock--;
		if (nodes_removed_on_group_call_lock == 0) {
			nodes_removed_on_group_call.clear();
		}
	}
}

void Stage::notify_group_flags(uint32_t p_call_flags, const StringName &p_group, int p_notification) {
	Vector<Entity *> nodes_copy;
	{
		_THREAD_SAFE_METHOD_
		HashMap<StringName, StageGroup>::Iterator E = group_map.find(p_group);
		if (!E) {
			return;
		}
		StageGroup &g = E->value;
		if (g.nodes.is_empty()) {
			return;
		}

		_update_group_order(g);

		nodes_copy = g.nodes;
	}

	Entity **gr_nodes = nodes_copy.ptrw();
	int gr_node_count = nodes_copy.size();

	{
		_THREAD_SAFE_METHOD_
		nodes_removed_on_group_call_lock++;
	}

	if (p_call_flags & GROUP_CALL_REVERSE) {
		for (int i = gr_node_count - 1; i >= 0; i--) {
			if (nodes_removed_on_group_call.has(gr_nodes[i])) {
				continue;
			}

			if (!(p_call_flags & GROUP_CALL_DEFERRED)) {
				gr_nodes[i]->notification(p_notification, true);
			} else {
				MessageQueue::get_singleton()->push_notification(gr_nodes[i], p_notification);
			}
		}

	} else {
		for (int i = 0; i < gr_node_count; i++) {
			if (nodes_removed_on_group_call.has(gr_nodes[i])) {
				continue;
			}

			if (!(p_call_flags & GROUP_CALL_DEFERRED)) {
				gr_nodes[i]->notification(p_notification);
			} else {
				MessageQueue::get_singleton()->push_notification(gr_nodes[i], p_notification);
			}
		}
	}

	{
		_THREAD_SAFE_METHOD_
		nodes_removed_on_group_call_lock--;
		if (nodes_removed_on_group_call_lock == 0) {
			nodes_removed_on_group_call.clear();
		}
	}
}

void Stage::set_group_flags(uint32_t p_call_flags, const StringName &p_group, const String &p_name, const Variant &p_value) {
	Vector<Entity *> nodes_copy;
	{
		_THREAD_SAFE_METHOD_

		HashMap<StringName, StageGroup>::Iterator E = group_map.find(p_group);
		if (!E) {
			return;
		}
		StageGroup &g = E->value;
		if (g.nodes.is_empty()) {
			return;
		}

		_update_group_order(g);

		nodes_copy = g.nodes;
	}
	Entity **gr_nodes = nodes_copy.ptrw();
	int gr_node_count = nodes_copy.size();

	{
		_THREAD_SAFE_METHOD_
		nodes_removed_on_group_call_lock++;
	}

	if (p_call_flags & GROUP_CALL_REVERSE) {
		for (int i = gr_node_count - 1; i >= 0; i--) {
			if (nodes_removed_on_group_call.has(gr_nodes[i])) {
				continue;
			}

			if (!(p_call_flags & GROUP_CALL_DEFERRED)) {
				gr_nodes[i]->set(p_name, p_value);
			} else {
				MessageQueue::get_singleton()->push_set(gr_nodes[i], p_name, p_value);
			}
		}

	} else {
		for (int i = 0; i < gr_node_count; i++) {
			if (nodes_removed_on_group_call.has(gr_nodes[i])) {
				continue;
			}

			if (!(p_call_flags & GROUP_CALL_DEFERRED)) {
				gr_nodes[i]->set(p_name, p_value);
			} else {
				MessageQueue::get_singleton()->push_set(gr_nodes[i], p_name, p_value);
			}
		}
	}

	{
		_THREAD_SAFE_METHOD_
		nodes_removed_on_group_call_lock--;
		if (nodes_removed_on_group_call_lock == 0) {
			nodes_removed_on_group_call.clear();
		}
	}
}

void Stage::notify_group(const StringName &p_group, int p_notification) {
	notify_group_flags(GROUP_CALL_DEFAULT, p_group, p_notification);
}

void Stage::set_group(const StringName &p_group, const String &p_name, const Variant &p_value) {
	set_group_flags(GROUP_CALL_DEFAULT, p_group, p_name, p_value);
}

void Stage::initialize() {
	OrblitProfileZone("Stage::initialize");
	ERR_FAIL_NULL(root);
	MainLoop::initialize();
	root->_set_tree(this);
}

void Stage::set_physics_interpolation_enabled(bool p_enabled) {
	// This version is for use in editor.
	_physics_interpolation_enabled_in_project = p_enabled;

	// We never want interpolation in the editor.
	if (Engine::get_singleton()->is_editor_hint()) {
		p_enabled = false;
	}

	if (p_enabled == _physics_interpolation_enabled) {
		return;
	}

	_physics_interpolation_enabled = p_enabled;
	RenderingSystem::get_singleton()->set_physics_interpolation_enabled(p_enabled);

	get_scene_tree_fti().set_enabled(get_root(), p_enabled);

	// Perform an auto reset on the root node for convenience for the user.
	if (root) {
		root->reset_physics_interpolation();
	}
}

void Stage::iteration_prepare() {
	if (_physics_interpolation_enabled) {
		// Make sure any pending transforms from the last tick / frame
		// are flushed before pumping the interpolation prev and currents.
		flush_transform_notifications();
		get_scene_tree_fti().tick_update();
		RenderingSystem::get_singleton()->tick();
	}
}

bool Stage::physics_process(double p_time) {
	current_frame++;

	flush_transform_notifications();

	if (MainLoop::physics_process(p_time)) {
		_quit = true;
	}
	physics_process_time = p_time;

	emit_signal(SNAME("physics_frame"));

#if !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)
	call_group(SNAME("_picking_viewports"), SNAME("_process_picking"));
#endif // !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)

	_process(true);

	_flush_ugc();
	MessageQueue::get_singleton()->flush(); //small little hack

	process_timers(p_time, true); //go through timers
	process_tweens(p_time, true);

	flush_transform_notifications();

	// This should happen last because any processing that deletes something beforehand might expect the object to be removed in the same frame.
	_flush_delete_queue();

	_call_idle_callbacks();

	return _quit;
}

void Stage::iteration_end() {
	// When physics interpolation is active, we want all pending transforms
	// to be flushed to the RenderingSystem before finishing a physics tick.
	if (_physics_interpolation_enabled) {
		flush_transform_notifications();
	}
}

bool Stage::process(double p_time) {
	// First pass of scene tree fixed timestep interpolation.
	if (get_scene_tree_fti().is_enabled()) {
		// Special, we need to ensure RenderingSystem is up to date
		// with *all* the pending xforms *before* updating it during
		// the FTI update.
		// If this is not done, we can end up with a deferred `set_transform()`
		// overwriting the interpolated xform in the server.
		flush_transform_notifications();
		get_scene_tree_fti().frame_update(get_root(), true);
	}

	if (MainLoop::process(p_time)) {
		_quit = true;
	}

	process_time = p_time;

	if (multiplayer_poll) {
		multiplayer->poll();
		for (KeyValue<EntityPath, Ref<MultiplayerAPI>> &E : custom_multiplayers) {
			E.value->poll();
		}
	}

	emit_signal(SNAME("process_frame"));

	MessageQueue::get_singleton()->flush(); //small little hack

	flush_transform_notifications();

	_process(false);

	_flush_ugc();
	MessageQueue::get_singleton()->flush(); //small little hack
	flush_transform_notifications(); //transforms after world update, to avoid unnecessary enter/exit notifications

	if (unlikely(pending_new_scene_id.is_valid())) {
		_flush_scene_change();
	}

	process_timers(p_time, false); //go through timers
	process_tweens(p_time, false);

	flush_transform_notifications(); // Additional transforms after timers update.

	// This should happen last because any processing that deletes something beforehand might expect the object to be removed in the same frame.
	_flush_delete_queue();

	_flush_accessibility_changes();

	_call_idle_callbacks();

#ifdef TOOLS_ENABLED
#ifndef SPATIAL_DISABLED
	if (Engine::get_singleton()->is_editor_hint()) {
		String env_path = GLOBAL_GET("rendering/environment/defaults/default_environment");
		env_path = env_path.strip_edges(); // User may have added a space or two.

		bool can_load = true;
		if (env_path.begins_with("uid://")) {
			// If an uid path, ensure it is mapped to a resource which could not be
			// the case if the editor is still scanning the filesystem.
			ResourceUID::ID id = ResourceUID::get_singleton()->text_to_id(env_path);
			can_load = ResourceUID::get_singleton()->has_id(id);
			if (can_load) {
				env_path = ResourceUID::get_singleton()->get_id_path(id);
			}
		}

		if (can_load) {
			String cpath;
			Ref<Environment> fallback = get_root()->get_world_3d()->get_fallback_environment();
			if (fallback.is_valid()) {
				cpath = fallback->get_path();
			}
			if (cpath != env_path) {
				if (!env_path.is_empty()) {
					fallback = ResourceLoader::load(env_path);
					if (fallback.is_null()) {
						//could not load fallback, set as empty
						ProjectSettings::get_singleton()->set("rendering/environment/defaults/default_environment", "");
					}
				} else {
					fallback.unref();
				}
				get_root()->get_world_3d()->set_fallback_environment(fallback);
			}
		}
	}
#endif // SPATIAL_DISABLED
#endif // TOOLS_ENABLED

	// Second pass of scene tree fixed timestep interpolation.
	// ToDo: Possibly needs another flush_transform_notifications here
	// depending on whether there are side effects to _call_idle_callbacks().
	get_scene_tree_fti().frame_update(get_root(), false);

	if (_physics_interpolation_enabled) {
		RenderingSystem::get_singleton()->pre_draw(true);
	}

	return _quit;
}

void Stage::process_timers(double p_delta, bool p_physics_frame) {
	_THREAD_SAFE_METHOD_
	const List<Ref<StageTimer>>::Element *L = timers.back(); // Last element.
	const double unscaled_delta = Engine::get_singleton()->get_process_step();

	for (List<Ref<StageTimer>>::Element *E = timers.front(); E;) {
		List<Ref<StageTimer>>::Element *N = E->next();
		Ref<StageTimer> timer = E->get();

		if ((paused && !timer->is_process_always()) || (timer->is_process_in_physics() != p_physics_frame)) {
			if (E == L) {
				break; // Break on last, so if new timers were added during list traversal, ignore them.
			}
			E = N;
			continue;
		}

		double time_left = timer->get_time_left();
		time_left -= timer->is_ignoring_time_scale() ? unscaled_delta : p_delta;
		timer->set_time_left(time_left);

		if (time_left <= 0) {
			E->get()->emit_signal(SNAME("timeout"));
			timers.erase(E);
		}
		if (E == L) {
			break; // Break on last, so if new timers were added during list traversal, ignore them.
		}
		E = N;
	}
}

void Stage::process_tweens(double p_delta, bool p_physics) {
	_THREAD_SAFE_METHOD_
	// This methods works similarly to how StageTimers are handled.
	const List<Ref<Tween>>::Element *L = tweens.back();
	const double unscaled_delta = Engine::get_singleton()->get_process_step();

	for (List<Ref<Tween>>::Element *E = tweens.front(); E;) {
		List<Ref<Tween>>::Element *N = E->next();
		Ref<Tween> &tween = E->get();

		// Don't process if paused or process mode doesn't match.
		if (!tween->can_run(paused) || (p_physics == (tween->get_process_mode() == Tween::TWEEN_PROCESS_IDLE))) {
			if (E == L) {
				break;
			}
			E = N;
			continue;
		}

		if (!tween->step(tween->is_ignoring_time_scale() ? unscaled_delta : p_delta)) {
			tween->clear();
			tweens.erase(E);
		}
		if (E == L) {
			break;
		}
		E = N;
	}
}

void Stage::finalize() {
	_flush_delete_queue();

	_flush_ugc();

	if (root) {
		root->_set_tree(nullptr);
		root->_propagate_after_exit_tree();
		memdelete(root); //delete root
		root = nullptr;

		// In case deletion of some objects was queued when destructing the `root`.
		// E.g. if `queue_destroy()` was called for some node outside the tree when handling NOTIFICATION_PREDELETE for some node in the tree.
		_flush_delete_queue();
	}

	MainLoop::finalize();

	// Cleanup timers.
	for (Ref<StageTimer> &timer : timers) {
		timer->release_connections();
	}
	timers.clear();

	// Cleanup tweens.
	for (Ref<Tween> &tween : tweens) {
		tween->clear();
	}
	tweens.clear();
}

void Stage::quit(int p_exit_code) {
	_THREAD_SAFE_METHOD_

	OS::get_singleton()->set_exit_code(p_exit_code);
	_quit = true;
}

void Stage::_main_window_close() {
	if (accept_quit) {
		_quit = true;
	}
}

void Stage::_main_window_go_back() {
	if (quit_on_go_back) {
		_quit = true;
	}
}

void Stage::_main_window_focus_in() {
	Input *id = Input::get_singleton();
	if (id) {
		id->ensure_touch_mouse_raised();
	}
}

void Stage::_notification(int p_notification) {
	if (!get_root()) {
		return;
	}

	switch (p_notification) {
		case NOTIFICATION_TRANSLATION_CHANGED: {
			get_root()->propagate_notification(p_notification);
		} break;

		case NOTIFICATION_OS_MEMORY_WARNING:
		case NOTIFICATION_OS_IME_UPDATE:
		case NOTIFICATION_WM_ABOUT:
		case NOTIFICATION_CRASH:
		case NOTIFICATION_APPLICATION_RESUMED:
		case NOTIFICATION_APPLICATION_PAUSED:
		case NOTIFICATION_APPLICATION_PIP_MODE_ENTERED:
		case NOTIFICATION_APPLICATION_PIP_MODE_EXITED: {
			// Pass these to nodes, since they are mirrored.
			get_root()->propagate_notification(p_notification);
		} break;

		case NOTIFICATION_APPLICATION_FOCUS_IN:
		case NOTIFICATION_APPLICATION_FOCUS_OUT: {
			if (Input::get_singleton()) {
				Input::get_singleton()->application_focused = p_notification == NOTIFICATION_APPLICATION_FOCUS_IN;

				// `release_pressed_events()` already preserves joypad state when the
				// unfocused joypad setting is disabled, but keyboard state still needs
				// to be released after focus loss.
				Input::get_singleton()->release_pressed_events();
			}

			// Pass these to nodes, since they are mirrored.
			get_root()->propagate_notification(p_notification);
		} break;
	}
}

bool Stage::is_auto_accept_quit() const {
	return accept_quit;
}

void Stage::set_auto_accept_quit(bool p_enable) {
	accept_quit = p_enable;
}

bool Stage::is_quit_on_go_back() const {
	return quit_on_go_back;
}

void Stage::set_quit_on_go_back(bool p_enable) {
	quit_on_go_back = p_enable;
}

#ifdef DEBUG_ENABLED
void Stage::set_debug_collisions_hint(bool p_enabled) {
#ifndef PHYSICS_PLANAR_DISABLED
	PhysicsSystem2D::get_singleton()->debug_set_enabled(p_enabled);
#endif
#ifndef PHYSICS_SPATIAL_DISABLED
	PhysicsSystem3D::get_singleton()->debug_set_enabled(p_enabled);
#endif
}

bool Stage::is_debugging_collisions_hint() const {
#ifndef PHYSICS_PLANAR_DISABLED
	if (PhysicsSystem2D::get_singleton()->debug_is_enabled()) {
		return true;
	}
#endif
#ifndef PHYSICS_SPATIAL_DISABLED
	if (PhysicsSystem3D::get_singleton()->debug_is_enabled()) {
		return true;
	}
#endif
	return false;
}

void Stage::set_debug_paths_hint(bool p_enabled) {
	debug_paths_hint = p_enabled;
}

bool Stage::is_debugging_paths_hint() const {
	return debug_paths_hint;
}

void Stage::set_debug_navigation_hint(bool p_enabled) {
	debug_navigation_hint = p_enabled;
}

bool Stage::is_debugging_navigation_hint() const {
	return debug_navigation_hint;
}
#endif

void Stage::set_debug_collisions_color(const Color &p_color) {
	debug_collisions_color = p_color;
}

Color Stage::get_debug_collisions_color() const {
	return debug_collisions_color;
}

void Stage::set_debug_collision_contact_color(const Color &p_color) {
	debug_collision_contact_color = p_color;
}

Color Stage::get_debug_collision_contact_color() const {
	return debug_collision_contact_color;
}

void Stage::set_debug_paths_color(const Color &p_color) {
	debug_paths_color = p_color;
}

Color Stage::get_debug_paths_color() const {
	return debug_paths_color;
}

void Stage::set_debug_paths_width(float p_width) {
	debug_paths_width = p_width;
}

float Stage::get_debug_paths_width() const {
	return debug_paths_width;
}

Ref<Material> Stage::get_debug_paths_material() {
	_THREAD_SAFE_METHOD_

	if (debug_paths_material.is_valid()) {
		return debug_paths_material;
	}

	Ref<StandardMaterial3D> _debug_material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	_debug_material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	_debug_material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	_debug_material->set_flag(StandardMaterial3D::FLAG_SRGB_VERTEX_COLOR, true);
	_debug_material->set_flag(StandardMaterial3D::FLAG_ALBEDO_FROM_VERTEX_COLOR, true);
	_debug_material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	_debug_material->set_albedo(get_debug_paths_color());

	debug_paths_material = _debug_material;

	return debug_paths_material;
}

Ref<Material> Stage::get_debug_collision_material() {
	_THREAD_SAFE_METHOD_

	if (collision_material.is_valid()) {
		return collision_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MIN + 1);
	material->set_cull_mode(StandardMaterial3D::CULL_BACK);
	material->set_flag(StandardMaterial3D::FLAG_SRGB_VERTEX_COLOR, true);
	material->set_flag(StandardMaterial3D::FLAG_ALBEDO_FROM_VERTEX_COLOR, true);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);

	collision_material = material;

	return collision_material;
}

Ref<ArrayMesh> Stage::get_debug_contact_mesh() {
	_THREAD_SAFE_METHOD_

	if (debug_contact_mesh.is_valid()) {
		return debug_contact_mesh;
	}

	debug_contact_mesh.instantiate();

	Ref<StandardMaterial3D> mat = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	mat->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	mat->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	mat->set_flag(StandardMaterial3D::FLAG_SRGB_VERTEX_COLOR, true);
	mat->set_flag(StandardMaterial3D::FLAG_ALBEDO_FROM_VERTEX_COLOR, true);
	mat->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	mat->set_albedo(get_debug_collision_contact_color());

	Vector3 diamond[6] = {
		Vector3(-1, 0, 0),
		Vector3(1, 0, 0),
		Vector3(0, -1, 0),
		Vector3(0, 1, 0),
		Vector3(0, 0, -1),
		Vector3(0, 0, 1)
	};

	/* clang-format off */
	int diamond_faces[8 * 3] = {
		0, 2, 4,
		0, 3, 4,
		1, 2, 4,
		1, 3, 4,
		0, 2, 5,
		0, 3, 5,
		1, 2, 5,
		1, 3, 5,
	};
	/* clang-format on */

	Vector<int> indices;
	for (int i = 0; i < 8 * 3; i++) {
		indices.push_back(diamond_faces[i]);
	}

	Vector<Vector3> vertices;
	for (int i = 0; i < 6; i++) {
		vertices.push_back(diamond[i] * 0.1);
	}

	Array arr;
	arr.resize(Mesh::ARRAY_MAX);
	arr[Mesh::ARRAY_VERTEX] = vertices;
	arr[Mesh::ARRAY_INDEX] = indices;

	debug_contact_mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, arr);
	debug_contact_mesh->surface_set_material(0, mat);

	return debug_contact_mesh;
}

void Stage::set_pause(bool p_enabled) {
	ERR_FAIL_COND_MSG(!Thread::is_main_thread(), "Pause can only be set from the main thread.");
	ERR_FAIL_COND_MSG(suspended, "Pause state cannot be modified while suspended.");

	if (p_enabled == paused) {
		return;
	}

	paused = p_enabled;

#ifndef PHYSICS_SPATIAL_DISABLED
	PhysicsSystem3D::get_singleton()->set_active(!p_enabled);
#endif // PHYSICS_SPATIAL_DISABLED
#ifndef PHYSICS_PLANAR_DISABLED
	PhysicsSystem2D::get_singleton()->set_active(!p_enabled);
#endif // PHYSICS_PLANAR_DISABLED
	if (get_root()) {
		get_root()->_propagate_pause_notification(p_enabled);
	}
}

bool Stage::is_paused() const {
	return paused;
}

void Stage::set_suspend(bool p_enabled) {
	ERR_FAIL_COND_MSG(!Thread::is_main_thread(), "Suspend can only be set from the main thread.");

	if (p_enabled == suspended) {
		return;
	}

	suspended = p_enabled;

	Engine::get_singleton()->set_freeze_time_scale(p_enabled);

#ifndef PHYSICS_SPATIAL_DISABLED
	PhysicsSystem3D::get_singleton()->set_active(!p_enabled && !paused);
#endif // PHYSICS_SPATIAL_DISABLED
#ifndef PHYSICS_PLANAR_DISABLED
	PhysicsSystem2D::get_singleton()->set_active(!p_enabled && !paused);
#endif // PHYSICS_PLANAR_DISABLED
	if (get_root()) {
		get_root()->_propagate_suspend_notification(p_enabled);
	}
}

bool Stage::is_suspended() const {
	return suspended;
}

void Stage::_process_group(ProcessGroup *p_group, bool p_physics) {
	// When reading this function, keep in mind that this code must work in a way where
	// if any node is removed, this needs to continue working.

	p_group->call_queue.flush(); // Flush messages before processing.

	Vector<Entity *> &nodes = p_physics ? p_group->physics_nodes : p_group->nodes;
	if (nodes.is_empty()) {
		return;
	}

	if (p_physics) {
		if (p_group->physics_node_order_dirty) {
			nodes.sort_custom<Entity::ComparatorWithPhysicsPriority>();
			p_group->physics_node_order_dirty = false;
		}
	} else {
		if (p_group->node_order_dirty) {
			nodes.sort_custom<Entity::ComparatorWithPriority>();
			p_group->node_order_dirty = false;
		}
	}

	// Make a copy, so if nodes are added/removed from process, this does not break
	Vector<Entity *> nodes_copy = nodes;

	uint32_t node_count = nodes_copy.size();
	Entity **nodes_ptr = (Entity **)nodes_copy.ptr(); // Force cast, pointer will not change.

	for (uint32_t i = 0; i < node_count; i++) {
		Entity *n = nodes_ptr[i];
		if (nodes_removed_on_group_call.has(n)) {
			// Entity may have been removed during process, skip it.
			// Keep in mind removals can only happen on the main thread.
			continue;
		}

		if (!n->can_run() || !n->is_inside_stage()) {
			continue;
		}

		if (p_physics) {
			if (n->is_internal_step_enabled()) {
				n->notification(Entity::NOTIFICATION_INTERNAL_STEP);
			}
			if (n->is_step_enabled()) {
				n->notification(Entity::NOTIFICATION_STEP);
			}
		} else {
			if (n->is_internal_frame_enabled()) {
				n->notification(Entity::NOTIFICATION_INTERNAL_FRAME);
			}
			if (n->is_frame_enabled()) {
				n->notification(Entity::NOTIFICATION_FRAME);
			}
		}
	}

	p_group->call_queue.flush(); // Flush messages also after processing (for potential deferred calls).
}

void Stage::_process_groups_thread(uint32_t p_index, bool p_physics) {
	Entity::current_run_thread_group = local_process_group_cache[p_index]->owner;
	_process_group(local_process_group_cache[p_index], p_physics);
	Entity::current_run_thread_group = nullptr;
}

void Stage::_process(bool p_physics) {
	if (process_groups_dirty) {
		{
			// First, remove dirty groups.
			// This needs to be done when not processing to avoid problems.
			ProcessGroup **pg_ptr = (ProcessGroup **)process_groups.ptr(); // discard constness.
			uint32_t pg_count = process_groups.size();

			for (uint32_t i = 0; i < pg_count; i++) {
				if (pg_ptr[i]->removed) {
					// Replace removed with last.
					pg_ptr[i] = pg_ptr[pg_count - 1];
					// Retry
					i--;
					pg_count--;
				}
			}
			if (pg_count != process_groups.size()) {
				process_groups.resize(pg_count);
			}
		}
		{
			// Then, re-sort groups.
			process_groups.sort_custom<ProcessGroupSort>();
		}

		process_groups_dirty = false;
	}

	// Cache the group count, because during processing new groups may be added.
	// They will be added at the end, hence for consistency they will be ignored by this process loop.
	// No group will be removed from the array during processing (this is done earlier in this function by marking the groups dirty).
	uint32_t group_count = process_groups.size();

	if (group_count == 0) {
		return;
	}

	process_last_pass++; // Increment pass
	uint32_t from = 0;
	uint32_t process_count = 0;
	nodes_removed_on_group_call_lock++;

	int current_order = process_groups[0]->owner ? process_groups[0]->owner->data.run_thread_group_order : 0;
	bool current_threaded = process_groups[0]->owner ? process_groups[0]->owner->data.run_thread_group == Entity::RUN_THREAD_GROUP_SUB_THREAD : false;

	for (uint32_t i = 0; i <= group_count; i++) {
		int order = i < group_count && process_groups[i]->owner ? process_groups[i]->owner->data.run_thread_group_order : 0;
		bool threaded = i < group_count && process_groups[i]->owner ? process_groups[i]->owner->data.run_thread_group == Entity::RUN_THREAD_GROUP_SUB_THREAD : false;

		if (i == group_count || current_order != order || current_threaded != threaded) {
			if (process_count > 0) {
				// Proceed to process the group.
				bool using_threads = process_groups[from]->owner && process_groups[from]->owner->data.run_thread_group == Entity::RUN_THREAD_GROUP_SUB_THREAD && !node_threading_disabled;

				if (using_threads) {
					local_process_group_cache.clear();
				}
				for (uint32_t j = from; j < i; j++) {
					if (process_groups[j]->last_pass == process_last_pass) {
						if (using_threads) {
							local_process_group_cache.push_back(process_groups[j]);
						} else {
							_process_group(process_groups[j], p_physics);
						}
					}
				}

				if (using_threads) {
					WorkerThreadPool::GroupID id = WorkerThreadPool::get_singleton()->add_template_group_task(this, &Stage::_process_groups_thread, p_physics, local_process_group_cache.size(), -1, true);
					WorkerThreadPool::get_singleton()->wait_for_group_task_completion(id);
				}
			}

			if (i == group_count) {
				// This one is invalid, no longer process
				break;
			}

			from = i;
			current_threaded = threaded;
			current_order = order;
		}

		if (process_groups[i]->removed) {
			continue;
		}

		ProcessGroup *pg = process_groups[i];

		// Validate group for processing
		bool process_valid = false;
		if (p_physics) {
			if (!pg->physics_nodes.is_empty()) {
				process_valid = true;
			} else if ((pg == &default_process_group || (pg->owner != nullptr && pg->owner->data.run_thread_messages.has_flag(Entity::FLAG_RUN_THREAD_MESSAGES_PHYSICS))) && pg->call_queue.has_messages()) {
				process_valid = true;
			}
		} else {
			if (!pg->nodes.is_empty()) {
				process_valid = true;
			} else if ((pg == &default_process_group || (pg->owner != nullptr && pg->owner->data.run_thread_messages.has_flag(Entity::FLAG_RUN_THREAD_MESSAGES))) && pg->call_queue.has_messages()) {
				process_valid = true;
			}
		}

		if (process_valid) {
			pg->last_pass = process_last_pass; // Enable for processing
			process_count++;
		}
	}

	nodes_removed_on_group_call_lock--;
	if (nodes_removed_on_group_call_lock == 0) {
		nodes_removed_on_group_call.clear();
	}
}

bool Stage::ProcessGroupSort::operator()(const ProcessGroup *p_left, const ProcessGroup *p_right) const {
	int left_order = p_left->owner ? p_left->owner->data.run_thread_group_order : 0;
	int right_order = p_right->owner ? p_right->owner->data.run_thread_group_order : 0;

	if (left_order == right_order) {
		int left_threaded = p_left->owner != nullptr && p_left->owner->data.run_thread_group == Entity::RUN_THREAD_GROUP_SUB_THREAD ? 0 : 1;
		int right_threaded = p_right->owner != nullptr && p_right->owner->data.run_thread_group == Entity::RUN_THREAD_GROUP_SUB_THREAD ? 0 : 1;
		return left_threaded < right_threaded;
	} else {
		return left_order < right_order;
	}
}

void Stage::_remove_process_group(Entity *p_node) {
	_THREAD_SAFE_METHOD_
	ProcessGroup *pg = (ProcessGroup *)p_node->data.process_group;
	ERR_FAIL_NULL(pg);
	ERR_FAIL_COND(pg->removed);
	pg->removed = true;
	pg->owner = nullptr;
	p_node->data.process_group = nullptr;
	process_groups_dirty = true;
}

void Stage::_add_process_group(Entity *p_node) {
	_THREAD_SAFE_METHOD_
	ERR_FAIL_NULL(p_node);

	ProcessGroup *pg = memnew(ProcessGroup);

	pg->owner = p_node;
	p_node->data.process_group = pg;

	process_groups.push_back(pg);

	process_groups_dirty = true;
}

void Stage::_remove_node_from_process_group(Entity *p_node, Entity *p_owner) {
	_THREAD_SAFE_METHOD_
	ProcessGroup *pg = p_owner ? (ProcessGroup *)p_owner->data.process_group : &default_process_group;

	if (p_node->is_frame_enabled() || p_node->is_internal_frame_enabled()) {
		bool found = pg->nodes.erase(p_node);
		ERR_FAIL_COND(!found);
	}

	if (p_node->is_step_enabled() || p_node->is_internal_step_enabled()) {
		bool found = pg->physics_nodes.erase(p_node);
		ERR_FAIL_COND(!found);
	}
}

void Stage::_add_node_to_process_group(Entity *p_node, Entity *p_owner) {
	_THREAD_SAFE_METHOD_
	ProcessGroup *pg = p_owner ? (ProcessGroup *)p_owner->data.process_group : &default_process_group;

	if (p_node->is_frame_enabled() || p_node->is_internal_frame_enabled()) {
		pg->nodes.push_back(p_node);
		pg->node_order_dirty = true;
	}

	if (p_node->is_step_enabled() || p_node->is_internal_step_enabled()) {
		pg->physics_nodes.push_back(p_node);
		pg->physics_node_order_dirty = true;
	}
}

void Stage::_call_input_pause(const StringName &p_group, CallInputType p_call_type, const Ref<InputEvent> &p_input, Viewport *p_viewport) {
	Vector<Entity *> nodes_copy;
	{
		_THREAD_SAFE_METHOD_

		HashMap<StringName, StageGroup>::Iterator E = group_map.find(p_group);
		if (!E) {
			return;
		}
		StageGroup &g = E->value;
		if (g.nodes.is_empty()) {
			return;
		}

		_update_group_order(g);

		//copy, so copy on write happens in case something is removed from process while being called
		//performance is not lost because only if something is added/removed the vector is copied.
		nodes_copy = g.nodes;
	}

	int gr_node_count = nodes_copy.size();
	Entity **gr_nodes = nodes_copy.ptrw();

	{
		_THREAD_SAFE_METHOD_
		nodes_removed_on_group_call_lock++;
	}

	Vector<ObjectID> no_context_node_ids; // Nodes may be deleted due to this shortcut input.

	for (int i = gr_node_count - 1; i >= 0; i--) {
		if (p_viewport->is_input_handled()) {
			break;
		}

		Entity *n = gr_nodes[i];
		if (nodes_removed_on_group_call.has(n)) {
			continue;
		}

		if (!n->can_run()) {
			continue;
		}

		switch (p_call_type) {
			case CALL_INPUT_TYPE_INPUT:
				n->_call_input(p_input);
				break;
			case CALL_INPUT_TYPE_SHORTCUT_INPUT: {
				const Control *c = Object::cast_to<Control>(n);
				if (c) {
					// If calling shortcut input on a control, ensure it respects the shortcut context.
					// Shortcut context (based on focus) only makes sense for controls (UI), so don't need to worry about it for nodes
					if (c->get_shortcut_context() == nullptr) {
						no_context_node_ids.append(n->get_instance_id());
						continue;
					}
					if (!c->is_focus_owner_in_shortcut_context()) {
						continue;
					}
				}
				n->_call_shortcut_input(p_input);
				break;
			}
			case CALL_INPUT_TYPE_UNHANDLED_INPUT:
				n->_call_unhandled_input(p_input);
				break;
			case CALL_INPUT_TYPE_UNHANDLED_KEY_INPUT:
				n->_call_unhandled_key_input(p_input);
				break;
		}
	}

	for (const ObjectID &id : no_context_node_ids) {
		if (p_viewport->is_input_handled()) {
			break;
		}
		Entity *n = ObjectRegistry::get_instance<Entity>(id);
		if (n) {
			n->_call_shortcut_input(p_input);
		}
	}

	{
		_THREAD_SAFE_METHOD_
		nodes_removed_on_group_call_lock--;
		if (nodes_removed_on_group_call_lock == 0) {
			nodes_removed_on_group_call.clear();
		}
	}
}

void Stage::_project_settings_changed() {
	gui_theme_bus = GLOBAL_GET("audio/buses/gui_theme_bus");
}

void Stage::_call_group_flags(const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	r_error.error = Callable::CallError::CALL_OK;

	ERR_FAIL_COND(p_argcount < 3);
	ERR_FAIL_COND(!p_args[0]->is_num());
	ERR_FAIL_COND(!p_args[1]->is_string());
	ERR_FAIL_COND(!p_args[2]->is_string());

	int flags = *p_args[0];
	StringName group = *p_args[1];
	StringName method = *p_args[2];

	call_group_flagsp(flags, group, method, p_args + 3, p_argcount - 3);
}

void Stage::_call_group(const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	r_error.error = Callable::CallError::CALL_OK;

	ERR_FAIL_COND(p_argcount < 2);
	ERR_FAIL_COND(!p_args[0]->is_string());
	ERR_FAIL_COND(!p_args[1]->is_string());

	StringName group = *p_args[0];
	StringName method = *p_args[1];

	call_group_flagsp(GROUP_CALL_DEFAULT, group, method, p_args + 2, p_argcount - 2);
}

int64_t Stage::get_frame() const {
	return current_frame;
}

TypedArray<Entity> Stage::_get_nodes_in_group(const StringName &p_group) {
	_THREAD_SAFE_METHOD_
	TypedArray<Entity> ret;
	HashMap<StringName, StageGroup>::Iterator E = group_map.find(p_group);
	if (!E) {
		return ret;
	}

	_update_group_order(E->value); //update order just in case
	int nc = E->value.nodes.size();
	if (nc == 0) {
		return ret;
	}

	ret.resize(nc);

	Entity **ptr = E->value.nodes.ptrw();
	for (int i = 0; i < nc; i++) {
		ret[i] = ptr[i];
	}

	return ret;
}

bool Stage::has_group(const StringName &p_identifier) const {
	_THREAD_SAFE_METHOD_
	return group_map.has(p_identifier);
}

int Stage::get_entity_count_in_group(const StringName &p_group) const {
	_THREAD_SAFE_METHOD_
	HashMap<StringName, StageGroup>::ConstIterator E = group_map.find(p_group);
	if (!E) {
		return 0;
	}

	return E->value.nodes.size();
}

Entity *Stage::get_first_entity_in_group(const StringName &p_group) {
	_THREAD_SAFE_METHOD_
	HashMap<StringName, StageGroup>::Iterator E = group_map.find(p_group);
	if (!E) {
		return nullptr; // No group.
	}

	_update_group_order(E->value); // Update order just in case.

	if (E->value.nodes.is_empty()) {
		return nullptr;
	}

	return E->value.nodes[0];
}

Vector<Entity *> Stage::get_entities_in_group(const StringName &p_group) {
	_THREAD_SAFE_METHOD_
	HashMap<StringName, StageGroup>::Iterator E = group_map.find(p_group);
	if (!E) {
		return {};
	}

	_update_group_order(E->value); //update order just in case
	int nc = E->value.nodes.size();
	if (nc == 0) {
		return {};
	}

	return E->value.nodes;
}

void Stage::_flush_delete_queue() {
	_THREAD_SAFE_METHOD_

	while (delete_queue.size()) {
		Object *obj = ObjectRegistry::get_instance(delete_queue.front()->get());
		memdelete(obj);
		delete_queue.pop_front();
	}
}

void Stage::queue_delete(RequiredParam<Object> p_object) {
	_THREAD_SAFE_METHOD_
	EXTRACT_PARAM_OR_FAIL(object, p_object);
	object->_is_queued_for_deletion = true;
	delete_queue.push_back(object->get_instance_id());
}

int Stage::get_entity_count() const {
	return nodes_in_tree_count;
}

void Stage::set_edited_scene_root(Entity *p_node) {
#ifdef TOOLS_ENABLED
	edited_scene_root = p_node;
#endif
}

Entity *Stage::get_edited_scene_root() const {
#ifdef TOOLS_ENABLED
	return edited_scene_root;
#else
	return nullptr;
#endif
}

void Stage::set_current_scene(Entity *p_scene) {
	ERR_FAIL_COND_MSG(!Thread::is_main_thread(), "Changing scene can only be done from the main thread.");
	ERR_FAIL_COND(p_scene && p_scene->get_parent() != root);
	current_scene = p_scene;
}

Entity *Stage::get_current_scene() const {
	return current_scene;
}

void Stage::_flush_scene_change() {
	if (prev_scene_id.is_valid()) {
		// Might have already been freed externally.
		Entity *prev_scene = ObjectRegistry::get_instance<Entity>(prev_scene_id);
		memdelete(prev_scene);
		prev_scene_id = ObjectID();
	}

	DEV_ASSERT(pending_new_scene_id.is_valid());
	Entity *pending_new_scene = ObjectRegistry::get_instance<Entity>(pending_new_scene_id);
	if (pending_new_scene) {
		// Ensure correct state before `add_child` (might enqueue subsequent scene change).
		current_scene = pending_new_scene;
		pending_new_scene_id = ObjectID();

		root->add_child(pending_new_scene);
		// Update display for cursor instantly.
		root->update_mouse_cursor_state();

		// Only on successful scene change.
		emit_signal(SNAME("scene_changed"));
	} else {
		current_scene = nullptr;
		pending_new_scene_id = ObjectID();
		ERR_PRINT("Scene instance has been freed before becoming the current scene. No current scene is set.");
	}
}

Error Stage::change_scene_to_file(const String &p_path) {
	ERR_FAIL_COND_V_MSG(!Thread::is_main_thread(), ERR_INVALID_PARAMETER, "Changing scene can only be done from the main thread.");
	Ref<Prefab> new_scene = ResourceLoader::load(p_path);
	if (new_scene.is_null()) {
		return ERR_CANT_OPEN;
	}

	return change_scene_to_prefab(new_scene);
}

Error Stage::change_scene_to_prefab(RequiredParam<Prefab> p_scene) {
	EXTRACT_PARAM_OR_FAIL_V_MSG(scene, p_scene, ERR_INVALID_PARAMETER, "Can't change to a null scene. Use unload_current_scene() if you wish to unload it.");

	Entity *new_scene = scene->instantiate();
	ERR_FAIL_NULL_V(new_scene, ERR_CANT_CREATE);

	return change_scene_to_entity(new_scene);
}

Error Stage::change_scene_to_entity(RequiredParam<Entity> p_node) {
	EXTRACT_PARAM_OR_FAIL_V_MSG(node, p_node, ERR_INVALID_PARAMETER, "Can't change to a null node. Use unload_current_scene() if you wish to unload it.");
	ERR_FAIL_COND_V_MSG(node->is_inside_stage(), ERR_UNCONFIGURED, "The new scene node can't already be inside scene tree.");

	// If called again while a change is pending.
	if (pending_new_scene_id.is_valid()) {
		Entity *pending_new_scene = ObjectRegistry::get_instance<Entity>(pending_new_scene_id);
		if (pending_new_scene) {
			queue_delete(pending_new_scene);
		}
		pending_new_scene_id = ObjectID();
	}

	if (current_scene) {
		prev_scene_id = current_scene->get_instance_id();
		// Let as many side effects as possible happen or be queued now,
		// so they are run before the scene is actually deleted.
		root->remove_child(current_scene);
	}
	DEV_ASSERT(!current_scene);

	pending_new_scene_id = node->get_instance_id();
	return OK;
}

Error Stage::reload_current_scene() {
	ERR_FAIL_COND_V_MSG(!Thread::is_main_thread(), ERR_INVALID_PARAMETER, "Reloading scene can only be done from the main thread.");
	ERR_FAIL_NULL_V(current_scene, ERR_UNCONFIGURED);
	String fname = current_scene->get_scene_file_path();
	return change_scene_to_file(fname);
}

void Stage::unload_current_scene() {
	ERR_FAIL_COND_MSG(!Thread::is_main_thread(), "Unloading the current scene can only be done from the main thread.");
	if (current_scene) {
		memdelete(current_scene);
		current_scene = nullptr;
	}
}

void Stage::add_current_scene(Entity *p_current) {
	ERR_FAIL_COND_MSG(!Thread::is_main_thread(), "Adding a current scene can only be done from the main thread.");
	current_scene = p_current;
	root->add_child(p_current);
}

RequiredResult<StageTimer> Stage::create_timer(double p_delay_sec, bool p_process_always, bool p_process_in_physics, bool p_ignore_time_scale) {
	_THREAD_SAFE_METHOD_
	Ref<StageTimer> stt;
	stt.instantiate();
	stt->set_process_always(p_process_always);
	stt->set_time_left(p_delay_sec);
	stt->set_process_in_physics(p_process_in_physics);
	stt->set_ignore_time_scale(p_ignore_time_scale);
	timers.push_back(stt);
	return stt;
}

RequiredResult<Tween> Stage::create_tween() {
	_THREAD_SAFE_METHOD_
	Ref<Tween> tween;
	tween.instantiate(this);
	tweens.push_back(tween);
	return tween;
}

void Stage::remove_tween(const Ref<Tween> &p_tween) {
	_THREAD_SAFE_METHOD_
	for (List<Ref<Tween>>::Element *E = tweens.back(); E; E = E->prev()) {
		if (E->get() == p_tween) {
			E->erase();
			break;
		}
	}
}

TypedArray<Tween> Stage::get_processed_tweens() {
	_THREAD_SAFE_METHOD_
	TypedArray<Tween> ret;
	ret.resize(tweens.size());

	int i = 0;
	for (const Ref<Tween> &tween : tweens) {
		ret[i] = tween;
		i++;
	}

	return ret;
}

void Stage::play_theme_sound(const Ref<AudioStream> &p_stream) {
	AudioStreamPlayer *audio_stream_player = memnew(AudioStreamPlayer);
	audio_stream_player->set_name(SNAME("_theme_sound"));
	audio_stream_player->set_bus(gui_theme_bus);
	audio_stream_player->set_stream(p_stream);
	audio_stream_player->set_autoplay(true);
	audio_stream_player->set_run_mode(Entity::RUN_MODE_ALWAYS);
	audio_stream_player->connect(SceneStringName(finished), callable_mp((Entity *)audio_stream_player, &Entity::queue_destroy));
	root->add_child(audio_stream_player);
}

RequiredResult<MultiplayerAPI> Stage::get_multiplayer(const EntityPath &p_for_path) const {
	ERR_FAIL_COND_V_MSG(!Thread::is_main_thread(), Ref<MultiplayerAPI>(), "Multiplayer can only be manipulated from the main thread.");
	if (p_for_path.is_empty()) {
		return multiplayer;
	}

	const Vector<StringName> tnames = p_for_path.get_names();
	const StringName *nptr = tnames.ptr();
	for (const KeyValue<EntityPath, Ref<MultiplayerAPI>> &E : custom_multiplayers) {
		const Vector<StringName> snames = E.key.get_names();
		if (tnames.size() < snames.size()) {
			continue;
		}
		const StringName *sptr = snames.ptr();
		bool valid = true;
		for (int i = 0; i < snames.size(); i++) {
			if (sptr[i] != nptr[i]) {
				valid = false;
				break;
			}
		}
		if (valid) {
			return E.value;
		}
	}

	return multiplayer;
}

void Stage::set_multiplayer(Ref<MultiplayerAPI> p_multiplayer, const EntityPath &p_root_path) {
	ERR_FAIL_COND_MSG(!Thread::is_main_thread(), "Multiplayer can only be manipulated from the main thread.");
	if (p_root_path.is_empty()) {
		ERR_FAIL_COND(p_multiplayer.is_null());
		if (multiplayer.is_valid()) {
			multiplayer->object_configuration_remove(nullptr, EntityPath("/" + root->get_name()));
		}
		multiplayer = p_multiplayer;
		multiplayer->object_configuration_add(nullptr, EntityPath("/" + root->get_name()));
	} else {
		if (custom_multiplayers.has(p_root_path)) {
			custom_multiplayers[p_root_path]->object_configuration_remove(nullptr, p_root_path);
		} else if (p_multiplayer.is_valid()) {
			const Vector<StringName> tnames = p_root_path.get_names();
			const StringName *nptr = tnames.ptr();
			for (const KeyValue<EntityPath, Ref<MultiplayerAPI>> &E : custom_multiplayers) {
				const Vector<StringName> snames = E.key.get_names();
				if (tnames.size() < snames.size()) {
					continue;
				}
				const StringName *sptr = snames.ptr();
				bool valid = true;
				for (int i = 0; i < snames.size(); i++) {
					if (sptr[i] != nptr[i]) {
						valid = false;
						break;
					}
				}
				ERR_FAIL_COND_MSG(valid, "Multiplayer is already configured for a parent of this path: '" + String(p_root_path) + "' in '" + String(E.key) + "'.");
			}
		}
		if (p_multiplayer.is_valid()) {
			custom_multiplayers[p_root_path] = p_multiplayer;
			p_multiplayer->object_configuration_add(nullptr, p_root_path);
		} else {
			custom_multiplayers.erase(p_root_path);
		}
	}
}

void Stage::set_multiplayer_poll_enabled(bool p_enabled) {
	ERR_FAIL_COND_MSG(!Thread::is_main_thread(), "Multiplayer can only be manipulated from the main thread.");
	multiplayer_poll = p_enabled;
}

bool Stage::is_multiplayer_poll_enabled() const {
	return multiplayer_poll;
}

void Stage::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_root"), &Stage::get_root);
	ClassRegistry::bind_method(D_METHOD("has_group", "name"), &Stage::has_group);

	ClassRegistry::bind_method(D_METHOD("is_accessibility_enabled"), &Stage::is_accessibility_enabled);
	ClassRegistry::bind_method(D_METHOD("is_accessibility_supported"), &Stage::is_accessibility_supported);

	ClassRegistry::bind_method(D_METHOD("is_auto_accept_quit"), &Stage::is_auto_accept_quit);
	ClassRegistry::bind_method(D_METHOD("set_auto_accept_quit", "enabled"), &Stage::set_auto_accept_quit);
	ClassRegistry::bind_method(D_METHOD("is_quit_on_go_back"), &Stage::is_quit_on_go_back);
	ClassRegistry::bind_method(D_METHOD("set_quit_on_go_back", "enabled"), &Stage::set_quit_on_go_back);

	ClassRegistry::bind_method(D_METHOD("set_debug_collisions_hint", "enable"), &Stage::set_debug_collisions_hint);
	ClassRegistry::bind_method(D_METHOD("is_debugging_collisions_hint"), &Stage::is_debugging_collisions_hint);
	ClassRegistry::bind_method(D_METHOD("set_debug_paths_hint", "enable"), &Stage::set_debug_paths_hint);
	ClassRegistry::bind_method(D_METHOD("is_debugging_paths_hint"), &Stage::is_debugging_paths_hint);
	ClassRegistry::bind_method(D_METHOD("set_debug_navigation_hint", "enable"), &Stage::set_debug_navigation_hint);
	ClassRegistry::bind_method(D_METHOD("is_debugging_navigation_hint"), &Stage::is_debugging_navigation_hint);

	ClassRegistry::bind_method(D_METHOD("set_edited_scene_root", "scene"), &Stage::set_edited_scene_root);
	ClassRegistry::bind_method(D_METHOD("get_edited_scene_root"), &Stage::get_edited_scene_root);

	ClassRegistry::bind_method(D_METHOD("set_pause", "enable"), &Stage::set_pause);
	ClassRegistry::bind_method(D_METHOD("is_paused"), &Stage::is_paused);

	ClassRegistry::bind_method(D_METHOD("create_timer", "time_sec", "process_always", "process_in_physics", "ignore_time_scale"), &Stage::create_timer, DEFVAL(true), DEFVAL(false), DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("create_tween"), &Stage::create_tween);
	ClassRegistry::bind_method(D_METHOD("get_processed_tweens"), &Stage::get_processed_tweens);

	ClassRegistry::bind_method(D_METHOD("get_entity_count"), &Stage::get_entity_count);
	ClassRegistry::bind_method(D_METHOD("get_frame"), &Stage::get_frame);
	ClassRegistry::bind_method(D_METHOD("quit", "exit_code"), &Stage::quit, DEFVAL(EXIT_SUCCESS));

	ClassRegistry::bind_method(D_METHOD("set_physics_interpolation_enabled", "enabled"), &Stage::set_physics_interpolation_enabled);
	ClassRegistry::bind_method(D_METHOD("is_physics_interpolation_enabled"), &Stage::is_physics_interpolation_enabled);

	ClassRegistry::bind_method(D_METHOD("queue_delete", "obj"), &Stage::queue_delete);

	MethodInfo mi;
	mi.name = "call_group_flags";
	mi.arguments.push_back(PropertyInfo(Variant::INT, "flags"));
	mi.arguments.push_back(PropertyInfo(Variant::STRING_NAME, "group"));
	mi.arguments.push_back(PropertyInfo(Variant::STRING_NAME, "method"));

	ClassRegistry::bind_vararg_method(METHOD_FLAGS_DEFAULT, "call_group_flags", &Stage::_call_group_flags, mi);

	ClassRegistry::bind_method(D_METHOD("notify_group_flags", "call_flags", "group", "notification"), &Stage::notify_group_flags);
	ClassRegistry::bind_method(D_METHOD("set_group_flags", "call_flags", "group", "property", "value"), &Stage::set_group_flags);

	MethodInfo mi2;
	mi2.name = "call_group";
	mi2.arguments.push_back(PropertyInfo(Variant::STRING_NAME, "group"));
	mi2.arguments.push_back(PropertyInfo(Variant::STRING_NAME, "method"));

	ClassRegistry::bind_vararg_method(METHOD_FLAGS_DEFAULT, "call_group", &Stage::_call_group, mi2);

	ClassRegistry::bind_method(D_METHOD("notify_group", "group", "notification"), &Stage::notify_group);
	ClassRegistry::bind_method(D_METHOD("set_group", "group", "property", "value"), &Stage::set_group);

	ClassRegistry::bind_method(D_METHOD("get_entities_in_group", "group"), &Stage::_get_nodes_in_group);
	ClassRegistry::bind_method(D_METHOD("get_first_entity_in_group", "group"), &Stage::get_first_entity_in_group);
	ClassRegistry::bind_method(D_METHOD("get_entity_count_in_group", "group"), &Stage::get_entity_count_in_group);

	ClassRegistry::bind_method(D_METHOD("set_current_scene", "child_node"), &Stage::set_current_scene);
	ClassRegistry::bind_method(D_METHOD("get_current_scene"), &Stage::get_current_scene);

	ClassRegistry::bind_method(D_METHOD("change_scene_to_file", "path"), &Stage::change_scene_to_file);
	ClassRegistry::bind_method(D_METHOD("change_scene_to_prefab", "prefab"), &Stage::change_scene_to_prefab);
	ClassRegistry::bind_method(D_METHOD("change_scene_to_entity", "node"), &Stage::change_scene_to_entity);

	ClassRegistry::bind_method(D_METHOD("reload_current_scene"), &Stage::reload_current_scene);
	ClassRegistry::bind_method(D_METHOD("unload_current_scene"), &Stage::unload_current_scene);

	ClassRegistry::bind_method(D_METHOD("set_multiplayer", "multiplayer", "root_path"), &Stage::set_multiplayer, DEFVAL(EntityPath()));
	ClassRegistry::bind_method(D_METHOD("get_multiplayer", "for_path"), &Stage::get_multiplayer, DEFVAL(EntityPath()));
	ClassRegistry::bind_method(D_METHOD("set_multiplayer_poll_enabled", "enabled"), &Stage::set_multiplayer_poll_enabled);
	ClassRegistry::bind_method(D_METHOD("is_multiplayer_poll_enabled"), &Stage::is_multiplayer_poll_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "auto_accept_quit"), "set_auto_accept_quit", "is_auto_accept_quit");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "quit_on_go_back"), "set_quit_on_go_back", "is_quit_on_go_back");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "debug_collisions_hint"), "set_debug_collisions_hint", "is_debugging_collisions_hint");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "debug_paths_hint"), "set_debug_paths_hint", "is_debugging_paths_hint");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "debug_navigation_hint"), "set_debug_navigation_hint", "is_debugging_navigation_hint");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "paused"), "set_pause", "is_paused");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "edited_scene_root", PROPERTY_HINT_RESOURCE_TYPE, Entity::get_static_class_name(), PROPERTY_USAGE_NONE), "set_edited_scene_root", "get_edited_scene_root");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "current_scene", PROPERTY_HINT_RESOURCE_TYPE, Entity::get_static_class_name(), PROPERTY_USAGE_NONE), "set_current_scene", "get_current_scene");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "root", PROPERTY_HINT_RESOURCE_TYPE, Entity::get_static_class_name(), PROPERTY_USAGE_NONE), "", "get_root");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "multiplayer_poll"), "set_multiplayer_poll_enabled", "is_multiplayer_poll_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "physics_interpolation"), "set_physics_interpolation_enabled", "is_physics_interpolation_enabled");

	ADD_SIGNAL(MethodInfo("stage_changed"));
	ADD_SIGNAL(MethodInfo("scene_changed"));
	ADD_SIGNAL(MethodInfo("stage_run_mode_changed")); //editor only signal, but due to API hash it can't be removed in run-time
	ADD_SIGNAL(MethodInfo("entity_added", PropertyInfo(Variant::OBJECT, "node", PROPERTY_HINT_RESOURCE_TYPE, Entity::get_static_class_name())));
	ADD_SIGNAL(MethodInfo("entity_removed", PropertyInfo(Variant::OBJECT, "node", PROPERTY_HINT_RESOURCE_TYPE, Entity::get_static_class_name())));
	ADD_SIGNAL(MethodInfo("entity_renamed", PropertyInfo(Variant::OBJECT, "node", PROPERTY_HINT_RESOURCE_TYPE, Entity::get_static_class_name())));
	ADD_SIGNAL(MethodInfo("entity_configuration_warning_changed", PropertyInfo(Variant::OBJECT, "node", PROPERTY_HINT_RESOURCE_TYPE, Entity::get_static_class_name())));

	ADD_SIGNAL(MethodInfo("process_frame"));
	ADD_SIGNAL(MethodInfo("physics_frame"));

	BIND_ENUM_CONSTANT(GROUP_CALL_DEFAULT);
	BIND_ENUM_CONSTANT(GROUP_CALL_REVERSE);
	BIND_ENUM_CONSTANT(GROUP_CALL_DEFERRED);
	BIND_ENUM_CONSTANT(GROUP_CALL_UNIQUE);
}

Stage *Stage::singleton = nullptr;

Stage::IdleCallback Stage::idle_callbacks[Stage::MAX_IDLE_CALLBACKS];
int Stage::idle_callback_count = 0;

void Stage::_call_idle_callbacks() {
	for (int i = 0; i < idle_callback_count; i++) {
		idle_callbacks[i]();
	}
}

void Stage::add_idle_callback(IdleCallback p_callback) {
	ERR_FAIL_COND(idle_callback_count >= MAX_IDLE_CALLBACKS);
	idle_callbacks[idle_callback_count++] = p_callback;
}

#ifdef TOOLS_ENABLED
void Stage::get_argument_options(const StringName &p_function, int p_idx, List<String> *r_options) const {
	bool add_options = false;
	if (p_idx == 0) {
		static const Vector<StringName> names = {
			StringName("add_to_group", true),
			StringName("call_group", true),
			StringName("get_first_entity_in_group", true),
			StringName("get_entity_count_in_group", true),
			StringName("get_entities_in_group", true),
			StringName("has_group", true),
			StringName("notify_group", true),
			StringName("set_group", true),
		};
		add_options = names.has(p_function);
	} else if (p_idx == 1) {
		static const Vector<StringName> names = {
			StringName("call_group_flags", true),
			StringName("notify_group_flags", true),
			StringName("set_group_flags", true),
		};
		add_options = names.has(p_function);
	}
	if (add_options) {
		HashMap<StringName, String> global_groups(ProjectSettings::get_singleton()->get_global_groups_list());
		for (const KeyValue<StringName, String> &E : global_groups) {
			r_options->push_back(E.key.string().quote());
		}
	}
	MainLoop::get_argument_options(p_function, p_idx, r_options);
}
#endif

void Stage::set_disable_node_threading(bool p_disable) {
	node_threading_disabled = p_disable;
}

Stage::Stage() {
	if (singleton == nullptr) {
		singleton = this;
	}
	debug_collisions_color = GLOBAL_DEF("debug/shapes/collision/shape_color", Color(0.0, 0.6, 0.7, 0.42));
	debug_collision_contact_color = GLOBAL_DEF("debug/shapes/collision/contact_color", Color(1.0, 0.2, 0.1, 0.8));
	debug_paths_color = GLOBAL_DEF("debug/shapes/paths/geometry_color", Color(0.1, 1.0, 0.7, 0.4));
	debug_paths_width = GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "debug/shapes/paths/geometry_width", PROPERTY_HINT_RANGE, "0.01,10,0.001,or_greater"), 2.0);
	collision_debug_contacts = GLOBAL_DEF(PropertyInfo(Variant::INT, "debug/shapes/collision/max_contacts_displayed", PROPERTY_HINT_RANGE, "0,20000,1"), 10000);
	accessibility_upd_per_sec = GLOBAL_GET(SNAME("accessibility/general/updates_per_second"));
	gui_theme_bus = GLOBAL_GET("audio/buses/gui_theme_bus");

	GLOBAL_DEF("debug/shapes/collision/draw_2d_outlines", true);

	process_group_call_queue_allocator = memnew(CallQueue::Allocator(64));
	Math::randomize();

	// Create with mainloop.

	root = memnew(Window);
	root->set_min_size(Size2i(64, 64)); // Define a very small minimum window size to prevent bugs such as GH-37242.
	root->set_run_mode(Entity::RUN_MODE_PAUSABLE);
	root->set_name("root");

	if (Engine::get_singleton()->is_editor_hint()) {
		root->set_wrap_controls(true);
		root->set_auto_translate_mode(Entity::AUTO_TRANSLATE_MODE_ALWAYS);
	} else {
		root->set_auto_translate_mode(GLOBAL_GET("internationalization/rendering/root_node_auto_translate") ? Entity::AUTO_TRANSLATE_MODE_ALWAYS : Entity::AUTO_TRANSLATE_MODE_DISABLED);
	}

	// Set after auto translate mode to avoid changing the displayed title back and forth.
	root->set_title(GLOBAL_GET("application/config/name"));

#ifndef SPATIAL_DISABLED
	if (root->get_world_3d().is_null()) {
		root->set_world_3d(Ref<World3D>(memnew(World3D)));
	}
	root->set_as_audio_listener_3d(true);
#endif // SPATIAL_DISABLED

	set_physics_interpolation_enabled(GLOBAL_DEF("physics/common/physics_interpolation", false));

	// Always disable jitter fix if physics interpolation is enabled -
	// Jitter fix will interfere with interpolation, and is not necessary
	// when interpolation is active.
	if (is_physics_interpolation_enabled()) {
		Engine::get_singleton()->set_physics_jitter_fix(0);
	}

	// Initialize network state.
	set_multiplayer(MultiplayerAPI::create_default_interface());

#ifndef PLANAR_DISABLED
	root->set_as_audio_listener_2d(true);
#endif // PLANAR_DISABLED
	current_scene = nullptr;

	const int msaa_mode_2d = GLOBAL_GET("rendering/anti_aliasing/quality/msaa_planar");
	root->set_msaa_2d(Viewport::MSAA(msaa_mode_2d));

	const int msaa_mode_3d = GLOBAL_GET("rendering/anti_aliasing/quality/msaa_spatial");
	root->set_msaa_3d(Viewport::MSAA(msaa_mode_3d));

	const bool transparent_background = GLOBAL_DEF("rendering/viewport/transparent_background", false);
	root->set_transparent_background(transparent_background);

	// Enable HDR if requested.
	const bool hdr_requested = GLOBAL_GET("display/window/hdr/request_hdr_output");
	DisplaySystem::get_singleton()->window_request_hdr_output(hdr_requested);

	const bool use_hdr_2d = GLOBAL_GET("rendering/viewport/hdr_planar");
	root->set_use_hdr_2d(use_hdr_2d || hdr_requested);

	if (hdr_requested && !use_hdr_2d) {
		WARN_PRINT_ED("HDR 2D was automatically enabled because HDR output was requested in project settings. To avoid this warning, enable rendering/viewport/hdr_planar in the Project Settings.");
	}

	const int ssaa_mode = GLOBAL_DEF_BASIC(PropertyInfo(Variant::INT, "rendering/anti_aliasing/quality/screen_space_aa", PROPERTY_HINT_ENUM, "Disabled (Fastest),FXAA (Fast),SMAA (Average)"), 0);
	root->set_screen_space_aa(Viewport::ScreenSpaceAA(ssaa_mode));

	const bool use_taa = GLOBAL_DEF_BASIC("rendering/anti_aliasing/quality/use_taa", false);
	root->set_use_taa(use_taa);

	const bool use_debanding = GLOBAL_GET("rendering/anti_aliasing/quality/use_debanding");
	root->set_use_debanding(use_debanding);

	const bool use_occlusion_culling = GLOBAL_DEF("rendering/occlusion_culling/use_occlusion_culling", false);
	root->set_use_occlusion_culling(use_occlusion_culling);

	float mesh_lod_threshold = GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "rendering/mesh_lod/lod_change/threshold_pixels", PROPERTY_HINT_RANGE, "0,1024,0.1"), 1.0);
	root->set_mesh_lod_threshold(mesh_lod_threshold);

	bool snap_2d_transforms = GLOBAL_DEF_BASIC("rendering/planar/snap/snap_2d_transforms_to_pixel", false);
	root->set_snap_2d_transforms_to_pixel(snap_2d_transforms);

	bool snap_2d_vertices = GLOBAL_DEF("rendering/planar/snap/snap_2d_vertices_to_pixel", false);
	root->set_snap_2d_vertices_to_pixel(snap_2d_vertices);

	// We setup VRS for the main viewport here, in the editor this will have little effect.
	const int vrs_mode = GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/vrs/mode", PROPERTY_HINT_ENUM, String::utf8("Disabled,Texture,XR")), 0);
	root->set_vrs_mode(Viewport::VRSMode(vrs_mode));
	const String vrs_texture_path = String(GLOBAL_DEF(PropertyInfo(Variant::STRING, "rendering/vrs/texture", PROPERTY_HINT_FILE, "*.bmp,*.png,*.tga,*.webp"), String())).strip_edges();
	if (vrs_mode == 1 && !vrs_texture_path.is_empty()) {
		Ref<Image> vrs_image;
		vrs_image.instantiate();
		Error load_err = ImageLoader::load_image(vrs_texture_path, vrs_image);
		if (load_err) {
			ERR_PRINT("Non-existing or invalid VRS texture at '" + vrs_texture_path + "'.");
		} else {
			root->set_vrs_texture(ImageTexture::create_from_image(vrs_image));
		}
	}

	int shadowmap_size = GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/lights_and_shadows/positional_shadow/atlas_size", PROPERTY_HINT_RANGE, "256,16384"), 4096);
	GLOBAL_DEF("rendering/lights_and_shadows/positional_shadow/atlas_size.mobile", 2048);
	bool shadowmap_16_bits = GLOBAL_GET("rendering/lights_and_shadows/positional_shadow/atlas_16_bits");
	int atlas_q0 = GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/lights_and_shadows/positional_shadow/atlas_quadrant_0_subdiv", PROPERTY_HINT_ENUM, "Disabled,1 Shadow,4 Shadows,16 Shadows,64 Shadows,256 Shadows,1024 Shadows"), 2);
	int atlas_q1 = GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/lights_and_shadows/positional_shadow/atlas_quadrant_1_subdiv", PROPERTY_HINT_ENUM, "Disabled,1 Shadow,4 Shadows,16 Shadows,64 Shadows,256 Shadows,1024 Shadows"), 2);
	int atlas_q2 = GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/lights_and_shadows/positional_shadow/atlas_quadrant_2_subdiv", PROPERTY_HINT_ENUM, "Disabled,1 Shadow,4 Shadows,16 Shadows,64 Shadows,256 Shadows,1024 Shadows"), 3);
	int atlas_q3 = GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/lights_and_shadows/positional_shadow/atlas_quadrant_3_subdiv", PROPERTY_HINT_ENUM, "Disabled,1 Shadow,4 Shadows,16 Shadows,64 Shadows,256 Shadows,1024 Shadows"), 4);

	root->set_positional_shadow_atlas_size(shadowmap_size);
	root->set_positional_shadow_atlas_16_bits(shadowmap_16_bits);
	root->set_positional_shadow_atlas_quadrant_subdiv(0, Viewport::PositionalShadowAtlasQuadrantSubdiv(atlas_q0));
	root->set_positional_shadow_atlas_quadrant_subdiv(1, Viewport::PositionalShadowAtlasQuadrantSubdiv(atlas_q1));
	root->set_positional_shadow_atlas_quadrant_subdiv(2, Viewport::PositionalShadowAtlasQuadrantSubdiv(atlas_q2));
	root->set_positional_shadow_atlas_quadrant_subdiv(3, Viewport::PositionalShadowAtlasQuadrantSubdiv(atlas_q3));

	Viewport::SDFOversize sdf_oversize = Viewport::SDFOversize(int(GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/planar/sdf/oversize", PROPERTY_HINT_ENUM, "100%,120%,150%,200%"), 1)));
	root->set_sdf_oversize(sdf_oversize);
	Viewport::SDFScale sdf_scale = Viewport::SDFScale(int(GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/planar/sdf/scale", PROPERTY_HINT_ENUM, "100%,50%,25%"), 1)));
	root->set_sdf_scale(sdf_scale);

#ifndef SPATIAL_DISABLED
	{ // Load default fallback environment.
		// Get possible extensions.
		List<String> exts;
		ResourceLoader::get_recognized_extensions_for_type("Environment", &exts);
		String ext_hint;
		for (const String &E : exts) {
			if (!ext_hint.is_empty()) {
				ext_hint += ",";
			}
			ext_hint += "*." + E;
		}
		// Get path.
		String env_path = GLOBAL_DEF(PropertyInfo(Variant::STRING, "rendering/environment/defaults/default_environment", PROPERTY_HINT_FILE, ext_hint), "");
		// Setup property.
		env_path = env_path.strip_edges();
		if (!env_path.is_empty()) {
			Ref<Environment> env = ResourceLoader::load(env_path);
			if (env.is_valid()) {
				root->get_world_3d()->set_fallback_environment(env);
			} else {
				if (Engine::get_singleton()->is_editor_hint()) {
					// File was erased, clear the field.
					ProjectSettings::get_singleton()->set("rendering/environment/defaults/default_environment", "");
				} else {
					// File was erased, notify user.
					ERR_PRINT("Default Environment as specified in the project setting \"rendering/environment/defaults/default_environment\" could not be loaded.");
				}
			}
		}
	}
#endif // SPATIAL_DISABLED

#if !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)
	root->set_physics_object_picking(GLOBAL_DEF("physics/common/enable_object_picking", true));
#endif // !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)

	root->connect("close_requested", callable_mp(this, &Stage::_main_window_close));
	root->connect("go_back_requested", callable_mp(this, &Stage::_main_window_go_back));
	root->connect(SceneStringName(focus_entered), callable_mp(this, &Stage::_main_window_focus_in));

	ProjectSettings::get_singleton()->connect("settings_changed", callable_mp(this, &Stage::_project_settings_changed));

#ifdef TOOLS_ENABLED
	edited_scene_root = nullptr;
#endif

	process_groups.push_back(&default_process_group);
}

Stage::~Stage() {
	if (prev_scene_id.is_valid()) {
		Entity *prev_scene = ObjectRegistry::get_instance<Entity>(prev_scene_id);
		memdelete(prev_scene);
		prev_scene_id = ObjectID();
	}
	if (pending_new_scene_id.is_valid()) {
		Entity *pending_new_scene = ObjectRegistry::get_instance<Entity>(pending_new_scene_id);
		memdelete(pending_new_scene);
		pending_new_scene_id = ObjectID();
	}
	if (root) {
		root->_set_tree(nullptr);
		root->_propagate_after_exit_tree();
		memdelete(root);
	}

	// Process groups are not deleted immediately, they may remain around. Delete them now.
	for (uint32_t i = 0; i < process_groups.size(); i++) {
		if (process_groups[i] != &default_process_group) {
			memdelete(process_groups[i]);
		}
	}

	memdelete(process_group_call_queue_allocator);

	if (singleton == this) {
		singleton = nullptr;
	}
}
