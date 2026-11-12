/**************************************************************************/
/*  entity.cpp                                                            */

#include "entity.h"

STATIC_ASSERT_INCOMPLETE_TYPE(class, Mesh);
STATIC_ASSERT_INCOMPLETE_TYPE(class, RenderingSystem);
STATIC_ASSERT_INCOMPLETE_TYPE(class, DisplaySystem);
STATIC_ASSERT_INCOMPLETE_TYPE(class, Shader);
STATIC_ASSERT_INCOMPLETE_TYPE(class, OS);
STATIC_ASSERT_INCOMPLETE_TYPE(class, Engine);

#include "core/config/engine.h"
#include "core/config/project_settings.h"
#include "core/io/resource.h"
#include "core/io/resource_loader.h"
#include "core/object/class_registry.h"
#include "core/object/message_queue.h"
#include "core/object/script_language.h"
#include "core/string/print_string.h"
#include "scene/animation/tween.h"
#include "scene/resources/prefab.h"
#include "scene/stage/instance_placeholder.h"
#include "scene/stage/multiplayer_api.h"
#include "scene/stage/stage.h"
#include "scene/stage/viewport.h"
#include "scene/stage/window.h"
#include "systems/display/accessibility_system.h"

#ifdef DEBUG_ENABLED
#include "scene/debugger/scene_debugger.h"
#endif

#ifdef DEBUG_ENABLED
SafeNumeric<uint64_t> Entity::total_node_count{ 0 };
#endif

thread_local Entity *Entity::current_run_thread_group = nullptr;

void Entity::_notification(int p_notification) {
	switch (p_notification) {
		case NOTIFICATION_ACCESSIBILITY_INVALIDATE: {
			if (data.accessibility_element.is_valid()) {
				AccessibilitySystem::get_singleton()->free_element(data.accessibility_element);
				data.accessibility_element = RID();
			}
		} break;

		case NOTIFICATION_ACCESSIBILITY_UPDATE: {
			RID ae = get_accessibility_element();
			ERR_FAIL_COND(ae.is_null());

			AccessibilitySystem::get_singleton()->update_set_name(ae, get_name());

			// Entity children.
			if (!accessibility_override_tree_hierarchy()) {
				for (int i = 0; i < get_child_count(); i++) {
					Entity *child_node = get_child(i);
					Window *child_wnd = Object::cast_to<Window>(child_node);
					if (child_wnd && !(child_wnd->is_visible() && (child_wnd->is_embedded() || child_wnd->is_popup()))) {
						continue;
					}
					if (child_node->is_part_of_edited_scene()) {
						continue;
					}
					AccessibilitySystem::get_singleton()->update_add_child(ae, child_node->get_accessibility_element());
				}
			}
		} break;

		case NOTIFICATION_FRAME: {
			ORBLIT_VIRTUAL_CALL(_frame, get_frame_delta());
		} break;

		case NOTIFICATION_STEP: {
			ORBLIT_VIRTUAL_CALL(_step, get_step_delta());
		} break;

		case NOTIFICATION_ENTER_STAGE: {
			ERR_FAIL_NULL(get_viewport());
			ERR_FAIL_NULL(data.tree);

			if (data.tree->is_accessibility_supported() && !is_part_of_edited_scene()) {
				data.tree->_accessibility_force_update();
				data.tree->_accessibility_notify_change(this);
				if (data.parent) {
					data.tree->_accessibility_notify_change(data.parent);
				} else {
					data.tree->_accessibility_notify_change(get_window()); // Root node.
				}
			}

			// Update process mode.
			if (data.run_mode == RUN_MODE_INHERIT) {
				if (data.parent) {
					data.process_owner = data.parent->data.process_owner;
				} else {
					ERR_PRINT("The root node can't be set to Inherit process mode, reverting to Pausable instead.");
					data.run_mode = RUN_MODE_PAUSABLE;
					data.process_owner = this;
				}
			} else {
				data.process_owner = this;
			}

			{ // Update threaded process mode.
				if (data.run_thread_group == RUN_THREAD_GROUP_INHERIT) {
					if (data.parent) {
						data.run_thread_group_owner = data.parent->data.run_thread_group_owner;
					}

					if (data.run_thread_group_owner) {
						data.process_group = data.run_thread_group_owner->data.process_group;
					} else {
						data.process_group = &data.tree->default_process_group;
					}
				} else {
					data.run_thread_group_owner = this;
					_add_process_group();
				}

				if (_is_any_processing()) {
					_add_to_run_thread_group();
				}
			}

			if (data.physics_interpolation_mode == PHYSICS_INTERPOLATION_MODE_INHERIT) {
				bool interpolate = true; // Root node default is for interpolation to be on.
				if (data.parent) {
					interpolate = data.parent->is_physics_interpolated();
				}
				_propagate_physics_interpolated(interpolate);
			}

			// Update auto translate mode.
			if (data.auto_translate_mode == AUTO_TRANSLATE_MODE_INHERIT && !data.parent) {
				ERR_PRINT("The root node can't be set to Inherit auto translate mode, reverting to Always instead.");
				data.auto_translate_mode = AUTO_TRANSLATE_MODE_ALWAYS;
			}
			data.is_auto_translate_dirty = true;
			data.is_translation_domain_dirty = true;

			if (data.input) {
				add_to_group("_vp_input" + itos(get_viewport()->get_instance_id()));
			}
			if (data.shortcut_input) {
				add_to_group("_vp_shortcut_input" + itos(get_viewport()->get_instance_id()));
			}
			if (data.unhandled_input) {
				add_to_group("_vp_unhandled_input" + itos(get_viewport()->get_instance_id()));
			}
			if (data.unhandled_key_input) {
				add_to_group("_vp_unhandled_key_input" + itos(get_viewport()->get_instance_id()));
			}

			data.tree->nodes_in_tree_count++;

		} break;

		case NOTIFICATION_POST_ENTER_STAGE: {
			notification(NOTIFICATION_TRANSLATION_CHANGED);
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			ERR_FAIL_NULL(get_viewport());
			ERR_FAIL_NULL(data.tree);

			if (data.tree->is_accessibility_supported() && !is_part_of_edited_scene()) {
				if (data.accessibility_element.is_valid()) {
					AccessibilitySystem::get_singleton()->free_element(data.accessibility_element);
					data.accessibility_element = RID();
				}
				data.tree->_accessibility_notify_change(this, true);
				if (data.parent) {
					data.tree->_accessibility_notify_change(data.parent);
				} else {
					data.tree->_accessibility_notify_change(get_window()); // Root node.
				}
			}

			data.tree->nodes_in_tree_count--;

			if (data.input) {
				remove_from_group("_vp_input" + itos(get_viewport()->get_instance_id()));
			}
			if (data.shortcut_input) {
				remove_from_group("_vp_shortcut_input" + itos(get_viewport()->get_instance_id()));
			}
			if (data.unhandled_input) {
				remove_from_group("_vp_unhandled_input" + itos(get_viewport()->get_instance_id()));
			}
			if (data.unhandled_key_input) {
				remove_from_group("_vp_unhandled_key_input" + itos(get_viewport()->get_instance_id()));
			}

			// Remove from processing first.
			if (_is_any_processing()) {
				_remove_from_run_thread_group();
			}
			// Remove the process group.
			if (data.run_thread_group_owner == this) {
				_remove_process_group();
			}
			data.run_thread_group_owner = nullptr;
			data.process_owner = nullptr;

			if (data.path_cache) {
				memdelete(data.path_cache);
				data.path_cache = nullptr;
			}
		} break;

		case NOTIFICATION_SUSPENDED:
		case NOTIFICATION_PAUSED: {
			if (is_physics_interpolated_and_enabled() && is_inside_stage()) {
				reset_physics_interpolation();
			}
		} break;

		case NOTIFICATION_PATH_RENAMED: {
			if (data.path_cache) {
				memdelete(data.path_cache);
				data.path_cache = nullptr;
			}
		} break;

		case NOTIFICATION_START: {
			if (ORBLIT_VIRTUAL_IS_OVERRIDDEN(_input)) {
				set_input_enabled(true);
			}

			if (ORBLIT_VIRTUAL_IS_OVERRIDDEN(_shortcut_input)) {
				set_shortcut_input_enabled(true);
			}

			if (ORBLIT_VIRTUAL_IS_OVERRIDDEN(_unhandled_input)) {
				set_unhandled_input_enabled(true);
			}

			if (ORBLIT_VIRTUAL_IS_OVERRIDDEN(_unhandled_key_input)) {
				set_unhandled_key_input_enabled(true);
			}

			if (ORBLIT_VIRTUAL_IS_OVERRIDDEN(_frame)) {
				set_frame_enabled(true);
			}
			if (ORBLIT_VIRTUAL_IS_OVERRIDDEN(_step)) {
				set_step_enabled(true);
			}

			ORBLIT_VIRTUAL_CALL(_start);
		} break;

		case NOTIFICATION_PREDELETE: {
			if (data.tree && !Thread::is_main_thread()) {
				cancel_free();
				ERR_PRINT("Attempted to free a node that is currently added to the Stage from a thread. This is not permitted, use queue_destroy() instead. Entity has not been freed.");
				return;
			}
#ifdef TOOLS_ENABLED
			if (Engine::get_singleton()->is_editor_hint() && data.tree && this == data.tree->get_edited_scene_root()) {
				cancel_free();
				ERR_PRINT(vformat("Something attempted to free the root Entity of a scene (\"%s\"). This is not supported inside the editor, so the Entity was not freed.", get_name()));
				return;
			}
#endif
			if (data.owner) {
				_clean_up_owner();
			}

			while (!data.owned.is_empty()) {
				Entity *n = data.owned.back()->get();
				n->_clean_up_owner(); // This will change data.owned. So it's impossible to loop over the list in the usual manner.
			}

			if (data.parent) {
				data.parent->remove_child(this);
			}

			// kill children as cleanly as possible
			while (data.children.size()) {
				Entity *child = data.children.last()->value; // begin from the end because its faster and more consistent with creation
				memdelete(child);
			}
		} break;

		case NOTIFICATION_TRANSLATION_CHANGED: {
			if (data.tree) {
				data.is_auto_translate_dirty = true;
			}
		} break;
	}
}

void Entity::_propagate_ready() {
	data.ready_notified = true;
	data.blocked++;
	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->_propagate_ready();
	}

	data.blocked--;

	notification(NOTIFICATION_POST_ENTER_STAGE);

	if (data.ready_first) {
		data.ready_first = false;
		notification(NOTIFICATION_START);
		emit_signal(SceneStringName(started));
	}
}

void Entity::_propagate_enter_tree() {
	// this needs to happen to all children before any enter_tree

	if (data.parent) {
		data.tree = data.parent->data.tree;
		data.depth = data.parent->data.depth + 1;
	} else {
		data.depth = 1;
	}

	data.viewport = Object::cast_to<Viewport>(this);
	if (!data.viewport && data.parent) {
		data.viewport = data.parent->data.viewport;
	}

	for (KeyValue<StringName, GroupData> &E : data.grouped) {
		E.value.group = data.tree->add_to_group(E.key, this);
	}

	notification(NOTIFICATION_ENTER_STAGE);

	ORBLIT_VIRTUAL_CALL(_enter_stage);

	emit_signal(SceneStringName(stage_entered));

	data.tree->entity_added(this);

	if (data.parent) {
		Variant c = this;
		const Variant *cptr = &c;
		data.parent->emit_signalp(SNAME("child_entered_stage"), &cptr, 1);
	}

	data.blocked++;
	//block while adding children

	for (KeyValue<StringName, Entity *> &K : data.children) {
		if (!K.value->is_inside_stage()) { // could have been added in enter_tree
			K.value->_propagate_enter_tree();
		}
	}

	data.blocked--;

#ifdef DEBUG_ENABLED
	SceneDebugger::add_to_cache(data.scene_file_path, this);
#endif
	// enter groups
}

void Entity::_propagate_after_exit_tree() {
	// Clear owner if it was not part of the pruned branch
	if (data.owner) {
		if (!data.owner->is_ancestor_of(this)) {
			_clean_up_owner();
		}
	}

	data.blocked++;

	for (HashMap<StringName, Entity *>::Iterator I = data.children.last(); I; --I) {
		I->value->_propagate_after_exit_tree();
	}

	data.blocked--;

	emit_signal(SceneStringName(stage_exited));
}

void Entity::_propagate_exit_tree() {
	//block while removing children

#ifdef DEBUG_ENABLED
	if (!data.scene_file_path.is_empty()) {
		// Only remove if file path is set (optimization).
		SceneDebugger::remove_from_cache(data.scene_file_path, this);
	}
#endif
	data.blocked++;

	for (HashMap<StringName, Entity *>::Iterator I = data.children.last(); I; --I) {
		I->value->_propagate_exit_tree();
	}

	data.blocked--;

	ORBLIT_VIRTUAL_CALL(_exit_stage);

	emit_signal(SceneStringName(stage_exiting));

	notification(NOTIFICATION_EXIT_STAGE, true);
	if (data.tree) {
		data.tree->entity_removed(this);
	}

	if (data.parent) {
		Variant c = this;
		const Variant *cptr = &c;
		data.parent->emit_signalp(SNAME("child_exiting_stage"), &cptr, 1);
	}

	// exit groups
	for (KeyValue<StringName, GroupData> &E : data.grouped) {
		data.tree->remove_from_group(E.key, this);
		E.value.group = nullptr;
	}

	data.viewport = nullptr;

	if (data.tree) {
		data.tree->stage_changed();
	}

	data.ready_notified = false;
	data.tree = nullptr;
	data.depth = -1;
}

void Entity::_propagate_physics_interpolated(bool p_interpolated) {
	switch (data.physics_interpolation_mode) {
		case PHYSICS_INTERPOLATION_MODE_INHERIT:
			// Keep the parent p_interpolated.
			break;
		case PHYSICS_INTERPOLATION_MODE_OFF: {
			p_interpolated = false;
		} break;
		case PHYSICS_INTERPOLATION_MODE_ON: {
			p_interpolated = true;
		} break;
	}

	// No change? No need to propagate further.
	if (data.physics_interpolated == p_interpolated) {
		return;
	}

	data.physics_interpolated = p_interpolated;

	// Allow a call to the RenderingSystem etc. in derived classes.
	_physics_interpolated_changed();

	update_configuration_warnings();

	data.blocked++;
	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->_propagate_physics_interpolated(p_interpolated);
	}
	data.blocked--;
}

void Entity::_propagate_physics_interpolation_reset_requested(bool p_requested) {
	if (is_physics_interpolated()) {
		data.physics_interpolation_reset_requested = p_requested;
	}

	data.blocked++;
	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->_propagate_physics_interpolation_reset_requested(p_requested);
	}
	data.blocked--;
}

void Entity::move_child(RequiredParam<Entity> p_child, int p_index) {
	ERR_FAIL_COND_MSG(data.tree && !Thread::is_main_thread(), "Moving child node positions inside the Stage is only allowed from the main thread. Use call_deferred(\"move_child\",child,index).");
	EXTRACT_PARAM_OR_FAIL(child, p_child);
	ERR_FAIL_COND_MSG(child->data.parent != this, "Child is not a child of this node.");

	_update_children_cache();
	// We need to check whether node is internal and move it only in the relevant node range.
	if (child->data.internal_mode == INTERNAL_MODE_FRONT) {
		if (p_index < 0) {
			p_index += data.internal_children_front_count_cache;
		}
		ERR_FAIL_INDEX_MSG(p_index, data.internal_children_front_count_cache, vformat("Invalid new child index: %d. Child is internal.", p_index));
		_move_child(child, p_index);
	} else if (child->data.internal_mode == INTERNAL_MODE_BACK) {
		if (p_index < 0) {
			p_index += data.internal_children_back_count_cache;
		}
		ERR_FAIL_INDEX_MSG(p_index, data.internal_children_back_count_cache, vformat("Invalid new child index: %d. Child is internal.", p_index));
		_move_child(child, (int)data.children_cache.size() - data.internal_children_back_count_cache + p_index);
	} else {
		if (p_index < 0) {
			p_index += get_child_count(false);
		}
		ERR_FAIL_INDEX_MSG(p_index, (int)data.children_cache.size() + 1 - data.internal_children_front_count_cache - data.internal_children_back_count_cache, vformat("Invalid new child index: %d.", p_index));
		_move_child(child, p_index + data.internal_children_front_count_cache);
	}
}

void Entity::_move_child(Entity *p_child, int p_index, bool p_ignore_end) {
	ERR_FAIL_COND_MSG(data.blocked > 0, "Parent node is busy setting up children, `move_child()` failed. Consider using `move_child.call_deferred(child, index)` instead (or `popup.call_deferred()` if this is from a popup).");

	// Specifying one place beyond the end
	// means the same as moving to the last index
	if (!p_ignore_end) { // p_ignore_end is a little hack to make back internal children work properly.
		if (p_child->data.internal_mode == INTERNAL_MODE_FRONT) {
			if (p_index == data.internal_children_front_count_cache) {
				p_index--;
			}
		} else if (p_child->data.internal_mode == INTERNAL_MODE_BACK) {
			if (p_index == (int)data.children_cache.size()) {
				p_index--;
			}
		} else {
			if (p_index == (int)data.children_cache.size() - data.internal_children_back_count_cache) {
				p_index--;
			}
		}
	}

	int child_index = p_child->get_index();

	if (child_index == p_index) {
		return; //do nothing
	}

	int motion_from = MIN(p_index, child_index);
	int motion_to = MAX(p_index, child_index);

	data.children_cache.remove_at(child_index);
	data.children_cache.insert(p_index, p_child);

	if (data.tree) {
		data.tree->stage_changed();
	}

	data.blocked++;
	//new pos first
	for (int i = motion_from; i <= motion_to; i++) {
		if (data.children_cache[i]->data.internal_mode == INTERNAL_MODE_DISABLED) {
			data.children_cache[i]->data.index = i - data.internal_children_front_count_cache;
		} else if (data.children_cache[i]->data.internal_mode == INTERNAL_MODE_BACK) {
			data.children_cache[i]->data.index = i - data.internal_children_front_count_cache - data.external_children_count_cache;
		} else {
			data.children_cache[i]->data.index = i;
		}
	}
	// notification second
	move_child_notify(p_child);
	notification(NOTIFICATION_CHILD_ORDER_CHANGED);
	emit_signal(SNAME("child_order_changed"));
	p_child->_propagate_groups_dirty();

	data.blocked--;
}

void Entity::_propagate_groups_dirty() {
	for (const KeyValue<StringName, GroupData> &E : data.grouped) {
		if (E.value.group) {
			E.value.group->changed = true;
		}
	}

	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->_propagate_groups_dirty();
	}
}

void Entity::add_child_notify(Entity *p_child) {
	// to be used when not wanted
}

void Entity::remove_child_notify(Entity *p_child) {
	// to be used when not wanted
}

void Entity::move_child_notify(Entity *p_child) {
	// to be used when not wanted
}

void Entity::owner_changed_notify() {
}

void Entity::_physics_interpolated_changed() {}

void Entity::set_step_enabled(bool p_process) {
	ERR_THREAD_GUARD
	if (data.physics_process == p_process) {
		return;
	}

	if (!is_inside_stage()) {
		data.physics_process = p_process;
		return;
	}

	if (_is_any_processing()) {
		_remove_from_run_thread_group();
	}

	data.physics_process = p_process;

	if (_is_any_processing()) {
		_add_to_run_thread_group();
	}
}

bool Entity::is_step_enabled() const {
	return data.physics_process;
}

void Entity::set_internal_step_enabled(bool p_process_internal) {
	ERR_THREAD_GUARD
	if (data.physics_process_internal == p_process_internal) {
		return;
	}

	if (!is_inside_stage()) {
		data.physics_process_internal = p_process_internal;
		return;
	}

	if (_is_any_processing()) {
		_remove_from_run_thread_group();
	}

	data.physics_process_internal = p_process_internal;

	if (_is_any_processing()) {
		_add_to_run_thread_group();
	}
}

bool Entity::is_internal_step_enabled() const {
	return data.physics_process_internal;
}

void Entity::set_run_mode(RunMode p_mode) {
	ERR_THREAD_GUARD
	if (data.run_mode == p_mode) {
		return;
	}

	if (!is_inside_stage()) {
		data.run_mode = p_mode;
		return;
	}

	bool prev_can_run = can_run();
	bool prev_enabled = _is_enabled();

	if (p_mode == RUN_MODE_INHERIT) {
		if (data.parent) {
			data.process_owner = data.parent->data.process_owner;
		} else {
			ERR_FAIL_MSG("The root node can't be set to Inherit process mode.");
		}
	} else {
		data.process_owner = this;
	}

	data.run_mode = p_mode;

	bool next_can_run = can_run();
	bool next_enabled = _is_enabled();

	int pause_notification = 0;

	if (prev_can_run && !next_can_run) {
		pause_notification = NOTIFICATION_PAUSED;
	} else if (!prev_can_run && next_can_run) {
		pause_notification = NOTIFICATION_UNPAUSED;
	}

	int enabled_notification = 0;

	if (prev_enabled && !next_enabled) {
		enabled_notification = NOTIFICATION_DISABLED;
	} else if (!prev_enabled && next_enabled) {
		enabled_notification = NOTIFICATION_ENABLED;
	}

	_propagate_process_owner(data.process_owner, pause_notification, enabled_notification);

#ifdef TOOLS_ENABLED
	// This is required for the editor to update the visibility of disabled nodes
	// It's very expensive during runtime to change, so editor-only
	if (Engine::get_singleton()->is_editor_hint()) {
		data.tree->emit_signal(SNAME("stage_run_mode_changed"));
	}

	_emit_editor_state_changed();
#endif
}

void Entity::_propagate_pause_notification(bool p_enable) {
	bool prev_can_run = _can_run(!p_enable);
	bool next_can_run = _can_run(p_enable);

	if (prev_can_run && !next_can_run) {
		notification(NOTIFICATION_PAUSED);
	} else if (!prev_can_run && next_can_run) {
		notification(NOTIFICATION_UNPAUSED);
	}

	data.blocked++;
	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->_propagate_pause_notification(p_enable);
	}
	data.blocked--;
}

void Entity::_propagate_suspend_notification(bool p_enable) {
	notification(p_enable ? NOTIFICATION_SUSPENDED : NOTIFICATION_UNSUSPENDED);

	data.blocked++;
	for (KeyValue<StringName, Entity *> &KV : data.children) {
		KV.value->_propagate_suspend_notification(p_enable);
	}
	data.blocked--;
}

Entity::RunMode Entity::get_run_mode() const {
	return data.run_mode;
}

void Entity::_propagate_process_owner(Entity *p_owner, int p_pause_notification, int p_enabled_notification) {
	data.process_owner = p_owner;

	if (p_pause_notification != 0) {
		notification(p_pause_notification);
	}

	if (p_enabled_notification != 0) {
		notification(p_enabled_notification);
	}

	data.blocked++;
	for (KeyValue<StringName, Entity *> &K : data.children) {
		Entity *c = K.value;
		if (c->data.run_mode == RUN_MODE_INHERIT) {
			c->_propagate_process_owner(p_owner, p_pause_notification, p_enabled_notification);
		}
	}
	data.blocked--;
}

void Entity::set_multiplayer_authority(int p_peer_id, bool p_recursive) {
	ERR_THREAD_GUARD
	data.multiplayer_authority = p_peer_id;

	if (p_recursive) {
		for (KeyValue<StringName, Entity *> &K : data.children) {
			K.value->set_multiplayer_authority(p_peer_id, true);
		}
	}
}

int Entity::get_multiplayer_authority() const {
	return data.multiplayer_authority;
}

bool Entity::is_multiplayer_authority() const {
	ERR_FAIL_COND_V(!is_inside_stage(), false);

	Ref<MultiplayerAPI> api = get_multiplayer();
	return api.is_valid() && (api->get_unique_id() == data.multiplayer_authority);
}

/***** RPC CONFIG ********/

void Entity::rpc_config(const StringName &p_method, const Variant &p_config) {
	ERR_THREAD_GUARD
	if (data.rpc_config.get_type() != Variant::DICTIONARY) {
		data.rpc_config = Dictionary();
	}
	Dictionary node_config = data.rpc_config;
	if (p_config.get_type() == Variant::NIL) {
		node_config.erase(p_method);
	} else {
		ERR_FAIL_COND(p_config.get_type() != Variant::DICTIONARY);
		node_config[p_method] = p_config;
	}
}

const Variant Entity::get_entity_rpc_config() const {
	return data.rpc_config;
}

/***** RPC FUNCTIONS ********/

Error Entity::_rpc_bind(const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	if (p_argcount < 1) {
		r_error.error = Callable::CallError::CALL_ERROR_TOO_FEW_ARGUMENTS;
		r_error.expected = 1;
		return ERR_INVALID_PARAMETER;
	}

	if (!p_args[0]->is_string()) {
		r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
		r_error.argument = 0;
		r_error.expected = Variant::STRING_NAME;
		return ERR_INVALID_PARAMETER;
	}

	StringName method = (*p_args[0]).operator StringName();

	Error err = rpcp(0, method, &p_args[1], p_argcount - 1);
	r_error.error = Callable::CallError::CALL_OK;
	return err;
}

Error Entity::_rpc_id_bind(const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	if (p_argcount < 2) {
		r_error.error = Callable::CallError::CALL_ERROR_TOO_FEW_ARGUMENTS;
		r_error.expected = 2;
		return ERR_INVALID_PARAMETER;
	}

	if (p_args[0]->get_type() != Variant::INT) {
		r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
		r_error.argument = 0;
		r_error.expected = Variant::INT;
		return ERR_INVALID_PARAMETER;
	}

	if (!p_args[1]->is_string()) {
		r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
		r_error.argument = 1;
		r_error.expected = Variant::STRING_NAME;
		return ERR_INVALID_PARAMETER;
	}

	int peer_id = *p_args[0];
	StringName method = (*p_args[1]).operator StringName();

	Error err = rpcp(peer_id, method, &p_args[2], p_argcount - 2);
	r_error.error = Callable::CallError::CALL_OK;
	return err;
}

Error Entity::rpcp(int p_peer_id, const StringName &p_method, const Variant **p_arg, int p_argcount) {
	ERR_FAIL_COND_V(!is_inside_stage(), ERR_UNCONFIGURED);

	Ref<MultiplayerAPI> api = get_multiplayer();
	if (api.is_null()) {
		return ERR_UNCONFIGURED;
	}
	return api->rpcp(this, p_peer_id, p_method, p_arg, p_argcount);
}

Ref<MultiplayerAPI> Entity::get_multiplayer() const {
	if (!is_inside_stage()) {
		return Ref<MultiplayerAPI>();
	}
	return data.tree->get_multiplayer(get_path());
}

//////////// end of rpc

bool Entity::can_process_notification(int p_what) const {
	switch (p_what) {
		case NOTIFICATION_STEP:
			return data.physics_process;
		case NOTIFICATION_FRAME:
			return data.process;
		case NOTIFICATION_INTERNAL_FRAME:
			return data.process_internal;
		case NOTIFICATION_INTERNAL_STEP:
			return data.physics_process_internal;
	}

	return true;
}

bool Entity::can_run() const {
	return is_inside_stage() && !data.tree->is_suspended() && _can_run(data.tree->is_paused());
}

bool Entity::_can_run(bool p_paused) const {
	RunMode run_mode;

	if (data.run_mode == RUN_MODE_INHERIT) {
		if (!data.process_owner) {
			run_mode = RUN_MODE_PAUSABLE;
		} else {
			run_mode = data.process_owner->data.run_mode;
		}
	} else {
		run_mode = data.run_mode;
	}

	// The owner can't be set to inherit, must be a bug.
	ERR_FAIL_COND_V(run_mode == RUN_MODE_INHERIT, false);

	if (run_mode == RUN_MODE_DISABLED) {
		return false;
	} else if (run_mode == RUN_MODE_ALWAYS) {
		return true;
	}

	if (p_paused) {
		return run_mode == RUN_MODE_WHEN_PAUSED;
	} else {
		return run_mode == RUN_MODE_PAUSABLE;
	}
}

void Entity::set_physics_interpolation_mode(PhysicsInterpolationMode p_mode) {
	ERR_THREAD_GUARD
	if (data.physics_interpolation_mode == p_mode) {
		return;
	}

	data.physics_interpolation_mode = p_mode;

	bool interpolate = true; // Default for root node.

	switch (p_mode) {
		case PHYSICS_INTERPOLATION_MODE_INHERIT: {
			if (is_inside_stage() && data.parent) {
				interpolate = data.parent->is_physics_interpolated();
			}
		} break;
		case PHYSICS_INTERPOLATION_MODE_OFF: {
			interpolate = false;
		} break;
		case PHYSICS_INTERPOLATION_MODE_ON: {
			interpolate = true;
		} break;
	}

	_propagate_physics_interpolated(interpolate);

	// Auto-reset on changing interpolation mode.
	if (is_physics_interpolated() && is_inside_stage()) {
		propagate_notification(NOTIFICATION_RESET_PHYSICS_INTERPOLATION);
	}
}

bool Entity::is_physics_interpolated_and_enabled() const {
	return Stage::is_fti_enabled() && is_physics_interpolated();
}

void Entity::reset_physics_interpolation() {
	if (Stage::is_fti_enabled() && is_inside_stage()) {
		propagate_notification(NOTIFICATION_RESET_PHYSICS_INTERPOLATION);

		// If `reset_physics_interpolation()` is called explicitly by the user
		// (e.g. from scripts) then we prevent deferred auto-resets taking place.
		// The user is trusted to call reset in the right order, and auto-reset
		// will interfere with their control of prev / curr, so should be turned off.
		_propagate_physics_interpolation_reset_requested(false);
	}
}

bool Entity::_is_enabled() const {
	RunMode run_mode;

	if (data.run_mode == RUN_MODE_INHERIT) {
		if (!data.process_owner) {
			run_mode = RUN_MODE_PAUSABLE;
		} else {
			run_mode = data.process_owner->data.run_mode;
		}
	} else {
		run_mode = data.run_mode;
	}

	return (run_mode != RUN_MODE_DISABLED);
}

bool Entity::is_enabled() const {
	ERR_FAIL_COND_V(!is_inside_stage(), false);
	return _is_enabled();
}

double Entity::get_step_delta() const {
	if (data.tree) {
		return data.tree->get_physics_process_time();
	} else {
		return 0;
	}
}

double Entity::get_frame_delta() const {
	if (data.tree) {
		return data.tree->get_process_time();
	} else {
		return 0;
	}
}

void Entity::set_frame_enabled(bool p_process) {
	ERR_THREAD_GUARD
	if (data.process == p_process) {
		return;
	}

	if (!is_inside_stage()) {
		data.process = p_process;
		return;
	}

	if (_is_any_processing()) {
		_remove_from_run_thread_group();
	}

	data.process = p_process;

	if (_is_any_processing()) {
		_add_to_run_thread_group();
	}
}

bool Entity::is_frame_enabled() const {
	return data.process;
}

void Entity::set_internal_frame_enabled(bool p_process_internal) {
	ERR_THREAD_GUARD
	if (data.process_internal == p_process_internal) {
		return;
	}

	if (!is_inside_stage()) {
		data.process_internal = p_process_internal;
		return;
	}

	if (_is_any_processing()) {
		_remove_from_run_thread_group();
	}

	data.process_internal = p_process_internal;

	if (_is_any_processing()) {
		_add_to_run_thread_group();
	}
}

void Entity::_add_process_group() {
	data.tree->_add_process_group(this);
}

void Entity::_remove_process_group() {
	data.tree->_remove_process_group(this);
}

void Entity::_remove_from_run_thread_group() {
	data.tree->_remove_node_from_process_group(this, data.run_thread_group_owner);
}

void Entity::_add_to_run_thread_group() {
	data.tree->_add_node_to_process_group(this, data.run_thread_group_owner);
}

void Entity::_remove_tree_from_run_thread_group() {
	if (!is_inside_stage()) {
		return; // May not be initialized yet.
	}

	for (KeyValue<StringName, Entity *> &K : data.children) {
		if (K.value->data.run_thread_group != RUN_THREAD_GROUP_INHERIT) {
			continue;
		}

		K.value->_remove_tree_from_run_thread_group();
	}

	if (_is_any_processing()) {
		_remove_from_run_thread_group();
	}
}

void Entity::_add_tree_to_run_thread_group(Entity *p_owner) {
	data.run_thread_group_owner = p_owner;
	if (p_owner != nullptr) {
		data.process_group = p_owner->data.process_group;
	} else {
		data.process_group = &data.tree->default_process_group;
	}

	if (_is_any_processing()) {
		_add_to_run_thread_group();
	}

	for (KeyValue<StringName, Entity *> &K : data.children) {
		if (K.value->data.run_thread_group != RUN_THREAD_GROUP_INHERIT) {
			continue;
		}

		K.value->_add_tree_to_run_thread_group(p_owner);
	}
}
bool Entity::is_internal_frame_enabled() const {
	return data.process_internal;
}

void Entity::set_run_thread_group_order(int p_order) {
	ERR_THREAD_GUARD
	if (data.run_thread_group_order == p_order) {
		return;
	}

	data.run_thread_group_order = p_order;

	// Not yet in the tree (or not a group owner, in whose case this is pointless but harmless); trivial update.
	if (!is_inside_stage() || data.run_thread_group_owner != this) {
		return;
	}

	data.tree->process_groups_dirty = true;
}

int Entity::get_run_thread_group_order() const {
	return data.run_thread_group_order;
}

void Entity::set_run_priority(int p_priority) {
	ERR_THREAD_GUARD
	if (data.run_priority == p_priority) {
		return;
	}
	if (!is_inside_stage()) {
		// Not yet in the tree; trivial update.
		data.run_priority = p_priority;
		return;
	}

	if (_is_any_processing()) {
		_remove_from_run_thread_group();
	}

	data.run_priority = p_priority;

	if (_is_any_processing()) {
		_add_to_run_thread_group();
	}
}

int Entity::get_run_priority() const {
	return data.run_priority;
}

void Entity::set_run_physics_priority(int p_priority) {
	ERR_THREAD_GUARD
	if (data.run_physics_priority == p_priority) {
		return;
	}
	if (!is_inside_stage()) {
		// Not yet in the tree; trivial update.
		data.run_physics_priority = p_priority;
		return;
	}

	if (_is_any_processing()) {
		_remove_from_run_thread_group();
	}

	data.run_physics_priority = p_priority;

	if (_is_any_processing()) {
		_add_to_run_thread_group();
	}
}

int Entity::get_run_physics_priority() const {
	return data.run_physics_priority;
}

void Entity::set_run_thread_group(RunThreadGroup p_mode) {
	ERR_FAIL_COND_MSG(data.tree && !Thread::is_main_thread(), "Changing the process thread group can only be done from the main thread. Use call_deferred(\"set_run_thread_group\",mode).");
	if (data.run_thread_group == p_mode) {
		return;
	}

	if (!is_inside_stage()) {
		// Not yet in the tree; trivial update.
		data.run_thread_group = p_mode;
		return;
	}

	_remove_tree_from_run_thread_group();
	if (data.run_thread_group != RUN_THREAD_GROUP_INHERIT) {
		_remove_process_group();
	}

	data.run_thread_group = p_mode;

	if (p_mode == RUN_THREAD_GROUP_INHERIT) {
		if (data.parent) {
			data.run_thread_group_owner = data.parent->data.run_thread_group_owner;
		} else {
			data.run_thread_group_owner = nullptr;
		}
	} else {
		data.run_thread_group_owner = this;
		_add_process_group();
	}

	_add_tree_to_run_thread_group(data.run_thread_group_owner);

	notify_property_list_changed();
}

Entity::RunThreadGroup Entity::get_run_thread_group() const {
	return data.run_thread_group;
}

void Entity::set_run_thread_messages(BitField<RunThreadMessages> p_flags) {
	ERR_THREAD_GUARD
	if (data.run_thread_messages == p_flags) {
		return;
	}

	data.run_thread_messages = p_flags;
}

BitField<Entity::RunThreadMessages> Entity::get_run_thread_messages() const {
	return data.run_thread_messages;
}

void Entity::set_input_enabled(bool p_enable) {
	ERR_THREAD_GUARD
	if (p_enable == data.input) {
		return;
	}

	data.input = p_enable;
	if (!is_inside_stage()) {
		return;
	}

	if (p_enable) {
		add_to_group("_vp_input" + itos(get_viewport()->get_instance_id()));
	} else {
		remove_from_group("_vp_input" + itos(get_viewport()->get_instance_id()));
	}
}

bool Entity::is_input_enabled() const {
	return data.input;
}

void Entity::set_shortcut_input_enabled(bool p_enable) {
	ERR_THREAD_GUARD
	if (p_enable == data.shortcut_input) {
		return;
	}
	data.shortcut_input = p_enable;
	if (!is_inside_stage()) {
		return;
	}

	if (p_enable) {
		add_to_group("_vp_shortcut_input" + itos(get_viewport()->get_instance_id()));
	} else {
		remove_from_group("_vp_shortcut_input" + itos(get_viewport()->get_instance_id()));
	}
}

bool Entity::is_shortcut_input_enabled() const {
	return data.shortcut_input;
}

void Entity::set_unhandled_input_enabled(bool p_enable) {
	ERR_THREAD_GUARD
	if (p_enable == data.unhandled_input) {
		return;
	}
	data.unhandled_input = p_enable;
	if (!is_inside_stage()) {
		return;
	}

	if (p_enable) {
		add_to_group("_vp_unhandled_input" + itos(get_viewport()->get_instance_id()));
	} else {
		remove_from_group("_vp_unhandled_input" + itos(get_viewport()->get_instance_id()));
	}
}

bool Entity::is_unhandled_input_enabled() const {
	return data.unhandled_input;
}

void Entity::set_unhandled_key_input_enabled(bool p_enable) {
	ERR_THREAD_GUARD
	if (p_enable == data.unhandled_key_input) {
		return;
	}
	data.unhandled_key_input = p_enable;
	if (!is_inside_stage()) {
		return;
	}

	if (p_enable) {
		add_to_group("_vp_unhandled_key_input" + itos(get_viewport()->get_instance_id()));
	} else {
		remove_from_group("_vp_unhandled_key_input" + itos(get_viewport()->get_instance_id()));
	}
}

bool Entity::is_unhandled_key_input_enabled() const {
	return data.unhandled_key_input;
}

void Entity::set_auto_translate_mode(AutoTranslateMode p_mode) {
	ERR_THREAD_GUARD
	if (data.auto_translate_mode == p_mode) {
		return;
	}

	if (p_mode == AUTO_TRANSLATE_MODE_INHERIT && data.tree && !data.parent) {
		ERR_FAIL_MSG("The root node can't be set to Inherit auto translate mode.");
	}

	data.auto_translate_mode = p_mode;
	data.is_auto_translating = p_mode != AUTO_TRANSLATE_MODE_DISABLED;
	data.is_auto_translate_dirty = true;

	propagate_notification(NOTIFICATION_TRANSLATION_CHANGED);
}

Entity::AutoTranslateMode Entity::get_auto_translate_mode() const {
	return data.auto_translate_mode;
}

bool Entity::can_auto_translate() const {
	ERR_READ_THREAD_GUARD_V(false);
	if (!data.is_auto_translate_dirty || data.auto_translate_mode != AUTO_TRANSLATE_MODE_INHERIT) {
		return data.is_auto_translating;
	}

	data.is_auto_translate_dirty = false;

	Entity *parent = data.parent;
	while (parent) {
		if (parent->data.auto_translate_mode == AUTO_TRANSLATE_MODE_INHERIT) {
			parent = parent->data.parent;
			continue;
		}

		data.is_auto_translating = parent->data.auto_translate_mode == AUTO_TRANSLATE_MODE_ALWAYS;
		break;
	}

	return data.is_auto_translating;
}

StringName Entity::get_translation_domain() const {
	ERR_READ_THREAD_GUARD_V(StringName());

	if (data.is_translation_domain_inherited && data.is_translation_domain_dirty) {
		const_cast<Entity *>(this)->_translation_domain = data.parent ? data.parent->get_translation_domain() : StringName();
		data.is_translation_domain_dirty = false;
	}
	return _translation_domain;
}

void Entity::set_translation_domain(const StringName &p_domain) {
	ERR_THREAD_GUARD

	if (!data.is_translation_domain_inherited && _translation_domain == p_domain) {
		return;
	}

	_translation_domain = p_domain;
	data.is_translation_domain_inherited = false;
	data.is_translation_domain_dirty = false;
	_propagate_translation_domain_dirty();
}

void Entity::set_translation_domain_inherited() {
	ERR_THREAD_GUARD

	if (data.is_translation_domain_inherited) {
		return;
	}
	data.is_translation_domain_inherited = true;
	data.is_translation_domain_dirty = true;
	_propagate_translation_domain_dirty();
}

void Entity::_propagate_translation_domain_dirty() {
	for (KeyValue<StringName, Entity *> &K : data.children) {
		Entity *child = K.value;
		if (child->data.is_translation_domain_inherited) {
			child->data.is_translation_domain_dirty = true;
			child->_propagate_translation_domain_dirty();
		}
	}

	if (is_inside_stage() && data.auto_translate_mode != AUTO_TRANSLATE_MODE_DISABLED) {
		notification(NOTIFICATION_TRANSLATION_CHANGED);
	}
}

StringName Entity::get_name() const {
	return data.name;
}

void Entity::_set_name_nocheck(const StringName &p_name) {
	data.name = p_name;
}

void Entity::set_name(const StringName &p_name) {
	ERR_FAIL_COND_MSG(data.tree && !Thread::is_main_thread(), "Changing the name to nodes inside the Stage is only allowed from the main thread. Use `set_name.call_deferred(new_name)`.");
	ERR_FAIL_COND(p_name.is_empty());

	const StringName old_name = data.name;
	if (data.unique_name_in_owner && data.owner) {
		_release_unique_name_in_owner();
	}

	{
		const String input_name_str = String(p_name);
		const String validated_node_name_string = input_name_str.validate_node_name();
		if (input_name_str == validated_node_name_string) {
			data.name = p_name;
		} else {
			data.name = StringName(validated_node_name_string);
		}
	}

	if (data.parent) {
		data.parent->_validate_child_name(this, true);
		bool success = data.parent->data.children.replace_key(old_name, data.name);
		ERR_FAIL_COND_MSG(!success, "Renaming child in hashtable failed, this is a bug.");
	}

	if (data.unique_name_in_owner && data.owner) {
		_acquire_unique_name_in_owner();
	}

	propagate_notification(NOTIFICATION_PATH_RENAMED);

	if (is_inside_stage()) {
		emit_signal(SNAME("renamed"));
		data.tree->entity_renamed(this);
		data.tree->stage_changed();
	}
}

// Returns a clear description of this node depending on what is available. Useful for error messages.
String Entity::get_description(bool p_show_not_in_tree) const {
	String description;
	if (is_inside_stage()) {
		description = String(get_path());
	} else {
		description = get_name();
		if (description.is_empty()) {
			description = get_class();
		}
		if (p_show_not_in_tree) {
			description += " (not inside tree)";
		}
	}
	return description;
}

static SafeRefCount node_hrcr_count;

void Entity::init_node_hrcr() {
	node_hrcr_count.init(1);
}

#ifdef TOOLS_ENABLED
String Entity::validate_child_name(Entity *p_child) {
	StringName name = p_child->data.name;
	_generate_serial_child_name(p_child, name);
	return name;
}

String Entity::prevalidate_child_name(Entity *p_child, StringName p_name) {
	_generate_serial_child_name(p_child, p_name);
	return p_name;
}
#endif

String Entity::adjust_name_casing(const String &p_name) {
	switch (GLOBAL_GET("editor/naming/node_name_casing").operator int()) {
		case NAME_CASING_PASCAL_CASE:
			return p_name.to_pascal_case();
		case NAME_CASING_CAMEL_CASE:
			return p_name.to_camel_case();
		case NAME_CASING_SNAKE_CASE:
			return p_name.to_snake_case();
		case NAME_CASING_KEBAB_CASE:
			return p_name.to_kebab_case();
	}
	return p_name;
}

void Entity::_validate_child_name(Entity *p_child, bool p_force_human_readable) {
	/* Make sure the name is unique */

	if (p_force_human_readable) {
		//this approach to autoset node names is human readable but very slow

		StringName name = p_child->data.name;
		_generate_serial_child_name(p_child, name);
		p_child->data.name = name;

	} else {
		//this approach to autoset node names is fast but not as readable
		//it's the default and reserves the '@' character for unique names.

		bool unique = true;

		if (p_child->data.name == StringName()) {
			//new unique name must be assigned
			unique = false;
		} else {
			const Entity *const *existing = data.children.getptr(p_child->data.name);
			unique = !existing || *existing == p_child;
		}

		if (!unique) {
			ERR_FAIL_COND(!node_hrcr_count.ref());
			// Optimized version of the code below:
			// String name = "@" + String(p_child->get_name()) + "@" + itos(node_hrcr_count.get());
			uint32_t c = node_hrcr_count.get();
			String cn = p_child->get_class_name().string();
			const char32_t *cn_ptr = cn.ptr();
			uint32_t cn_length = cn.length();
			uint32_t c_chars = String::num_characters(c);
			uint32_t len = 2 + cn_length + c_chars;
			char32_t *str = (char32_t *)alloca(sizeof(char32_t) * (len + 1));
			uint32_t idx = 0;
			str[idx++] = '@';
			for (uint32_t i = 0; i < cn_length; i++) {
				str[idx++] = cn_ptr[i];
			}
			str[idx++] = '@';
			idx += c_chars;
			ERR_FAIL_COND(idx != len);
			str[idx] = 0;
			while (c) {
				str[--idx] = '0' + (c % 10);
				c /= 10;
			}
			p_child->data.name = String(str);
		}
	}
}

// Return s + 1 as if it were an integer
String increase_numeric_string(const String &s) {
	String res = s;
	bool carry = res.length() > 0;

	for (int i = res.length() - 1; i >= 0; i--) {
		if (!carry) {
			break;
		}
		char32_t n = s[i];
		if (n == '9') { // keep carry as true: 9 + 1
			res[i] = '0';
		} else {
			res[i] = s[i] + 1;
			carry = false;
		}
	}

	if (carry) {
		res = "1" + res;
	}

	return res;
}

void Entity::_generate_serial_child_name(const Entity *p_child, StringName &name) const {
	if (name == StringName()) {
		// No name and a new name is needed, create one.

		name = p_child->get_class();
	}

	const Entity *const *existing = data.children.getptr(name);
	if (!existing || *existing == p_child) { // Unused, or is current node.
		return;
	}

	// Extract trailing number
	String name_string = name;
	String nums;
	for (int i = name_string.length() - 1; i >= 0; i--) {
		char32_t n = name_string[i];
		if (is_digit(n)) {
			nums = String::chr(name_string[i]) + nums;
		} else {
			break;
		}
	}

	String nnsep = _get_name_num_separator();
	int name_last_index = name_string.length() - nnsep.length() - nums.length();

	// Assign the base name + separator to name if we have numbers preceded by a separator
	if (nums.length() > 0 && name_string.substr(name_last_index, nnsep.length()) == nnsep) {
		name_string = name_string.substr(0, name_last_index + nnsep.length());
	} else {
		nums = "";
	}

	for (;;) {
		StringName attempt = name_string + nums;

		existing = data.children.getptr(attempt);
		bool exists = existing != nullptr && *existing != p_child;

		if (!exists) {
			name = attempt;
			return;
		} else {
			if (nums.length() == 0) {
				// Name was undecorated so skip to 2 for a more natural result
				nums = "2";
				name_string += nnsep; // Add separator because nums.length() > 0 was false
			} else {
				nums = increase_numeric_string(nums);
			}
		}
	}
}

Entity::InternalMode Entity::get_internal_mode() const {
	return data.internal_mode;
}

void Entity::_add_child_nocheck(Entity *p_child, const StringName &p_name, InternalMode p_internal_mode) {
	//add a child node quickly, without name validation

	p_child->data.name = p_name;
	data.children.insert(p_name, p_child);

	p_child->data.internal_mode = p_internal_mode;

	bool can_push_back = false;
	switch (p_internal_mode) {
		case INTERNAL_MODE_FRONT: {
			p_child->data.index = data.internal_children_front_count_cache++;
			// Safe to push back when ordinary and back children are empty.
			can_push_back = (data.external_children_count_cache + data.internal_children_back_count_cache) == 0;
		} break;
		case INTERNAL_MODE_BACK: {
			p_child->data.index = data.internal_children_back_count_cache++;
			// Safe to push back when cache is valid.
			can_push_back = true;
		} break;
		case INTERNAL_MODE_DISABLED: {
			p_child->data.index = data.external_children_count_cache++;
			// Safe to push back when back children are empty.
			can_push_back = data.internal_children_back_count_cache == 0;
		} break;
	}

	p_child->data.parent = this;

	if (!data.children_cache_dirty && can_push_back) {
		data.children_cache.push_back(p_child);
	} else {
		data.children_cache_dirty = true;
	}

	p_child->notification(NOTIFICATION_PARENTED);

	if (data.tree) {
		p_child->_set_tree(data.tree);
	}

	/* Notify */
	add_child_notify(p_child);
	notification(NOTIFICATION_CHILD_ORDER_CHANGED);
	emit_signal(SNAME("child_order_changed"));
}

void Entity::add_child(RequiredParam<Entity> p_child, bool p_force_readable_name, InternalMode p_internal) {
	ERR_FAIL_COND_MSG(data.tree && !Thread::is_main_thread(), "Adding children to a node inside the Stage is only allowed from the main thread. Use call_deferred(\"add_child\",node).");

	ERR_THREAD_GUARD
	EXTRACT_PARAM_OR_FAIL(child, p_child);
	ERR_FAIL_COND_MSG(child == this, vformat("Can't add child '%s' to itself.", child->get_name())); // adding to itself!
	ERR_FAIL_COND_MSG(child->data.parent, vformat("Can't add child '%s' to '%s', already has a parent '%s'.", child->get_name(), get_name(), child->data.parent->get_name())); //Fail if node has a parent
#ifdef DEBUG_ENABLED
	ERR_FAIL_COND_MSG(child->is_ancestor_of(this), vformat("Can't add child '%s' to '%s' as it would result in a cyclic dependency since '%s' is already a parent of '%s'.", child->get_name(), get_name(), child->get_name(), get_name()));
#endif
	ERR_FAIL_COND_MSG(data.blocked > 0, "Parent node is busy setting up children, `add_child()` failed. Consider using `add_child.call_deferred(child)` instead.");

	_validate_child_name(child, p_force_readable_name);

#ifdef DEBUG_ENABLED
	if (child->data.owner && !child->data.owner->is_ancestor_of(child)) {
		// Owner of p_child should be ancestor of p_child.
		WARN_PRINT(vformat("Adding '%s' as child to '%s' will make owner '%s' inconsistent. Consider unsetting the owner beforehand.", child->get_name(), get_name(), child->data.owner->get_name()));
	}
#endif // DEBUG_ENABLED

	_add_child_nocheck(child, child->data.name, p_internal);
}

void Entity::add_sibling(RequiredParam<Entity> p_sibling, bool p_force_readable_name) {
	ERR_FAIL_COND_MSG(data.tree && !Thread::is_main_thread(), "Adding a sibling to a node inside the Stage is only allowed from the main thread. Use call_deferred(\"add_sibling\",node).");
	EXTRACT_PARAM_OR_FAIL(sibling, p_sibling);
	ERR_FAIL_COND_MSG(sibling == this, vformat("Can't add sibling '%s' to itself.", sibling->get_name())); // adding to itself!
	ERR_FAIL_NULL(data.parent);
	ERR_FAIL_COND_MSG(data.parent->data.blocked > 0, "Parent node is busy setting up children, `add_sibling()` failed. Consider using `add_sibling.call_deferred(sibling)` instead.");

	data.parent->add_child(sibling, p_force_readable_name, data.internal_mode);
	data.parent->_update_children_cache();
	data.parent->_move_child(sibling, get_index() + 1);
}

void Entity::remove_child(RequiredParam<Entity> p_child) {
	ERR_FAIL_COND_MSG(data.tree && !Thread::is_main_thread(), "Removing children from a node inside the Stage is only allowed from the main thread. Use call_deferred(\"remove_child\",node).");
	EXTRACT_PARAM_OR_FAIL(child, p_child);
	ERR_FAIL_COND_MSG(data.blocked > 0, "Parent node is busy adding/removing children, `remove_child()` can't be called at this time. Consider using `remove_child.call_deferred(child)` instead.");
	ERR_FAIL_COND(child->data.parent != this);

	/**
	 *  Do not change the data.internal_children*cache counters here.
	 *  Because if nodes are re-added, the indices can remain
	 *  greater-than-everything indices and children added remain
	 *  properly ordered.
	 *
	 *  All children indices and counters will be updated next time the
	 *  cache is re-generated.
	 */

	data.blocked++;
	child->_set_tree(nullptr);

	remove_child_notify(child);
	child->notification(NOTIFICATION_UNPARENTED);

	data.blocked--;

	if (!data.children_cache_dirty && !data.children_cache.is_empty() && data.children_cache[data.children_cache.size() - 1] == child) {
		// Removing the last child keeps the cache and the counters valid, so pop it
		// instead of dirtying the cache. This keeps interleaving removals with reads
		// (like get_child()) linear, as long as children are removed back to front.
		data.children_cache.resize(data.children_cache.size() - 1);
		switch (child->data.internal_mode) {
			case INTERNAL_MODE_DISABLED: {
				data.external_children_count_cache--;
			} break;
			case INTERNAL_MODE_FRONT: {
				data.internal_children_front_count_cache--;
			} break;
			case INTERNAL_MODE_BACK: {
				data.internal_children_back_count_cache--;
			} break;
		}
	} else {
		data.children_cache_dirty = true;
	}
	bool success = data.children.erase(child->data.name);
	ERR_FAIL_COND_MSG(!success, "Children name does not match parent name in hashtable, this is a bug.");

	child->data.parent = nullptr;
	child->data.index = -1;

	notification(NOTIFICATION_CHILD_ORDER_CHANGED);
	emit_signal(SNAME("child_order_changed"));

	if (data.tree) {
		child->_propagate_after_exit_tree();
	}
}

void Entity::_update_children_cache_impl() const {
	// Assign children
	data.children_cache.resize(data.children.size());
	int idx = 0;
	for (const KeyValue<StringName, Entity *> &K : data.children) {
		data.children_cache[idx] = K.value;
		idx++;
	}
	// Sort them
	data.children_cache.sort_custom<ComparatorByIndex>();
	// Update indices
	data.external_children_count_cache = 0;
	data.internal_children_back_count_cache = 0;
	data.internal_children_front_count_cache = 0;

	for (uint32_t i = 0; i < data.children_cache.size(); i++) {
		switch (data.children_cache[i]->data.internal_mode) {
			case INTERNAL_MODE_DISABLED: {
				data.children_cache[i]->data.index = data.external_children_count_cache++;
			} break;
			case INTERNAL_MODE_FRONT: {
				data.children_cache[i]->data.index = data.internal_children_front_count_cache++;
			} break;
			case INTERNAL_MODE_BACK: {
				data.children_cache[i]->data.index = data.internal_children_back_count_cache++;
			} break;
		}
	}
	data.children_cache_dirty = false;
}

template <bool p_include_internal>
Iterable<Entity::ChildrenIterator> Entity::iterate_children() const {
	// The thread guard is omitted for performance reasons.
	// ERR_THREAD_GUARD_V(Iterable<ChildrenIterator>(nullptr, nullptr));

	_update_children_cache();
	const uint32_t size = data.children_cache.size();
	// Might be null, but then size and internal counts are also 0.
	Entity **ptr = data.children_cache.ptr();

	if constexpr (p_include_internal) {
		return Iterable(ChildrenIterator(ptr), ChildrenIterator(ptr + size));
	} else {
		return Iterable(ChildrenIterator(ptr + data.internal_children_front_count_cache), ChildrenIterator(ptr + size - data.internal_children_back_count_cache));
	}
}

template Iterable<Entity::ChildrenIterator> Entity::iterate_children<true>() const;
template Iterable<Entity::ChildrenIterator> Entity::iterate_children<false>() const;

int Entity::get_child_count(bool p_include_internal) const {
	ERR_THREAD_GUARD_V(0);
	if (p_include_internal) {
		return data.children.size();
	}

	_update_children_cache();
	return data.children_cache.size() - data.internal_children_front_count_cache - data.internal_children_back_count_cache;
}

Entity *Entity::get_child(int p_index, bool p_include_internal) const {
	ERR_THREAD_GUARD_V(nullptr);
	_update_children_cache();

	if (p_include_internal) {
		if (p_index < 0) {
			p_index += data.children_cache.size();
		}
		ERR_FAIL_INDEX_V(p_index, (int)data.children_cache.size(), nullptr);
		return data.children_cache[p_index];
	} else {
		if (p_index < 0) {
			p_index += (int)data.children_cache.size() - data.internal_children_front_count_cache - data.internal_children_back_count_cache;
		}
		ERR_FAIL_INDEX_V(p_index, (int)data.children_cache.size() - data.internal_children_front_count_cache - data.internal_children_back_count_cache, nullptr);
		p_index += data.internal_children_front_count_cache;
		return data.children_cache[p_index];
	}
}

TypedArray<Entity> Entity::get_children(bool p_include_internal) const {
	ERR_THREAD_GUARD_V(TypedArray<Entity>());
	_update_children_cache();

	TypedArray<Entity> children;

	if (p_include_internal) {
		children.resize(data.children_cache.size());

		Array::Iterator itr = children.begin();
		for (const Entity *child : data.children_cache) {
			*itr = child;
			++itr;
		}
	} else {
		const int size = data.children_cache.size() - data.internal_children_back_count_cache;
		children.resize(size - data.internal_children_front_count_cache);

		Array::Iterator itr = children.begin();
		for (int i = data.internal_children_front_count_cache; i < size; i++) {
			*itr = data.children_cache[i];
			++itr;
		}
	}

	return children;
}

Entity *Entity::_get_child_by_name(const StringName &p_name) const {
	const Entity *const *node = data.children.getptr(p_name);
	if (node) {
		return const_cast<Entity *>(*node);
	} else {
		return nullptr;
	}
}

Entity *Entity::get_entity_or_null(const EntityPath &p_path) const {
	ERR_THREAD_GUARD_V(nullptr);
	if (p_path.is_empty()) {
		return nullptr;
	}

	ERR_FAIL_COND_V_MSG(!data.tree && p_path.is_absolute(), nullptr, "Can't use get_entity() with absolute paths from outside the active scene tree.");

	Entity *current = nullptr;
	Entity *root = nullptr;

	if (!p_path.is_absolute()) {
		current = const_cast<Entity *>(this); //start from this
	} else {
		root = const_cast<Entity *>(this);
		while (root->data.parent) {
			root = root->data.parent; //start from root
		}
	}

	for (int i = 0; i < p_path.get_name_count(); i++) {
		StringName name = p_path.get_name(i);
		Entity *next = nullptr;

		if (name == SNAME(".")) {
			next = current;

		} else if (name == SNAME("..")) {
			if (current == nullptr || !current->data.parent) {
				return nullptr;
			}

			next = current->data.parent;
		} else if (current == nullptr) {
			if (name == root->get_name()) {
				next = root;
			}

		} else if (name.is_node_unique_name()) {
			Entity **unique = current->data.owned_unique_nodes.getptr(name);
			if (!unique && current->data.owner) {
				unique = current->data.owner->data.owned_unique_nodes.getptr(name);
			}
			if (!unique) {
				return nullptr;
			}
			next = *unique;
		} else {
			next = nullptr;
			const Entity *const *node = current->data.children.getptr(name);
			if (node) {
				next = const_cast<Entity *>(*node);
			} else {
				return nullptr;
			}
		}
		current = next;
	}

	return current;
}

Entity *Entity::get_entity(const EntityPath &p_path) const {
	Entity *node = get_entity_or_null(p_path);

	if (unlikely(!node)) {
		const String desc = get_description();
		if (p_path.is_absolute()) {
			ERR_FAIL_V_MSG(nullptr,
					vformat(R"(Entity not found: "%s" (absolute path attempted from "%s").)", p_path, desc));
		} else {
			ERR_FAIL_V_MSG(nullptr,
					vformat(R"(Entity not found: "%s" (relative to "%s").)", p_path, desc));
		}
	}

	return node;
}

bool Entity::has_entity(const EntityPath &p_path) const {
	return get_entity_or_null(p_path) != nullptr;
}

// Finds the first child node (in tree order) whose name matches the given pattern.
// Can be recursive or not, and limited to owned nodes.
Entity *Entity::find_child(const String &p_pattern, bool p_recursive, bool p_owned) const {
	ERR_THREAD_GUARD_V(nullptr);
	ERR_FAIL_COND_V(p_pattern.is_empty(), nullptr);
	_update_children_cache();
	Entity *const *cptr = data.children_cache.ptr();
	int ccount = data.children_cache.size();
	for (int i = 0; i < ccount; i++) {
		if (p_owned && !cptr[i]->data.owner) {
			continue;
		}
		if (cptr[i]->data.name.string().match(p_pattern)) {
			return cptr[i];
		}

		if (!p_recursive) {
			continue;
		}

		Entity *ret = cptr[i]->find_child(p_pattern, true, p_owned);
		if (ret) {
			return ret;
		}
	}
	return nullptr;
}

// Finds child nodes based on their name using pattern matching, or class name,
// or both (either pattern or type can be left empty).
// Can be recursive or not, and limited to owned nodes.
TypedArray<Entity> Entity::find_children(const String &p_pattern, const String &p_type, bool p_recursive, bool p_owned) const {
	ERR_THREAD_GUARD_V(TypedArray<Entity>());
	TypedArray<Entity> ret;
	ERR_FAIL_COND_V(p_pattern.is_empty() && p_type.is_empty(), ret);
	_find_children_into(p_pattern, p_type, p_recursive, p_owned, ret);
	return ret;
}

void Entity::_find_children_into(const String &p_pattern, const String &p_type, bool p_recursive, bool p_owned, TypedArray<Entity> &r_found) const {
	ERR_THREAD_GUARD
	_update_children_cache();
	Entity *const *cptr = data.children_cache.ptr();
	int ccount = data.children_cache.size();
	for (int i = 0; i < ccount; i++) {
		if (p_owned && !cptr[i]->data.owner) {
			continue;
		}

		if (p_pattern.is_empty() || cptr[i]->data.name.string().match(p_pattern)) {
			if (p_type.is_empty() || cptr[i]->is_class(p_type)) {
				r_found.append(cptr[i]);
			} else if (cptr[i]->get_script_instance()) {
				Ref<Script> scr = cptr[i]->get_script_instance()->get_script();
				while (scr.is_valid()) {
					if ((ScriptSystem::is_global_class(p_type) && ScriptSystem::get_global_class_path(p_type) == scr->get_path()) || p_type == scr->get_path()) {
						r_found.append(cptr[i]);
						break;
					}

					scr = scr->get_base_script();
				}
			}
		}

		if (p_recursive) {
			cptr[i]->_find_children_into(p_pattern, p_type, true, p_owned, r_found);
		}
	}
}

void Entity::reparent(RequiredParam<Entity> p_parent, bool p_keep_global_transform) {
	ERR_THREAD_GUARD
	EXTRACT_PARAM_OR_FAIL(parent, p_parent);
	ERR_FAIL_NULL_MSG(data.parent, "Entity needs a parent to be reparented.");
	ERR_FAIL_COND_MSG(parent == this, vformat("Can't reparent '%s' to itself.", parent->get_name()));

	if (parent == data.parent) {
		return;
	}

	bool preserve_owner = data.owner && (data.owner == parent || data.owner->is_ancestor_of(parent));
	Entity *owner_temp = data.owner;
	LocalVector<Entity *> common_parents;

	// If the new parent is related to the owner, find all children of the reparented node who have the same owner so that we can reassign them.
	if (preserve_owner) {
		LocalVector<Entity *> to_visit;

		to_visit.push_back(this);
		common_parents.push_back(this);

		while (to_visit.size() > 0) {
			Entity *check = to_visit[to_visit.size() - 1];
			to_visit.resize(to_visit.size() - 1);

			for (int i = 0; i < check->get_child_count(false); i++) {
				Entity *child = check->get_child(i, false);
				to_visit.push_back(child);
				if (child->data.owner == owner_temp) {
					common_parents.push_back(child);
				}
			}
		}
	}

	data.parent->remove_child(this);
	parent->add_child(this);

	// Reassign the old owner to those found nodes.
	if (preserve_owner) {
		for (Entity *E : common_parents) {
			E->set_owner(owner_temp);
		}
	}
}

Entity *Entity::get_parent() const {
	return data.parent;
}

Entity *Entity::find_parent(const String &p_pattern) const {
	ERR_THREAD_GUARD_V(nullptr);
	Entity *p = data.parent;
	while (p) {
		if (p->data.name.string().match(p_pattern)) {
			return p;
		}
		p = p->data.parent;
	}

	return nullptr;
}

void Entity::set_unique_scene_id(int32_t p_unique_id) {
	data.unique_scene_id = p_unique_id;
}

int32_t Entity::get_unique_scene_id() const {
	return data.unique_scene_id;
}

Window *Entity::get_window() const {
	ERR_THREAD_GUARD_V(nullptr);
	Viewport *vp = get_viewport();
	if (vp) {
		return vp->get_base_window();
	}
	return nullptr;
}

Window *Entity::get_non_popup_window() const {
	Window *w = get_window();
	while (w && w->is_popup()) {
		w = w->get_parent_visible_window();
	}
	return w;
}

Window *Entity::get_last_exclusive_window() const {
	Window *w = get_window();
	while (w && w->get_exclusive_child()) {
		w = w->get_exclusive_child();
	}

	return w;
}

bool Entity::is_ancestor_of(RequiredParam<const Entity> p_node) const {
	EXTRACT_PARAM_OR_FAIL_V(node, p_node, false);

	const Entity *n = node;
	if (n == this) {
		return false;
	}

	if (is_inside_stage() && node->data.tree == data.tree) {
		const int depth = data.depth;
		while (n->data.depth > depth) {
			n = n->data.parent;
		}
		return n == this;
	}

	n = node->data.parent;
	while (n) {
		if (n == this) {
			return true;
		}
		n = n->data.parent;
	}

	return false;
}

bool Entity::is_greater_than(RequiredParam<const Entity> p_node) const {
	// parent->get_child(1) > parent->get_child(0) > parent

	EXTRACT_PARAM_OR_FAIL_V(node, p_node, false);
	ERR_FAIL_COND_V(!data.tree, false);
	ERR_FAIL_COND_V(node->data.tree != data.tree, false);

	ERR_FAIL_COND_V(data.depth < 0, false);
	ERR_FAIL_COND_V(node->data.depth < 0, false);

	_update_children_cache();

	bool this_is_deeper = this->data.depth > node->data.depth;

	const Entity *deep = this;
	const Entity *shallow = node;
	if (!this_is_deeper) {
		deep = node;
		shallow = this;
	}

	while (deep->data.depth > shallow->data.depth) {
		deep = deep->data.parent;
	}

	if (deep == shallow) { // Shallow is ancestor of deep.
		return this_is_deeper;
	}

	while (deep->data.parent != shallow->data.parent) {
		deep = deep->data.parent;
		shallow = shallow->data.parent;
	}

	return (deep->get_index() > shallow->get_index()) == this_is_deeper;
}

void Entity::get_owned_by(Entity *p_by, List<Entity *> *p_owned) {
	if (data.owner == p_by) {
		p_owned->push_back(this);
	}

	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->get_owned_by(p_by, p_owned);
	}
}

void Entity::_set_owner_nocheck(Entity *p_owner) {
	if (data.owner == p_owner) {
		return;
	}

	ERR_FAIL_COND(data.owner);
	data.owner = p_owner;
	data.owner->data.owned.push_back(this);
	data.OW = data.owner->data.owned.back();

	owner_changed_notify();
}

void Entity::_release_unique_name_in_owner() {
	ERR_FAIL_NULL(data.owner); // Safety check.
	StringName key = StringName(UNIQUE_NODE_PREFIX + data.name.string());
	Entity **which = data.owner->data.owned_unique_nodes.getptr(key);
	if (which == nullptr || *which != this) {
		return; // Ignore.
	}
	data.owner->data.owned_unique_nodes.erase(key);
}

void Entity::_acquire_unique_name_in_owner() {
	ERR_FAIL_NULL(data.owner); // Safety check.
	StringName key = StringName(UNIQUE_NODE_PREFIX + data.name.string());
	Entity **which = data.owner->data.owned_unique_nodes.getptr(key);
	if (which != nullptr && *which != this) {
		String which_path = String(is_inside_stage() ? (*which)->get_path() : data.owner->get_path_to(*which));
		WARN_PRINT(vformat("Setting node name '%s' to be unique within scene for '%s', but it's already claimed by '%s'.\n'%s' is no longer set as having a unique name.",
				get_name(), is_inside_stage() ? get_path() : data.owner->get_path_to(this), which_path, which_path));
		data.unique_name_in_owner = false;
		return;
	}
	data.owner->data.owned_unique_nodes[key] = this;
}

void Entity::set_unique_name_in_owner(bool p_enabled) {
	ERR_MAIN_THREAD_GUARD
	if (data.unique_name_in_owner == p_enabled) {
		return;
	}

	if (data.unique_name_in_owner && data.owner != nullptr) {
		_release_unique_name_in_owner();
	}
	data.unique_name_in_owner = p_enabled;

	if (data.unique_name_in_owner && data.owner != nullptr) {
		_acquire_unique_name_in_owner();
	}

	update_configuration_warnings();
	_emit_editor_state_changed();
}

bool Entity::is_unique_name_in_owner() const {
	return data.unique_name_in_owner;
}

void Entity::set_owner(Entity *p_owner) {
	ERR_MAIN_THREAD_GUARD
	if (data.owner) {
		_clean_up_owner();
	}

	ERR_FAIL_COND(p_owner == this);

	if (!p_owner) {
		return;
	}

	bool owner_valid = p_owner->is_ancestor_of(this);

	ERR_FAIL_COND_MSG(!owner_valid, "Invalid owner. Owner must be an ancestor in the tree.");

	_set_owner_nocheck(p_owner);

	if (data.unique_name_in_owner) {
		_acquire_unique_name_in_owner();
	}

	_emit_editor_state_changed();
}

Entity *Entity::get_owner() const {
	return data.owner;
}

void Entity::_clean_up_owner() {
	ERR_FAIL_NULL(data.owner); // Safety check.

	if (data.unique_name_in_owner) {
		_release_unique_name_in_owner();
	}
	data.owner->data.owned.erase(data.OW);
	data.owner = nullptr;
	data.OW = nullptr;
}

static int ancestor_count(const Entity *p_entity) {
	int count = 0;
	for (const Entity *n = p_entity; n; n = n->get_parent()) {
		count++;
	}
	return count;
}

// An entity counts as its own ancestor. Walking both sides to the same depth finds the meeting
// point without allocating a set of visited entities. Returns null when the two share no ancestor.
static const Entity *lowest_common_ancestor(const Entity *p_a, const Entity *p_b) {
	int depth_a = ancestor_count(p_a);
	int depth_b = ancestor_count(p_b);
	for (; depth_a > depth_b; depth_a--) {
		p_a = p_a->get_parent();
	}
	for (; depth_b > depth_a; depth_b--) {
		p_b = p_b->get_parent();
	}
	while (p_a != p_b) {
		p_a = p_a->get_parent();
		p_b = p_b->get_parent();
	}
	return p_a;
}

Entity *Entity::find_common_parent_with(const Entity *p_node) const {
	return const_cast<Entity *>(lowest_common_ancestor(this, p_node));
}

EntityPath Entity::get_path_to(RequiredParam<const Entity> p_node, bool p_use_unique_path) const {
	EXTRACT_PARAM_OR_FAIL_V(node, p_node, EntityPath());

	if (this == node) {
		return EntityPath(".");
	}

	const Entity *common_parent = lowest_common_ancestor(this, node);
	ERR_FAIL_NULL_V_MSG(common_parent, EntityPath(), vformat("No path can be resolved between the nodes %s and %s as they share no common ancestor.", get_description(true), node->get_description(true)));

	const Entity *n = this;
	Vector<StringName> path;
	StringName up = String("..");

	if (p_use_unique_path) {
		n = node;

		bool is_detected = false;
		while (n != common_parent) {
			if (n->is_unique_name_in_owner() && n->get_owner() == get_owner()) {
				path.push_back(UNIQUE_NODE_PREFIX + String(n->get_name()));
				is_detected = true;
				break;
			}
			path.push_back(n->get_name());
			n = n->data.parent;
		}

		if (!is_detected) {
			n = this;

			String detected_name;
			int up_count = 0;
			while (n != common_parent) {
				if (n->is_unique_name_in_owner() && n->get_owner() == get_owner()) {
					detected_name = n->get_name();
					up_count = 0;
				}
				up_count++;
				n = n->data.parent;
			}

			for (int i = 0; i < up_count; i++) {
				path.push_back(up);
			}

			if (!detected_name.is_empty()) {
				path.push_back(UNIQUE_NODE_PREFIX + detected_name);
			}
		}
	} else {
		n = node;

		while (n != common_parent) {
			path.push_back(n->get_name());
			n = n->data.parent;
		}

		n = this;

		while (n != common_parent) {
			path.push_back(up);
			n = n->data.parent;
		}
	}

	path.reverse();

	return EntityPath(path, false);
}

EntityPath Entity::get_path() const {
	ERR_FAIL_COND_V_MSG(!is_inside_stage(), EntityPath(), "Cannot get path of node as it is not in a scene tree.");

	if (data.path_cache) {
		return *data.path_cache;
	}

	const Entity *n = this;

	Vector<StringName> path;
	path.resize(data.depth);

	StringName *ptrw = path.ptrw();
	while (n) {
		ptrw[n->data.depth - 1] = n->get_name();
		n = n->data.parent;
	}

	data.path_cache = memnew(EntityPath(path, true));

	return *data.path_cache;
}

bool Entity::is_in_group(const StringName &p_identifier) const {
	ERR_THREAD_GUARD_V(false);
	return data.grouped.has(p_identifier);
}

void Entity::add_to_group(const StringName &p_identifier, bool p_persistent) {
	ERR_THREAD_GUARD
	ERR_FAIL_COND_MSG(p_identifier.is_empty(), vformat("Cannot add node '%s' to a group with an empty name.", get_name()));

	if (data.grouped.has(p_identifier)) {
		return;
	}

	GroupData gd;

	if (data.tree) {
		gd.group = data.tree->add_to_group(p_identifier, this);
	} else {
		gd.group = nullptr;
	}

	gd.persistent = p_persistent;

	data.grouped[p_identifier] = gd;
	if (p_persistent) {
		_emit_editor_state_changed();
	}
}

void Entity::remove_from_group(const StringName &p_identifier) {
	ERR_THREAD_GUARD
	HashMap<StringName, GroupData>::Iterator E = data.grouped.find(p_identifier);

	if (!E) {
		return;
	}

#ifdef TOOLS_ENABLED
	bool persistent = E->value.persistent;
#endif

	if (data.tree) {
		data.tree->remove_from_group(E->key, this);
	}

	data.grouped.remove(E);

#ifdef TOOLS_ENABLED
	if (persistent) {
		_emit_editor_state_changed();
	}
#endif
}

TypedArray<StringName> Entity::_get_groups() const {
	TypedArray<StringName> groups;
	List<GroupInfo> gi;
	get_groups(&gi);
	for (const GroupInfo &E : gi) {
		groups.push_back(E.name);
	}

	return groups;
}

void Entity::get_groups(List<GroupInfo> *p_groups) const {
	ERR_THREAD_GUARD
	for (const KeyValue<StringName, GroupData> &E : data.grouped) {
		GroupInfo gi;
		gi.name = E.key;
		gi.persistent = E.value.persistent;
		p_groups->push_back(gi);
	}
}

int Entity::get_persistent_group_count() const {
	ERR_THREAD_GUARD_V(0);
	int count = 0;

	for (const KeyValue<StringName, GroupData> &E : data.grouped) {
		if (E.value.persistent) {
			count += 1;
		}
	}

	return count;
}

void Entity::print_entity_tree_pretty() {
	print_line(_get_entity_tree_string_pretty("", true));
}

void Entity::print_entity_tree() {
	print_line(_get_entity_tree_string(this));
}

String Entity::_get_entity_tree_string_pretty(const String &p_prefix, bool p_last) {
	String new_prefix = p_last ? String::utf8(" ┖╴") : String::utf8(" ┠╴");
	_update_children_cache();
	String return_tree = p_prefix + new_prefix + String(get_name()) + "\n";
	for (uint32_t i = 0; i < data.children_cache.size(); i++) {
		new_prefix = p_last ? String::utf8("   ") : String::utf8(" ┃ ");
		return_tree += data.children_cache[i]->_get_entity_tree_string_pretty(p_prefix + new_prefix, i == data.children_cache.size() - 1);
	}
	return return_tree;
}

String Entity::get_entity_tree_string_pretty() {
	return _get_entity_tree_string_pretty("", true);
}

String Entity::_get_entity_tree_string(const Entity *p_node) {
	_update_children_cache();
	String return_tree = String(p_node->get_path_to(this)) + "\n";
	for (uint32_t i = 0; i < data.children_cache.size(); i++) {
		return_tree += data.children_cache[i]->_get_entity_tree_string(p_node);
	}
	return return_tree;
}

String Entity::get_entity_tree_string() {
	return _get_entity_tree_string(this);
}

void Entity::propagate_notification(int p_notification) {
	ERR_THREAD_GUARD
	data.blocked++;
	notification(p_notification);

	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->propagate_notification(p_notification);
	}
	data.blocked--;
}

void Entity::propagate_call(const StringName &p_method, const Array &p_args, const bool p_parent_first) {
	ERR_THREAD_GUARD
	data.blocked++;

	if (p_parent_first && has_method(p_method)) {
		callv(p_method, p_args);
	}

	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->propagate_call(p_method, p_args, p_parent_first);
	}

	if (!p_parent_first && has_method(p_method)) {
		callv(p_method, p_args);
	}

	data.blocked--;
}

void Entity::_propagate_replace_owner(Entity *p_owner, Entity *p_by_owner) {
	if (get_owner() == p_owner) {
		set_owner(p_by_owner);
	}

	data.blocked++;
	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->_propagate_replace_owner(p_owner, p_by_owner);
	}
	data.blocked--;
}

RequiredResult<Tween> Entity::create_tween() {
	ERR_THREAD_GUARD_V(Ref<Tween>());

	Stage *tree = data.tree;
	if (!tree) {
		tree = Stage::get_singleton();
	}
	ERR_FAIL_NULL_V_MSG(tree, Ref<Tween>(), "No available Stage to create the Tween.");

	Ref<Tween> tween = tree->create_tween();
	tween->bind_entity(this);
	return tween;
}

void Entity::set_scene_file_path(const String &p_scene_file_path) {
	ERR_THREAD_GUARD
	data.scene_file_path = p_scene_file_path;
	_emit_editor_state_changed();
}

String Entity::get_scene_file_path() const {
	return data.scene_file_path;
}

void Entity::set_editor_description(const String &p_editor_description) {
	ERR_THREAD_GUARD
	if (data.editor_description == p_editor_description) {
		return;
	}

	data.editor_description = p_editor_description;
	emit_signal(SNAME("editor_description_changed"), this);
}

String Entity::get_editor_description() const {
	return data.editor_description;
}

void Entity::set_editable_instance(RequiredParam<Entity> p_node, bool p_editable) {
	ERR_THREAD_GUARD
	EXTRACT_PARAM_OR_FAIL(node, p_node);
	ERR_FAIL_COND(!is_ancestor_of(node));
	if (!p_editable) {
		node->data.editable_instance = false;
		// Avoid this flag being needlessly saved;
		// also give more visual feedback if editable children are re-enabled
		set_display_folded(false);
	} else {
		node->data.editable_instance = true;
	}

	node->_emit_editor_state_changed();
}

bool Entity::is_editable_instance(const Entity *p_node) const {
	if (!p_node) {
		return false; // Easier, null is never editable. :)
	}
	ERR_FAIL_COND_V(!is_ancestor_of(p_node), false);
	return p_node->data.editable_instance;
}

Entity *Entity::get_deepest_editable_node(Entity *p_start_node) const {
	ERR_THREAD_GUARD_V(nullptr);
	ERR_FAIL_NULL_V(p_start_node, nullptr);
	ERR_FAIL_COND_V(!is_ancestor_of(p_start_node), p_start_node);

	const Entity *iterated_item = p_start_node;
	Entity *node = p_start_node;

	while (iterated_item->get_owner() && iterated_item->get_owner() != this) {
		if (!is_editable_instance(iterated_item->get_owner())) {
			node = iterated_item->get_owner();
		}

		iterated_item = iterated_item->get_owner();
	}

	return node;
}

#ifdef TOOLS_ENABLED
void Entity::set_property_pinned(const String &p_property, bool p_pinned) {
	ERR_THREAD_GUARD
	bool current_pinned = false;
	Array pinned = get_meta("_edit_pinned_properties_", Array());
	StringName psa = get_property_store_alias(p_property);
	current_pinned = pinned.has(psa);

	if (current_pinned != p_pinned) {
		if (p_pinned) {
			pinned.append(psa);
		} else {
			pinned.erase(psa);
		}
	}

	if (pinned.is_empty()) {
		remove_meta("_edit_pinned_properties_");
	} else {
		set_meta("_edit_pinned_properties_", pinned);
	}
}

bool Entity::is_property_pinned(const StringName &p_property) const {
	Array pinned = get_meta("_edit_pinned_properties_", Array());
	StringName psa = get_property_store_alias(p_property);
	return pinned.has(psa);
}

StringName Entity::get_property_store_alias(const StringName &p_property) const {
	return p_property;
}

bool Entity::is_part_of_edited_scene() const {
	return Engine::get_singleton()->is_editor_hint() && is_inside_stage() && data.tree->get_edited_scene_root() &&
			data.tree->get_edited_scene_root()->get_parent() && // Defend against edge cases when creating new scenes and they are not fully added to the tree yet.
			data.tree->get_edited_scene_root()->get_parent()->is_ancestor_of(this);
}
#endif

void Entity::get_storable_properties(HashSet<StringName> &r_storable_properties) const {
	ERR_THREAD_GUARD
	List<PropertyInfo> property_list;
	get_property_list(&property_list);
	for (const PropertyInfo &pi : property_list) {
		if ((pi.usage & PROPERTY_USAGE_STORAGE)) {
			r_storable_properties.insert(pi.name);
		}
	}
}

void Entity::set_scene_instance_state(const Ref<SceneState> &p_state) {
	ERR_THREAD_GUARD
	data.instance_state = p_state;
}

Ref<SceneState> Entity::get_scene_instance_state() const {
	return data.instance_state;
}

void Entity::set_scene_inherited_state(const Ref<SceneState> &p_state) {
	ERR_THREAD_GUARD
	data.inherited_state = p_state;
	_emit_editor_state_changed();
}

Ref<SceneState> Entity::get_scene_inherited_state() const {
	return data.inherited_state;
}

void Entity::set_scene_instance_load_placeholder(bool p_enable) {
	data.use_placeholder = p_enable;
}

bool Entity::get_scene_instance_load_placeholder() const {
	return data.use_placeholder;
}

Entity *Entity::_duplicate(int p_flags, HashMap<const Entity *, Entity *> *r_duplimap) const {
	ERR_THREAD_GUARD_V(nullptr);
	Entity *node = nullptr;

	bool instantiated = false;

	if (Object::cast_to<InstancePlaceholder>(this)) {
		const InstancePlaceholder *ip = Object::cast_to<const InstancePlaceholder>(this);
		InstancePlaceholder *nip = memnew(InstancePlaceholder);
		nip->set_instance_path(ip->get_instance_path());
		node = nip;

	} else if ((p_flags & DUPLICATE_USE_INSTANTIATION) && is_instance()) {
		Ref<Prefab> res = ResourceLoader::load(get_scene_file_path());
		ERR_FAIL_COND_V(res.is_null(), nullptr);
		Prefab::GenEditState edit_state = Prefab::GEN_EDIT_STATE_DISABLED;
#ifdef TOOLS_ENABLED
		if (p_flags & DUPLICATE_FROM_EDITOR) {
			edit_state = Prefab::GEN_EDIT_STATE_INSTANCE;
		}
#endif
		node = res->instantiate(edit_state);
		ERR_FAIL_NULL_V(node, nullptr);
		node->set_scene_instance_load_placeholder(get_scene_instance_load_placeholder());

		instantiated = true;

	} else {
		Object *obj = ClassRegistry::instantiate(get_class());
		ERR_FAIL_NULL_V(obj, nullptr);
		node = Object::cast_to<Entity>(obj);
		if (!node) {
			memdelete(obj);
		}
		ERR_FAIL_NULL_V(node, nullptr);
	}

	if (is_instance()) {
		node->set_scene_file_path(get_scene_file_path());
		node->data.editable_instance = data.editable_instance;
	}

	List<const Entity *> hidden_roots;

	if (instantiated) {
		// Since nodes in the instantiated hierarchy won't be duplicated explicitly, we need to make an inventory
		// of all the nodes in the tree of the instantiated scene in order to transfer the values of the properties

		List<const Entity *> node_tree;
		node_tree.push_front(this);

		Vector<const Entity *> instance_roots;
		instance_roots.push_back(this);

		for (List<const Entity *>::Element *N = node_tree.front(); N; N = N->next()) {
			for (int i = 0; i < N->get()->get_child_count(false); ++i) {
				Entity *descendant = N->get()->get_child(i, false);

				// Skip nodes not really belonging to the instantiated hierarchy; they'll be processed normally later
				// but remember non-instantiated nodes that are hidden below instantiated ones
				if (!instance_roots.has(descendant->get_owner())) {
					if (descendant->get_parent() && descendant->get_parent() != this && descendant->data.owner != descendant->get_parent()) {
						hidden_roots.push_back(descendant);
					}
					continue;
				}

				node_tree.push_back(descendant);

				if (descendant->is_instance() && instance_roots.has(descendant->get_owner())) {
					instance_roots.push_back(descendant);
				}
			}
		}

		for (List<const Entity *>::Element *N = node_tree.front(); N; N = N->next()) {
			const Entity *source_node = N->get();
			if (source_node->get_scene_file_path().is_empty()) {
				continue;
			}

			EntityPath relative_path = get_path_to(source_node);
			Entity *duplicated_node = node->get_entity_or_null(relative_path);
			ERR_CONTINUE(!duplicated_node);

			duplicated_node->data.editable_instance = source_node->data.editable_instance;
		}
	}

	if (get_name() != String()) {
		node->set_name(get_name());
	}

#ifdef TOOLS_ENABLED
	if ((p_flags & DUPLICATE_FROM_EDITOR) && r_duplimap) {
		r_duplimap->insert(this, node);
	}
#endif

	if (p_flags & DUPLICATE_GROUPS) {
		List<GroupInfo> gi;
		get_groups(&gi);
		for (const GroupInfo &E : gi) {
#ifdef TOOLS_ENABLED
			if ((p_flags & DUPLICATE_FROM_EDITOR) && !E.persistent) {
				continue;
			}
#endif

			node->add_to_group(E.name, E.persistent);
		}
	}

	for (int i = 0; i < get_child_count(false); i++) {
		if (instantiated && get_child(i, false)->data.owner == this) {
			continue; //part of instance
		}

		Entity *dup = get_child(i, false)->_duplicate(p_flags, r_duplimap);
		if (!dup) {
			memdelete(node);
			return nullptr;
		}

		node->add_child(dup);
		if (i < node->get_child_count(false) - 1) {
			node->move_child(dup, i);
		}
	}

	for (const Entity *&E : hidden_roots) {
		Entity *parent = node->get_entity(get_path_to(E->data.parent));
		if (!parent) {
			memdelete(node);
			return nullptr;
		}

		Entity *dup = E->_duplicate(p_flags, r_duplimap);
		if (!dup) {
			memdelete(node);
			return nullptr;
		}

		parent->add_child(dup);
		int pos = E->get_index(false);

		if (pos < parent->get_child_count(false) - 1) {
			parent->move_child(dup, pos);
		}
	}
	return node;
}

Entity *Entity::duplicate(int p_flags) const {
	ERR_THREAD_GUARD_V(nullptr);
	Entity *dupe = _duplicate(p_flags);

	ERR_FAIL_NULL_V_MSG(dupe, nullptr, "Failed to duplicate node.");

	if (p_flags & DUPLICATE_SCRIPTS) {
		_duplicate_scripts(this, dupe, p_flags);
	}

	_duplicate_properties(this, this, dupe, p_flags);

	if (p_flags & DUPLICATE_SIGNALS) {
		_duplicate_signals(this, dupe);
	}

	return dupe;
}

#ifdef TOOLS_ENABLED
Entity *Entity::duplicate_from_editor(HashMap<const Entity *, Entity *> &r_duplimap) const {
	HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> tmp;
	return duplicate_from_editor(r_duplimap, nullptr, tmp);
}

Entity *Entity::duplicate_from_editor(HashMap<const Entity *, Entity *> &r_duplimap, Entity *p_scene_root, HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> &p_resource_remap) const {
	int flags = DUPLICATE_SIGNALS | DUPLICATE_GROUPS | DUPLICATE_SCRIPTS | DUPLICATE_USE_INSTANTIATION | DUPLICATE_FROM_EDITOR;
	Entity *dupe = _duplicate(flags, &r_duplimap);

	ERR_FAIL_NULL_V_MSG(dupe, nullptr, "Failed to duplicate node.");

	if (flags & DUPLICATE_SCRIPTS) {
		_duplicate_scripts(this, dupe, flags);
	}

	_duplicate_properties(this, this, dupe, flags);

	// This is used by StageDock's paste functionality. When pasting to foreign scene, resources are duplicated.
	if (p_scene_root) {
		remap_node_resources(dupe, p_scene_root, p_resource_remap);
	}

	// Duplication of signals must happen after all the node descendants have been copied,
	// because re-targeting of connections from some descendant to another is not possible
	// if the emitter node comes later in tree order than the receiver
	_duplicate_signals(this, dupe);

	return dupe;
}

void Entity::remap_node_resources(Entity *p_node, Entity *p_scene_root, HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> &p_resource_remap) const {
	Entity *local_scene = p_node->is_instance() ? p_node : (p_node->get_owner() ? p_node->get_owner() : p_scene_root);

	List<PropertyInfo> props;
	p_node->get_property_list(&props);

	for (const PropertyInfo &E : props) {
		if (!(E.usage & PROPERTY_USAGE_STORAGE)) {
			continue;
		}

		Variant v = p_node->get(E.name);
		if (!v.is_ref_counted()) {
			continue;
		}
		Ref<Resource> res = v;
		if (res.is_null()) {
			continue;
		}

		if (res->is_local_to_scene()) {
			if (local_scene == res->get_local_scene()) {
				continue;
			}
			Ref<Resource> dup = SceneState::get_remap_resource(res, p_resource_remap, nullptr, local_scene);
			p_node->set(E.name, dup);
			continue;
		}

		if (res->is_built_in()) {
			// Use nullptr instead of a specific node (current scene root node) to represent the scene,
			// as the Make Scene Root operation may be executed.
			if (p_resource_remap[nullptr].has(res)) {
				p_node->set(E.name, p_resource_remap[nullptr][res]);
				remap_nested_resources(res, p_resource_remap[nullptr]);
			}
		}
	}

	for (int i = 0; i < p_node->get_child_count(); i++) {
		remap_node_resources(p_node->get_child(i), p_scene_root, p_resource_remap);
	}
}

void Entity::remap_nested_resources(Ref<Resource> p_resource, HashMap<Ref<Resource>, Ref<Resource>> &p_resource_remap) const {
	List<PropertyInfo> props;
	p_resource->get_property_list(&props);

	for (const PropertyInfo &E : props) {
		if (!(E.usage & PROPERTY_USAGE_STORAGE)) {
			continue;
		}

		Variant v = p_resource->get(E.name);
		if (v.is_ref_counted()) {
			Ref<Resource> res = v;
			if (res.is_valid()) {
				if (p_resource_remap.has(res)) {
					p_resource->set(E.name, p_resource_remap[res]);
					remap_nested_resources(res, p_resource_remap);
				}
			}
		}
	}
}

void Entity::_emit_editor_state_changed() {
	// This is required for the StageEditor to properly keep track of when an update is needed.
	// This signal might be expensive and not needed for anything outside of the editor.
	if (Engine::get_singleton()->is_editor_hint()) {
		emit_signal(SNAME("editor_state_changed"));
	}
}
#endif

void Entity::_duplicate_scripts(const Entity *p_original, Entity *p_copy, int p_flags) const {
	bool is_valid = false;
	Variant scr = p_original->get(CoreStringName(script), &is_valid);
	if (is_valid) {
		p_copy->set(CoreStringName(script), scr);
	}

	bool instantiated = (p_flags & DUPLICATE_USE_INSTANTIATION) && is_instance();
	for (int i = 0; i < p_original->get_child_count(false); i++) {
		Entity *original = p_original->get_child(i, false);
		// If using instantiation and the child structure changed, getting it by index will result in
		// fetching values from the wrong nodes. So use the slower (but more accurate) path method.
		Entity *copy = instantiated ? p_copy->get_entity(EntityPath({ original->get_name() }, false)) : p_copy->get_child(i, false);
		ERR_FAIL_NULL_MSG(copy, "Child node disappeared while duplicating.");
		_duplicate_scripts(original, copy, p_flags);
	}
}

// Duplicate node's properties.
// This has to be called after nodes have been duplicated since there might be properties
// of type Entity that can be updated properly only if duplicated node tree is complete.
void Entity::_duplicate_properties(const Entity *p_root, const Entity *p_original, Entity *p_copy, int p_flags) const {
	List<PropertyInfo> props;
	p_original->get_property_list(&props);
	const StringName &script_property_name = CoreStringName(script);
	for (const PropertyInfo &E : props) {
		if (!(p_flags & DUPLICATE_INTERNAL_STATE) && !(E.usage & PROPERTY_USAGE_STORAGE)) {
			continue;
		}
		const StringName name = E.name;

		if (name == script_property_name) {
			continue;
		}

		Variant value = p_original->get(name);
		// To keep classic behavior, because, in contrast, nowadays a resource would be duplicated.
		if (value.get_type() != Variant::OBJECT) {
			value = value.duplicate(true);
		}

		if (E.usage & PROPERTY_USAGE_ALWAYS_DUPLICATE) {
			Resource *res = Object::cast_to<Resource>(value);
			if (res) { // Duplicate only if it's a resource
				p_copy->set(name, res->duplicate());
			}
		} else {
			if (value.get_type() == Variant::OBJECT) {
				Entity *property_node = Object::cast_to<Entity>(value);
				Variant out_value = value;
				if (property_node && (p_root == property_node || p_root->is_ancestor_of(property_node))) {
					out_value = p_copy->get_entity_or_null(p_original->get_path_to(property_node));
				}
				p_copy->set(name, out_value);
			} else if (value.get_type() == Variant::ARRAY) {
				Array arr = value;
				if (arr.get_typed_builtin() == Variant::OBJECT) {
					for (int i = 0; i < arr.size(); i++) {
						Entity *property_node = Object::cast_to<Entity>(arr[i]);
						if (property_node && (p_root == property_node || p_root->is_ancestor_of(property_node))) {
							arr[i] = p_copy->get_entity_or_null(p_original->get_path_to(property_node));
						}
					}
				}
				p_copy->set(name, arr);
			} else {
				p_copy->set(name, value);
			}
		}
	}

	bool instantiated = (p_flags & DUPLICATE_USE_INSTANTIATION) && is_instance();
	for (int i = 0; i < p_original->get_child_count(false); i++) {
		Entity *original = p_original->get_child(i, false);
		// If using instantiation and the child structure changed, getting it by index will result in
		// fetching values from the wrong nodes. So use the slower (but more accurate) path method.
		Entity *copy = instantiated ? p_copy->get_entity(EntityPath({ original->get_name() }, false)) : p_copy->get_child(i, false);
		ERR_FAIL_NULL_MSG(copy, "Child node disappeared while duplicating.");
		_duplicate_properties(p_root, original, copy, p_flags);
	}
}

// Duplication of signals must happen after all the node descendants have been copied,
// because re-targeting of connections from some descendant to another is not possible
// if the emitter node comes later in tree order than the receiver
void Entity::_duplicate_signals(const Entity *p_original, Entity *p_copy) const {
	if ((this != p_original) && !(p_original->is_ancestor_of(this))) {
		return;
	}

	List<const Entity *> process_list;
	process_list.push_back(this);
	while (!process_list.is_empty()) {
		const Entity *n = process_list.front()->get();
		process_list.pop_front();

		List<Connection> conns;
		n->get_all_signal_connections(&conns);

		for (const Connection &E : conns) {
			if (E.flags & CONNECT_PERSIST) {
				//user connected
				EntityPath p = p_original->get_path_to(n);
				Entity *copy = p_copy->get_entity(p);

				Entity *target = Object::cast_to<Entity>(E.callable.get_object());
				if (!target) {
					continue;
				}

				EntityPath ptarget = p_original->get_path_to(target);
				if (ptarget.is_empty()) {
					continue;
				}

				Entity *copytarget = target;

				// Attempt to find a path to the duplicate target, if it seems it's not part
				// of the duplicated and not yet parented hierarchy then at least try to connect
				// to the same target as the original

				if (p_copy->has_entity(ptarget)) {
					copytarget = p_copy->get_entity(ptarget);
				}

				if (copy && copytarget && E.callable.get_method() != StringName()) {
					Callable copy_callable = Callable(copytarget, E.callable.get_method());
					if (!copy->is_connected(E.signal.get_name(), copy_callable)) {
						int unbound_arg_count = E.callable.get_unbound_arguments_count();
						if (unbound_arg_count > 0) {
							copy_callable = copy_callable.unbind(unbound_arg_count);
						}
						if (E.callable.get_bound_arguments_count() > 0) {
							copy_callable = copy_callable.bindv(E.callable.get_bound_arguments());
						}
						copy->connect(E.signal.get_name(), copy_callable, E.flags);
					}
				}
			}
		}

		for (int i = 0; i < n->get_child_count(); i++) {
			process_list.push_back(n->get_child(i));
		}
	}
}

static void find_owned_by(Entity *p_by, Entity *p_node, List<Entity *> *p_owned) {
	if (p_node->get_owner() == p_by) {
		p_owned->push_back(p_node);
	}

	for (int i = 0; i < p_node->get_child_count(); i++) {
		find_owned_by(p_by, p_node->get_child(i), p_owned);
	}
}

void Entity::replace_by(RequiredParam<Entity> p_node, bool p_keep_groups) {
	ERR_THREAD_GUARD
	EXTRACT_PARAM_OR_FAIL(node, p_node);
	ERR_FAIL_COND(node->data.parent);

	List<Entity *> owned(data.owned);
	List<Entity *> owned_by_owner;
	Entity *owner = (data.owner == this) ? node : data.owner;

	if (p_keep_groups) {
		List<GroupInfo> groups;
		get_groups(&groups);

		for (const GroupInfo &E : groups) {
			node->add_to_group(E.name, E.persistent);
		}
	}

	_replace_connections_target(node);

	if (data.owner) {
		for (int i = 0; i < get_child_count(); i++) {
			find_owned_by(data.owner, get_child(i), &owned_by_owner);
		}

		_clean_up_owner();
	}

	Entity *parent = data.parent;
	int index_in_parent = get_index(false);

	if (data.parent) {
		parent->remove_child(this);
		parent->add_child(node);
		parent->move_child(node, index_in_parent);
	}

	emit_signal(SNAME("replacing_by"), node);

	// Move non-internal children to `p_node`.
	while (get_child_count(false)) {
		Entity *child = get_child(0, false);
		remove_child(child);
		Entity *child_owner = child->get_owner() == this ? node : child->get_owner();
		child->set_owner(nullptr);
		node->add_child(child);
		child->set_owner(child_owner);
	}

	node->set_owner(owner);
	for (Entity *E : owned) {
		if (E->data.owner != node) {
			E->set_owner(node);
		}
	}

	for (Entity *E : owned_by_owner) {
		if (E->data.owner != owner) {
			E->set_owner(owner);
		}
	}

	node->set_scene_file_path(get_scene_file_path());
}

void Entity::_replace_connections_target(Entity *p_new_target) {
	List<Connection> cl;
	get_signals_connected_to_this(&cl);

	for (const Connection &c : cl) {
		if (c.flags & CONNECT_PERSIST) {
			c.signal.get_object()->disconnect(c.signal.get_name(), Callable(this, c.callable.get_method()));
			bool valid = p_new_target->has_method(c.callable.get_method()) || Ref<Script>(p_new_target->get_script()).is_null() || Ref<Script>(p_new_target->get_script())->has_method(c.callable.get_method());
			ERR_CONTINUE_MSG(!valid, vformat("Attempt to connect signal '%s.%s' to nonexistent method '%s.%s'.", c.signal.get_object()->get_class(), c.signal.get_name(), c.callable.get_object()->get_class(), c.callable.get_method()));
			c.signal.get_object()->connect(c.signal.get_name(), Callable(p_new_target, c.callable.get_method()), c.flags);
		}
	}
}

bool Entity::has_entity_and_resource(const EntityPath &p_path) const {
	ERR_THREAD_GUARD_V(false);
	if (!has_entity(p_path)) {
		return false;
	}
	Ref<Resource> res;
	Vector<StringName> leftover_path;
	Entity *node = get_entity_and_resource(p_path, res, leftover_path, false);

	return node;
}

Array Entity::_get_node_and_resource(const EntityPath &p_path) {
	Ref<Resource> res;
	Vector<StringName> leftover_path;
	Entity *node = get_entity_and_resource(p_path, res, leftover_path, false);
	Array result;

	if (node) {
		result.push_back(node);
	} else {
		result.push_back(Variant());
	}

	if (res.is_valid()) {
		result.push_back(res);
	} else {
		result.push_back(Variant());
	}

	result.push_back(EntityPath(Vector<StringName>(), leftover_path, false));

	return result;
}

Entity *Entity::get_entity_and_resource(const EntityPath &p_path, Ref<Resource> &r_res, Vector<StringName> &r_leftover_subpath, bool p_last_is_property) const {
	ERR_THREAD_GUARD_V(nullptr);
	r_res = Ref<Resource>();
	r_leftover_subpath = Vector<StringName>();
	Entity *node = get_entity_or_null(p_path);
	if (!node) {
		return nullptr;
	}

	if (p_path.get_subname_count()) {
		int j = 0;
		// If not p_last_is_property, we shouldn't consider the last one as part of the resource
		for (; j < p_path.get_subname_count() - (int)p_last_is_property; j++) {
			bool is_valid = false;
			Variant new_res_v = j == 0 ? node->get(p_path.get_subname(j), &is_valid) : r_res->get(p_path.get_subname(j), &is_valid);

			if (!is_valid) { // Found nothing on that path
				return nullptr;
			}

			Ref<Resource> new_res = new_res_v;

			if (new_res.is_null()) { // No longer a resource, assume property
				break;
			}

			r_res = new_res;
		}
		for (; j < p_path.get_subname_count(); j++) {
			// Put the rest of the subpath in the leftover path
			r_leftover_subpath.push_back(p_path.get_subname(j));
		}
	}

	return node;
}

void Entity::_set_tree(Stage *p_tree) {
	Stage *stage_changed_a = nullptr;
	Stage *stage_changed_b = nullptr;

	//ERR_FAIL_COND(p_scene && data.parent && !data.parent->data.scene); //nobug if both are null

	if (data.tree) {
		_propagate_exit_tree();

		stage_changed_a = data.tree;
	}

	data.tree = p_tree;

	if (data.tree) {
		_propagate_enter_tree();
		if (!data.parent || data.parent->data.ready_notified) { // No parent (root) or parent ready
			_propagate_ready(); //reverse_notification(NOTIFICATION_START);
		}

		stage_changed_b = data.tree;
	}

	if (stage_changed_a) {
		stage_changed_a->stage_changed();
	}
	if (stage_changed_b) {
		stage_changed_b->stage_changed();
	}
}

#ifdef DEBUG_ENABLED
static HashMap<ObjectID, List<String>> _print_orphan_nodes_map;

static void _print_orphan_nodes_routine(Object *p_obj, void *p_user_data) {
	Entity *n = Object::cast_to<Entity>(p_obj);
	if (!n) {
		return;
	}

	if (n->is_inside_stage()) {
		return;
	}

	Entity *p = n;
	while (p->get_parent()) {
		p = p->get_parent();
	}

	String path;
	if (p == n) {
		path = n->get_name();
	} else {
		path = String(p->get_name()) + "/" + String(p->get_path_to(n));
	}

	String source;
	Variant script = n->get_script();
	if (!script.is_null()) {
		Resource *obj = Object::cast_to<Resource>(script);
		source = obj->get_path();
	}

	List<String> info_strings;
	info_strings.push_back(path);
	info_strings.push_back(n->get_class());
	info_strings.push_back(source);

	_print_orphan_nodes_map[p_obj->get_instance_id()] = info_strings;
}
#endif // DEBUG_ENABLED

void Entity::print_orphan_entities() {
#ifdef DEBUG_ENABLED
	// Make sure it's empty.
	_print_orphan_nodes_map.clear();

	// Collect and print information about orphan nodes.
	ObjectRegistry::debug_objects(_print_orphan_nodes_routine, nullptr);

	for (const KeyValue<ObjectID, List<String>> &E : _print_orphan_nodes_map) {
		print_line(itos(E.key) + " - Stray Entity: " + E.value.get(0) + " (Type: " + E.value.get(1) + ") (Source:" + E.value.get(2) + ")");
	}

	// Flush it after use.
	_print_orphan_nodes_map.clear();
#endif
}
TypedArray<int> Entity::get_orphan_entity_ids() {
	TypedArray<int> ret;
#ifdef DEBUG_ENABLED
	// Make sure it's empty.
	_print_orphan_nodes_map.clear();

	// Collect and return information about orphan nodes.
	ObjectRegistry::debug_objects(_print_orphan_nodes_routine, nullptr);

	for (const KeyValue<ObjectID, List<String>> &E : _print_orphan_nodes_map) {
		ret.push_back(E.key);
	}

	// Flush it after use.
	_print_orphan_nodes_map.clear();
#endif
	return ret;
}

void Entity::queue_destroy() {
	// There are users which instantiate multiple scene trees for their games.
	// Use the node's own tree to handle its deletion when relevant.
	if (data.tree) {
		data.tree->queue_delete(this);
	} else {
		Stage *tree = Stage::get_singleton();
		ERR_FAIL_NULL_MSG(tree, "Can't queue a node for destruction when no Stage is available.");
		tree->queue_delete(this);
	}
}

#ifdef TOOLS_ENABLED
static void _add_nodes_to_options(const Entity *p_base, const Entity *p_node, List<String> *r_options) {
	if (p_node != p_base && !p_node->get_owner()) {
		return;
	}
	if (p_node->is_unique_name_in_owner() && p_node->get_owner() == p_base) {
		String n = "%" + p_node->get_name();
		r_options->push_back(n.quote());
	}
	String n = String(p_base->get_path_to(p_node));
	r_options->push_back(n.quote());
	for (int i = 0; i < p_node->get_child_count(); i++) {
		_add_nodes_to_options(p_base, p_node->get_child(i), r_options);
	}
}

void Entity::get_argument_options(const StringName &p_function, int p_idx, List<String> *r_options) const {
	const String pf = p_function;
	if (p_idx == 0 && (pf == "has_entity" || pf == "get_entity" || pf == "get_entity_or_null")) {
		_add_nodes_to_options(this, this, r_options);
	} else if (p_idx == 0 && (pf == "add_to_group" || pf == "remove_from_group" || pf == "is_in_group")) {
		HashMap<StringName, String> global_groups(ProjectSettings::get_singleton()->get_global_groups_list());
		for (const KeyValue<StringName, String> &E : global_groups) {
			r_options->push_back(E.key.string().quote());
		}
	}
	Object::get_argument_options(p_function, p_idx, r_options);
}
#endif

void Entity::clear_internal_tree_resource_paths() {
	clear_internal_resource_paths();
	for (KeyValue<StringName, Entity *> &K : data.children) {
		K.value->clear_internal_tree_resource_paths();
	}
}

PackedStringArray Entity::get_accessibility_configuration_warnings() const {
	ERR_THREAD_GUARD_V(PackedStringArray());
	PackedStringArray ret;

	Vector<String> warnings;
	if (ORBLIT_VIRTUAL_CALL(_get_accessibility_configuration_warnings, warnings)) {
		ret.append_array(warnings);
	}

	return ret;
}

PackedStringArray Entity::get_configuration_warnings() const {
	ERR_THREAD_GUARD_V(PackedStringArray());
	PackedStringArray ret;

	Vector<String> warnings;
	if (ORBLIT_VIRTUAL_CALL(_get_configuration_warnings, warnings)) {
		ret.append_array(warnings);
	}

	return ret;
}

void Entity::update_configuration_warnings() {
	ERR_THREAD_GUARD
#ifdef TOOLS_ENABLED
	if (!data.tree) {
		return;
	}
	if (data.tree->get_edited_scene_root() && (data.tree->get_edited_scene_root() == this || data.tree->get_edited_scene_root()->is_ancestor_of(this))) {
		data.tree->emit_signal(SceneStringName(entity_configuration_warning_changed), this);
	}
#endif
}

void Entity::set_display_folded(bool p_folded) {
	ERR_THREAD_GUARD
	data.display_folded = p_folded;
}

bool Entity::is_displayed_folded() const {
	return data.display_folded;
}

bool Entity::is_ready() const {
	return !data.ready_first;
}

void Entity::request_start() {
	ERR_THREAD_GUARD
	data.ready_first = true;
}

void Entity::_call_input(const Ref<InputEvent> &p_event) {
	if (p_event->get_device() != InputEvent::DEVICE_ID_INTERNAL) {
		ORBLIT_VIRTUAL_CALL(_input, p_event);
	}
	if (!is_inside_stage() || !get_viewport() || get_viewport()->is_input_handled()) {
		return;
	}
	input(p_event);
}

void Entity::_call_shortcut_input(const Ref<InputEvent> &p_event) {
	if (p_event->get_device() != InputEvent::DEVICE_ID_INTERNAL) {
		ORBLIT_VIRTUAL_CALL(_shortcut_input, p_event);
	}
	if (!is_inside_stage() || !get_viewport() || get_viewport()->is_input_handled()) {
		return;
	}
	shortcut_input(p_event);
}

void Entity::_call_unhandled_input(const Ref<InputEvent> &p_event) {
	if (p_event->get_device() != InputEvent::DEVICE_ID_INTERNAL) {
		ORBLIT_VIRTUAL_CALL(_unhandled_input, p_event);
	}
	if (!is_inside_stage() || !get_viewport() || get_viewport()->is_input_handled()) {
		return;
	}
	unhandled_input(p_event);
}

void Entity::_call_unhandled_key_input(const Ref<InputEvent> &p_event) {
	if (p_event->get_device() != InputEvent::DEVICE_ID_INTERNAL) {
		ORBLIT_VIRTUAL_CALL(_unhandled_key_input, p_event);
	}
	if (!is_inside_stage() || !get_viewport() || get_viewport()->is_input_handled()) {
		return;
	}
	unhandled_key_input(p_event);
}

void Entity::_validate_property(PropertyInfo &p_property) const {
	if ((p_property.name == "run_thread_group_order" || p_property.name == "run_thread_messages") && data.run_thread_group == RUN_THREAD_GROUP_INHERIT) {
		p_property.usage = PROPERTY_USAGE_NONE;
	}
}

String Entity::_to_string() {
	ERR_THREAD_GUARD_V(String());
	return (get_name() ? String(get_name()) + ":" : "") + Object::_to_string();
}

void Entity::input(const Ref<InputEvent> &p_event) {
}

void Entity::shortcut_input(const Ref<InputEvent> &p_key_event) {
}

void Entity::unhandled_input(const Ref<InputEvent> &p_event) {
}

void Entity::unhandled_key_input(const Ref<InputEvent> &p_key_event) {
}

Variant Entity::_call_deferred_thread_group_bind(const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	if (p_argcount < 1) {
		r_error.error = Callable::CallError::CALL_ERROR_TOO_FEW_ARGUMENTS;
		r_error.expected = 1;
		return Variant();
	}

	if (!p_args[0]->is_string()) {
		r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
		r_error.argument = 0;
		r_error.expected = Variant::STRING_NAME;
		return Variant();
	}

	r_error.error = Callable::CallError::CALL_OK;

	StringName method = *p_args[0];

	call_deferred_thread_groupp(method, &p_args[1], p_argcount - 1, true);

	return Variant();
}

Variant Entity::_call_thread_safe_bind(const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	if (p_argcount < 1) {
		r_error.error = Callable::CallError::CALL_ERROR_TOO_FEW_ARGUMENTS;
		r_error.expected = 1;
		return Variant();
	}

	if (!p_args[0]->is_string()) {
		r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
		r_error.argument = 0;
		r_error.expected = Variant::STRING_NAME;
		return Variant();
	}

	r_error.error = Callable::CallError::CALL_OK;

	StringName method = *p_args[0];

	call_thread_safep(method, &p_args[1], p_argcount - 1, true);

	return Variant();
}

void Entity::call_deferred_thread_groupp(const StringName &p_method, const Variant **p_args, int p_argcount, bool p_show_error) {
	ERR_FAIL_COND(!is_inside_stage());
	Stage::ProcessGroup *pg = (Stage::ProcessGroup *)data.process_group;
	pg->call_queue.push_callp(this, p_method, p_args, p_argcount, p_show_error);
}

void Entity::set_deferred_thread_group(const StringName &p_property, const Variant &p_value) {
	ERR_FAIL_COND(!is_inside_stage());
	Stage::ProcessGroup *pg = (Stage::ProcessGroup *)data.process_group;
	pg->call_queue.push_set(this, p_property, p_value);
}

void Entity::notify_deferred_thread_group(int p_notification) {
	ERR_FAIL_COND(!is_inside_stage());
	Stage::ProcessGroup *pg = (Stage::ProcessGroup *)data.process_group;
	pg->call_queue.push_notification(this, p_notification);
}

void Entity::call_thread_safep(const StringName &p_method, const Variant **p_args, int p_argcount, bool p_show_error) {
	if (is_accessible_from_caller_thread()) {
		Callable::CallError ce;
		callp(p_method, p_args, p_argcount, ce);
		if (p_show_error && ce.error != Callable::CallError::CALL_OK) {
			ERR_FAIL_MSG("Error calling method from 'call_threadp': " + Variant::get_call_error_text(this, p_method, p_args, p_argcount, ce) + ".");
		}
	} else {
		call_deferred_thread_groupp(p_method, p_args, p_argcount, p_show_error);
	}
}

void Entity::set_thread_safe(const StringName &p_property, const Variant &p_value) {
	if (is_accessible_from_caller_thread()) {
		set(p_property, p_value);
	} else {
		set_deferred_thread_group(p_property, p_value);
	}
}

void Entity::notify_thread_safe(int p_notification) {
	if (is_accessible_from_caller_thread()) {
		notification(p_notification);
	} else {
		notify_deferred_thread_group(p_notification);
	}
}

RID Entity::get_focused_accessibility_element() const {
	RID id;
	if (ORBLIT_VIRTUAL_CALL(_get_focused_accessibility_element, id)) {
		return id;
	} else {
		return get_accessibility_element();
	}
}

Transform2D Entity::get_accessibility_transform() const {
	if (is_inside_stage() && data.parent) {
		return data.parent->get_accessibility_transform();
	} else {
		return Transform2D();
	}
}

void Entity::queue_accessibility_update() {
	if (is_inside_stage() && data.tree->is_accessibility_supported() && !is_part_of_edited_scene()) {
		data.tree->_accessibility_notify_change(this);
	}
}

RID Entity::get_accessibility_element() const {
	if (is_part_of_edited_scene()) {
		return RID();
	}
	if (unlikely(data.accessibility_element.is_null())) {
		Window *w = get_non_popup_window();
		if (w && w->get_window_id() != DisplaySystemEnums::INVALID_WINDOW_ID && get_window()->is_visible()) {
			data.accessibility_element = AccessibilitySystem::get_singleton()->create_element(w->get_window_id(), AccessibilitySystemEnums::ROLE_CONTAINER);
		}
	}
	return data.accessibility_element;
}

void Entity::register_members() {
	GLOBAL_DEF(PropertyInfo(Variant::INT, "editor/naming/node_name_num_separator", PROPERTY_HINT_ENUM, "None,Space,Underscore,Dash"), 0);
	GLOBAL_DEF(PropertyInfo(Variant::INT, "editor/naming/node_name_casing", PROPERTY_HINT_ENUM, "PascalCase,camelCase,snake_case,kebab-case"), NAME_CASING_PASCAL_CASE);

	ClassRegistry::bind_static_method("Entity", D_METHOD("print_orphan_entities"), &Entity::print_orphan_entities);
	ClassRegistry::bind_static_method("Entity", D_METHOD("get_orphan_entity_ids"), &Entity::get_orphan_entity_ids);
	ClassRegistry::bind_method(D_METHOD("add_sibling", "sibling", "force_readable_name"), &Entity::add_sibling, DEFVAL(false));

	ClassRegistry::bind_method(D_METHOD("set_name", "name"), &Entity::set_name);
	ClassRegistry::bind_method(D_METHOD("get_name"), &Entity::get_name);
	ClassRegistry::bind_method(D_METHOD("add_child", "node", "force_readable_name", "internal"), &Entity::add_child, DEFVAL(false), DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("remove_child", "node"), &Entity::remove_child);
	ClassRegistry::bind_method(D_METHOD("reparent", "new_parent", "keep_global_transform"), &Entity::reparent, DEFVAL(true));
	ClassRegistry::bind_method(D_METHOD("get_child_count", "include_internal"), &Entity::get_child_count, DEFVAL(false)); // Note that the default value bound for include_internal is false, while the method is declared with true. This is because internal nodes are irrelevant for scripts.
	ClassRegistry::bind_method(D_METHOD("get_children", "include_internal"), &Entity::get_children, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("get_child", "idx", "include_internal"), &Entity::get_child, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("has_entity", "path"), &Entity::has_entity);
	ClassRegistry::bind_method(D_METHOD("get_entity", "path"), &Entity::get_entity);
	ClassRegistry::bind_method(D_METHOD("get_entity_or_null", "path"), &Entity::get_entity_or_null);
	ClassRegistry::bind_method(D_METHOD("get_parent"), &Entity::get_parent);
	ClassRegistry::bind_method(D_METHOD("find_child", "pattern", "recursive", "owned"), &Entity::find_child, DEFVAL(true), DEFVAL(true));
	ClassRegistry::bind_method(D_METHOD("find_children", "pattern", "type", "recursive", "owned"), &Entity::find_children, DEFVAL(""), DEFVAL(true), DEFVAL(true));
	ClassRegistry::bind_method(D_METHOD("find_parent", "pattern"), &Entity::find_parent);
	ClassRegistry::bind_method(D_METHOD("has_entity_and_resource", "path"), &Entity::has_entity_and_resource);
	ClassRegistry::bind_method(D_METHOD("get_entity_and_resource", "path"), &Entity::_get_node_and_resource);

	ClassRegistry::bind_method(D_METHOD("is_inside_stage"), &Entity::is_inside_stage);
	ClassRegistry::bind_method(D_METHOD("is_part_of_edited_scene"), &Entity::is_part_of_edited_scene);
	ClassRegistry::bind_method(D_METHOD("is_ancestor_of", "node"), &Entity::is_ancestor_of);
	ClassRegistry::bind_method(D_METHOD("is_greater_than", "node"), &Entity::is_greater_than);
	ClassRegistry::bind_method(D_METHOD("get_path"), &Entity::get_path);
	ClassRegistry::bind_method(D_METHOD("get_path_to", "node", "use_unique_path"), &Entity::get_path_to, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("add_to_group", "group", "persistent"), &Entity::add_to_group, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("remove_from_group", "group"), &Entity::remove_from_group);
	ClassRegistry::bind_method(D_METHOD("is_in_group", "group"), &Entity::is_in_group);
	ClassRegistry::bind_method(D_METHOD("move_child", "child_node", "to_index"), &Entity::move_child);
	ClassRegistry::bind_method(D_METHOD("get_groups"), &Entity::_get_groups);
	ClassRegistry::bind_method(D_METHOD("set_owner", "owner"), &Entity::set_owner);
	ClassRegistry::bind_method(D_METHOD("get_owner"), &Entity::get_owner);
	ClassRegistry::bind_method(D_METHOD("get_index", "include_internal"), &Entity::get_index, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("print_entity_tree"), &Entity::print_entity_tree);
	ClassRegistry::bind_method(D_METHOD("print_entity_tree_pretty"), &Entity::print_entity_tree_pretty);
	ClassRegistry::bind_method(D_METHOD("get_entity_tree_string"), &Entity::get_entity_tree_string);
	ClassRegistry::bind_method(D_METHOD("get_entity_tree_string_pretty"), &Entity::get_entity_tree_string_pretty);
	ClassRegistry::bind_method(D_METHOD("set_scene_file_path", "scene_file_path"), &Entity::set_scene_file_path);
	ClassRegistry::bind_method(D_METHOD("get_scene_file_path"), &Entity::get_scene_file_path);
	ClassRegistry::bind_method(D_METHOD("propagate_notification", "what"), &Entity::propagate_notification);
	ClassRegistry::bind_method(D_METHOD("propagate_call", "method", "args", "parent_first"), &Entity::propagate_call, DEFVAL(Array()), DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("set_step_enabled", "enable"), &Entity::set_step_enabled);
	ClassRegistry::bind_method(D_METHOD("get_step_delta"), &Entity::get_step_delta);
	ClassRegistry::bind_method(D_METHOD("is_step_enabled"), &Entity::is_step_enabled);
	ClassRegistry::bind_method(D_METHOD("get_frame_delta"), &Entity::get_frame_delta);
	ClassRegistry::bind_method(D_METHOD("set_frame_enabled", "enable"), &Entity::set_frame_enabled);
	ClassRegistry::bind_method(D_METHOD("set_run_priority", "priority"), &Entity::set_run_priority);
	ClassRegistry::bind_method(D_METHOD("get_run_priority"), &Entity::get_run_priority);
	ClassRegistry::bind_method(D_METHOD("set_run_physics_priority", "priority"), &Entity::set_run_physics_priority);
	ClassRegistry::bind_method(D_METHOD("get_run_physics_priority"), &Entity::get_run_physics_priority);
	ClassRegistry::bind_method(D_METHOD("is_frame_enabled"), &Entity::is_frame_enabled);
	ClassRegistry::bind_method(D_METHOD("set_input_enabled", "enable"), &Entity::set_input_enabled);
	ClassRegistry::bind_method(D_METHOD("is_input_enabled"), &Entity::is_input_enabled);
	ClassRegistry::bind_method(D_METHOD("set_shortcut_input_enabled", "enable"), &Entity::set_shortcut_input_enabled);
	ClassRegistry::bind_method(D_METHOD("is_shortcut_input_enabled"), &Entity::is_shortcut_input_enabled);
	ClassRegistry::bind_method(D_METHOD("set_unhandled_input_enabled", "enable"), &Entity::set_unhandled_input_enabled);
	ClassRegistry::bind_method(D_METHOD("is_unhandled_input_enabled"), &Entity::is_unhandled_input_enabled);
	ClassRegistry::bind_method(D_METHOD("set_unhandled_key_input_enabled", "enable"), &Entity::set_unhandled_key_input_enabled);
	ClassRegistry::bind_method(D_METHOD("is_unhandled_key_input_enabled"), &Entity::is_unhandled_key_input_enabled);
	ClassRegistry::bind_method(D_METHOD("set_run_mode", "mode"), &Entity::set_run_mode);
	ClassRegistry::bind_method(D_METHOD("get_run_mode"), &Entity::get_run_mode);
	ClassRegistry::bind_method(D_METHOD("can_run"), &Entity::can_run);

	ClassRegistry::bind_method(D_METHOD("set_run_thread_group", "mode"), &Entity::set_run_thread_group);
	ClassRegistry::bind_method(D_METHOD("get_run_thread_group"), &Entity::get_run_thread_group);

	ClassRegistry::bind_method(D_METHOD("set_run_thread_messages", "flags"), &Entity::set_run_thread_messages);
	ClassRegistry::bind_method(D_METHOD("get_run_thread_messages"), &Entity::get_run_thread_messages);

	ClassRegistry::bind_method(D_METHOD("set_run_thread_group_order", "order"), &Entity::set_run_thread_group_order);
	ClassRegistry::bind_method(D_METHOD("get_run_thread_group_order"), &Entity::get_run_thread_group_order);

	ClassRegistry::bind_method(D_METHOD("queue_accessibility_update"), &Entity::queue_accessibility_update);
	ClassRegistry::bind_method(D_METHOD("get_accessibility_element"), &Entity::get_accessibility_element);

	ClassRegistry::bind_method(D_METHOD("set_display_folded", "fold"), &Entity::set_display_folded);
	ClassRegistry::bind_method(D_METHOD("is_displayed_folded"), &Entity::is_displayed_folded);

	ClassRegistry::bind_method(D_METHOD("set_internal_frame_enabled", "enable"), &Entity::set_internal_frame_enabled);
	ClassRegistry::bind_method(D_METHOD("is_internal_frame_enabled"), &Entity::is_internal_frame_enabled);

	ClassRegistry::bind_method(D_METHOD("set_internal_step_enabled", "enable"), &Entity::set_internal_step_enabled);
	ClassRegistry::bind_method(D_METHOD("is_internal_step_enabled"), &Entity::is_internal_step_enabled);

	ClassRegistry::bind_method(D_METHOD("set_physics_interpolation_mode", "mode"), &Entity::set_physics_interpolation_mode);
	ClassRegistry::bind_method(D_METHOD("get_physics_interpolation_mode"), &Entity::get_physics_interpolation_mode);
	ClassRegistry::bind_method(D_METHOD("is_physics_interpolated"), &Entity::is_physics_interpolated);
	ClassRegistry::bind_method(D_METHOD("is_physics_interpolated_and_enabled"), &Entity::is_physics_interpolated_and_enabled);
	ClassRegistry::bind_method(D_METHOD("reset_physics_interpolation"), &Entity::reset_physics_interpolation);

	ClassRegistry::bind_method(D_METHOD("set_auto_translate_mode", "mode"), &Entity::set_auto_translate_mode);
	ClassRegistry::bind_method(D_METHOD("get_auto_translate_mode"), &Entity::get_auto_translate_mode);
	ClassRegistry::bind_method(D_METHOD("can_auto_translate"), &Entity::can_auto_translate);
	ClassRegistry::bind_method(D_METHOD("set_translation_domain_inherited"), &Entity::set_translation_domain_inherited);

	ClassRegistry::bind_method(D_METHOD("get_window"), &Entity::get_window);
	ClassRegistry::bind_method(D_METHOD("get_last_exclusive_window"), &Entity::get_last_exclusive_window);
	ClassRegistry::bind_method(D_METHOD("get_stage"), &Entity::get_stage);
	ClassRegistry::bind_method(D_METHOD("create_tween"), &Entity::create_tween);

	ClassRegistry::bind_method(D_METHOD("duplicate", "flags"), &Entity::duplicate, DEFVAL(DUPLICATE_DEFAULT));
	ClassRegistry::bind_method(D_METHOD("replace_by", "node", "keep_groups"), &Entity::replace_by, DEFVAL(false));

	ClassRegistry::bind_method(D_METHOD("set_scene_instance_load_placeholder", "load_placeholder"), &Entity::set_scene_instance_load_placeholder);
	ClassRegistry::bind_method(D_METHOD("get_scene_instance_load_placeholder"), &Entity::get_scene_instance_load_placeholder);
	ClassRegistry::bind_method(D_METHOD("set_editable_instance", "node", "is_editable"), &Entity::set_editable_instance);
	ClassRegistry::bind_method(D_METHOD("is_editable_instance", "node"), &Entity::is_editable_instance);

	ClassRegistry::bind_method(D_METHOD("get_viewport"), &Entity::get_viewport);

	ClassRegistry::bind_method(D_METHOD("queue_destroy"), &Entity::queue_destroy);

	ClassRegistry::bind_method(D_METHOD("request_start"), &Entity::request_start);
	ClassRegistry::bind_method(D_METHOD("is_entity_started"), &Entity::is_ready);

	ClassRegistry::bind_method(D_METHOD("set_multiplayer_authority", "id", "recursive"), &Entity::set_multiplayer_authority, DEFVAL(true));
	ClassRegistry::bind_method(D_METHOD("get_multiplayer_authority"), &Entity::get_multiplayer_authority);

	ClassRegistry::bind_method(D_METHOD("is_multiplayer_authority"), &Entity::is_multiplayer_authority);

	ClassRegistry::bind_method(D_METHOD("get_multiplayer"), &Entity::get_multiplayer);
	ClassRegistry::bind_method(D_METHOD("rpc_config", "method", "config"), &Entity::rpc_config);
	ClassRegistry::bind_method(D_METHOD("get_entity_rpc_config"), &Entity::_get_node_rpc_config_bind);

	ClassRegistry::bind_method(D_METHOD("set_editor_description", "editor_description"), &Entity::set_editor_description);
	ClassRegistry::bind_method(D_METHOD("get_editor_description"), &Entity::get_editor_description);

	ClassRegistry::bind_method(D_METHOD("set_unique_name_in_owner", "enable"), &Entity::set_unique_name_in_owner);
	ClassRegistry::bind_method(D_METHOD("is_unique_name_in_owner"), &Entity::is_unique_name_in_owner);

	ClassRegistry::bind_method(D_METHOD("atr", "message", "context"), &Entity::atr, DEFVAL(""));
	ClassRegistry::bind_method(D_METHOD("atr_n", "message", "plural_message", "n", "context"), &Entity::atr_n, DEFVAL(""));

#ifdef TOOLS_ENABLED
	ClassRegistry::bind_method(D_METHOD("_set_property_pinned", "property", "pinned"), &Entity::set_property_pinned);
#endif

	{
		MethodInfo mi;

		mi.arguments.push_back(PropertyInfo(Variant::STRING_NAME, "method"));

		mi.name = "rpc";
		ClassRegistry::bind_vararg_method(METHOD_FLAGS_DEFAULT, "rpc", &Entity::_rpc_bind, mi);
	}

	{
		MethodInfo mi;

		mi.arguments.push_back(PropertyInfo(Variant::INT, "peer_id"));
		mi.arguments.push_back(PropertyInfo(Variant::STRING_NAME, "method"));

		mi.name = "rpc_id";
		ClassRegistry::bind_vararg_method(METHOD_FLAGS_DEFAULT, "rpc_id", &Entity::_rpc_id_bind, mi);
	}

	ClassRegistry::bind_method(D_METHOD("update_configuration_warnings"), &Entity::update_configuration_warnings);

	{
		MethodInfo mi;
		mi.name = "call_deferred_thread_group";
		mi.arguments.push_back(PropertyInfo(Variant::STRING_NAME, "method"));

		ClassRegistry::bind_vararg_method(METHOD_FLAGS_DEFAULT, "call_deferred_thread_group", &Entity::_call_deferred_thread_group_bind, mi, varray(), false);
	}
	ClassRegistry::bind_method(D_METHOD("set_deferred_thread_group", "property", "value"), &Entity::set_deferred_thread_group);
	ClassRegistry::bind_method(D_METHOD("notify_deferred_thread_group", "what"), &Entity::notify_deferred_thread_group);

	{
		MethodInfo mi;
		mi.name = "call_thread_safe";
		mi.arguments.push_back(PropertyInfo(Variant::STRING_NAME, "method"));

		ClassRegistry::bind_vararg_method(METHOD_FLAGS_DEFAULT, "call_thread_safe", &Entity::_call_thread_safe_bind, mi, varray(), false);
	}
	ClassRegistry::bind_method(D_METHOD("set_thread_safe", "property", "value"), &Entity::set_thread_safe);
	ClassRegistry::bind_method(D_METHOD("notify_thread_safe", "what"), &Entity::notify_thread_safe);

	BIND_CONSTANT(NOTIFICATION_ENTER_STAGE);
	BIND_CONSTANT(NOTIFICATION_EXIT_STAGE);
	BIND_CONSTANT(NOTIFICATION_START);
	BIND_CONSTANT(NOTIFICATION_PAUSED);
	BIND_CONSTANT(NOTIFICATION_UNPAUSED);
	BIND_CONSTANT(NOTIFICATION_STEP);
	BIND_CONSTANT(NOTIFICATION_FRAME);
	BIND_CONSTANT(NOTIFICATION_PARENTED);
	BIND_CONSTANT(NOTIFICATION_UNPARENTED);
	BIND_CONSTANT(NOTIFICATION_SCENE_INSTANTIATED);
	BIND_CONSTANT(NOTIFICATION_DRAG_BEGIN);
	BIND_CONSTANT(NOTIFICATION_DRAG_END);
	BIND_CONSTANT(NOTIFICATION_PATH_RENAMED);
	BIND_CONSTANT(NOTIFICATION_CHILD_ORDER_CHANGED);
	BIND_CONSTANT(NOTIFICATION_INTERNAL_FRAME);
	BIND_CONSTANT(NOTIFICATION_INTERNAL_STEP);
	BIND_CONSTANT(NOTIFICATION_POST_ENTER_STAGE);
	BIND_CONSTANT(NOTIFICATION_DISABLED);
	BIND_CONSTANT(NOTIFICATION_ENABLED);
	BIND_CONSTANT(NOTIFICATION_RESET_PHYSICS_INTERPOLATION);

	BIND_CONSTANT(NOTIFICATION_EDITOR_PRE_SAVE);
	BIND_CONSTANT(NOTIFICATION_EDITOR_POST_SAVE);

	BIND_CONSTANT(NOTIFICATION_WM_MOUSE_ENTER);
	BIND_CONSTANT(NOTIFICATION_WM_MOUSE_EXIT);
	BIND_CONSTANT(NOTIFICATION_WM_WINDOW_FOCUS_IN);
	BIND_CONSTANT(NOTIFICATION_WM_WINDOW_FOCUS_OUT);
	BIND_CONSTANT(NOTIFICATION_WM_CLOSE_REQUEST);
	BIND_CONSTANT(NOTIFICATION_WM_GO_BACK_REQUEST);
	BIND_CONSTANT(NOTIFICATION_WM_SIZE_CHANGED);
	BIND_CONSTANT(NOTIFICATION_WM_DPI_CHANGE);
	BIND_CONSTANT(NOTIFICATION_VP_MOUSE_ENTER);
	BIND_CONSTANT(NOTIFICATION_VP_MOUSE_EXIT);
	BIND_CONSTANT(NOTIFICATION_WM_POSITION_CHANGED);
	BIND_CONSTANT(NOTIFICATION_WM_OUTPUT_MAX_LINEAR_VALUE_CHANGED);
	BIND_CONSTANT(NOTIFICATION_OS_MEMORY_WARNING);
	BIND_CONSTANT(NOTIFICATION_TRANSLATION_CHANGED);
	BIND_CONSTANT(NOTIFICATION_WM_ABOUT);
	BIND_CONSTANT(NOTIFICATION_CRASH);
	BIND_CONSTANT(NOTIFICATION_OS_IME_UPDATE);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_RESUMED);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_PAUSED);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_FOCUS_IN);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_FOCUS_OUT);
	BIND_CONSTANT(NOTIFICATION_TEXT_SYSTEM_CHANGED);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_PIP_MODE_ENTERED);
	BIND_CONSTANT(NOTIFICATION_APPLICATION_PIP_MODE_EXITED);

	BIND_CONSTANT(NOTIFICATION_ACCESSIBILITY_UPDATE);
	BIND_CONSTANT(NOTIFICATION_ACCESSIBILITY_INVALIDATE);

	BIND_ENUM_CONSTANT(RUN_MODE_INHERIT);
	BIND_ENUM_CONSTANT(RUN_MODE_PAUSABLE);
	BIND_ENUM_CONSTANT(RUN_MODE_WHEN_PAUSED);
	BIND_ENUM_CONSTANT(RUN_MODE_ALWAYS);
	BIND_ENUM_CONSTANT(RUN_MODE_DISABLED);

	BIND_ENUM_CONSTANT(RUN_THREAD_GROUP_INHERIT);
	BIND_ENUM_CONSTANT(RUN_THREAD_GROUP_MAIN_THREAD);
	BIND_ENUM_CONSTANT(RUN_THREAD_GROUP_SUB_THREAD);

	BIND_BITFIELD_FLAG(FLAG_RUN_THREAD_MESSAGES);
	BIND_BITFIELD_FLAG(FLAG_RUN_THREAD_MESSAGES_PHYSICS);
	BIND_BITFIELD_FLAG(FLAG_RUN_THREAD_MESSAGES_ALL);

	BIND_ENUM_CONSTANT(PHYSICS_INTERPOLATION_MODE_INHERIT);
	BIND_ENUM_CONSTANT(PHYSICS_INTERPOLATION_MODE_ON);
	BIND_ENUM_CONSTANT(PHYSICS_INTERPOLATION_MODE_OFF);

	BIND_ENUM_CONSTANT(DUPLICATE_SIGNALS);
	BIND_ENUM_CONSTANT(DUPLICATE_GROUPS);
	BIND_ENUM_CONSTANT(DUPLICATE_SCRIPTS);
	BIND_ENUM_CONSTANT(DUPLICATE_USE_INSTANTIATION);
	BIND_ENUM_CONSTANT(DUPLICATE_INTERNAL_STATE);
	BIND_ENUM_CONSTANT(DUPLICATE_DEFAULT);

	BIND_ENUM_CONSTANT(INTERNAL_MODE_DISABLED);
	BIND_ENUM_CONSTANT(INTERNAL_MODE_FRONT);
	BIND_ENUM_CONSTANT(INTERNAL_MODE_BACK);

	BIND_ENUM_CONSTANT(AUTO_TRANSLATE_MODE_INHERIT);
	BIND_ENUM_CONSTANT(AUTO_TRANSLATE_MODE_ALWAYS);
	BIND_ENUM_CONSTANT(AUTO_TRANSLATE_MODE_DISABLED);

	ADD_SIGNAL(MethodInfo("started"));
	ADD_SIGNAL(MethodInfo("renamed"));
	ADD_SIGNAL(MethodInfo("stage_entered"));
	ADD_SIGNAL(MethodInfo("stage_exiting"));
	ADD_SIGNAL(MethodInfo("stage_exited"));
	ADD_SIGNAL(MethodInfo("child_entered_stage", PropertyInfo(Variant::OBJECT, "node", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_DEFAULT, "Entity")));
	ADD_SIGNAL(MethodInfo("child_exiting_stage", PropertyInfo(Variant::OBJECT, "node", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_DEFAULT, "Entity")));

	ADD_SIGNAL(MethodInfo("child_order_changed"));
	ADD_SIGNAL(MethodInfo("replacing_by", PropertyInfo(Variant::OBJECT, "node", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_DEFAULT, "Entity")));
	ADD_SIGNAL(MethodInfo("editor_description_changed", PropertyInfo(Variant::OBJECT, "node", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_DEFAULT, "Entity")));
	ADD_SIGNAL(MethodInfo("editor_state_changed"));

	ADD_PROPERTY(PropertyInfo(Variant::STRING_NAME, "name", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_name", "get_name");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "unique_name_in_owner", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_unique_name_in_owner", "is_unique_name_in_owner");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "scene_file_path", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_scene_file_path", "get_scene_file_path");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "owner", PROPERTY_HINT_RESOURCE_TYPE, Entity::get_static_class_name(), PROPERTY_USAGE_NONE), "set_owner", "get_owner");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "multiplayer", PROPERTY_HINT_RESOURCE_TYPE, MultiplayerAPI::get_static_class_name(), PROPERTY_USAGE_NONE), "", "get_multiplayer");

	ADD_GROUP("Run", "run_");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "run_mode", PROPERTY_HINT_ENUM, "Inherit,Pausable,When Paused,Always,Disabled"), "set_run_mode", "get_run_mode");
	// The priority is a 32-bit integer, so the inspector must not accept more.
	ADD_PROPERTY(PropertyInfo(Variant::INT, "run_priority", PROPERTY_HINT_RANGE, "-2147483648,2147483647,1"), "set_run_priority", "get_run_priority");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "run_physics_priority", PROPERTY_HINT_RANGE, "-2147483648,2147483647,1"), "set_run_physics_priority", "get_run_physics_priority");

	ADD_SUBGROUP("Thread Group", "run_thread");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "run_thread_group", PROPERTY_HINT_ENUM, "Inherit,Main Thread,Sub Thread"), "set_run_thread_group", "get_run_thread_group");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "run_thread_group_order"), "set_run_thread_group_order", "get_run_thread_group_order");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "run_thread_messages", PROPERTY_HINT_FLAGS, "Process,Physics Process"), "set_run_thread_messages", "get_run_thread_messages");

	ADD_GROUP("Physics Interpolation", "physics_interpolation_");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "physics_interpolation_mode", PROPERTY_HINT_ENUM, "Inherit,On,Off"), "set_physics_interpolation_mode", "get_physics_interpolation_mode");

	ADD_GROUP("Auto Translate", "auto_translate_");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "auto_translate_mode", PROPERTY_HINT_ENUM, "Inherit,Always,Disabled"), "set_auto_translate_mode", "get_auto_translate_mode");

	ADD_GROUP("Editor Description", "editor_");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "editor_description", PROPERTY_HINT_MULTILINE_TEXT), "set_editor_description", "get_editor_description");

	ORBLIT_VIRTUAL_BIND(_frame, "delta");
	ORBLIT_VIRTUAL_BIND(_step, "delta");
	ORBLIT_VIRTUAL_BIND(_enter_stage);
	ORBLIT_VIRTUAL_BIND(_exit_stage);
	ORBLIT_VIRTUAL_BIND(_start);
	ORBLIT_VIRTUAL_BIND(_get_configuration_warnings);
	ORBLIT_VIRTUAL_BIND(_get_accessibility_configuration_warnings);
	ORBLIT_VIRTUAL_BIND(_input, "event");
	ORBLIT_VIRTUAL_BIND(_shortcut_input, "event");
	ORBLIT_VIRTUAL_BIND(_unhandled_input, "event");
	ORBLIT_VIRTUAL_BIND(_unhandled_key_input, "event");
	ORBLIT_VIRTUAL_BIND(_get_focused_accessibility_element);
}

String Entity::_get_name_num_separator() {
	switch (GLOBAL_GET("editor/naming/node_name_num_separator").operator int()) {
		case 0:
			return "";
		case 1:
			return " ";
		case 2:
			return "_";
		case 3:
			return "-";
	}
	return " ";
}

Entity::Entity() {
	_define_ancestry(AncestralClass::NODE);
#ifdef DEBUG_ENABLED
	total_node_count.increment();
#endif
	// Default member initializer for bitfield is a C++20 extension, so:

	data.run_mode = RUN_MODE_INHERIT;
	data.physics_interpolation_mode = PHYSICS_INTERPOLATION_MODE_INHERIT;

	data.physics_process = false;
	data.process = false;

	data.physics_process_internal = false;
	data.process_internal = false;

	data.input = false;
	data.shortcut_input = false;
	data.unhandled_input = false;
	data.unhandled_key_input = false;

	data.physics_interpolated = true;
	data.physics_interpolation_reset_requested = false;
	data.use_identity_transform = false;

	data.use_placeholder = false;

	data.display_folded = false;
	data.editable_instance = false;

	data.ready_notified = false; // This is a small hack, so if a node is added during _start() to the tree, it correctly gets the _start() notification.
	data.ready_first = true;

	data.auto_translate_mode = AUTO_TRANSLATE_MODE_INHERIT;
	data.is_auto_translating = true;
	data.is_auto_translate_dirty = true;

	data.is_translation_domain_inherited = true;
	data.is_translation_domain_dirty = true;
}

Entity::~Entity() {
	data.grouped.clear();
	data.owned.clear();
	data.children.clear();
	data.children_cache.clear();

	ERR_FAIL_COND(data.parent);
	ERR_FAIL_COND(data.children_cache.size());

#ifdef DEBUG_ENABLED
	total_node_count.decrement();
#endif
}

////////////////////////////////
// Multithreaded locked version of Object functions.

#ifdef DEBUG_ENABLED

void Entity::set_script(const Variant &p_script) {
	ERR_THREAD_GUARD;
	Object::set_script(p_script);
}

Variant Entity::get_script() const {
	ERR_THREAD_GUARD_V(Variant());
	return Object::get_script();
}

bool Entity::has_meta(const StringName &p_name) const {
	ERR_THREAD_GUARD_V(false);
	return Object::has_meta(p_name);
}

void Entity::set_meta(const StringName &p_name, const Variant &p_value) {
	ERR_THREAD_GUARD;
	Object::set_meta(p_name, p_value);
	_emit_editor_state_changed();
}

void Entity::remove_meta(const StringName &p_name) {
	ERR_THREAD_GUARD;
	Object::remove_meta(p_name);
	_emit_editor_state_changed();
}

Variant Entity::get_meta(const StringName &p_name, const Variant &p_default) const {
	ERR_THREAD_GUARD_V(Variant());
	return Object::get_meta(p_name, p_default);
}

void Entity::get_meta_list(List<StringName> *p_list) const {
	ERR_THREAD_GUARD;
	Object::get_meta_list(p_list);
}

Error Entity::emit_signalp(const StringName &p_name, const Variant **p_args, int p_argcount) {
	ERR_THREAD_GUARD_V(ERR_INVALID_PARAMETER);
	return Object::emit_signalp(p_name, p_args, p_argcount);
}

bool Entity::has_signal(const StringName &p_name) const {
	ERR_THREAD_GUARD_V(false);
	return Object::has_signal(p_name);
}

void Entity::get_signal_list(List<MethodInfo> *p_signals) const {
	ERR_THREAD_GUARD;
	Object::get_signal_list(p_signals);
}

void Entity::get_signal_connection_list(const StringName &p_signal, List<Connection> *p_connections) const {
	ERR_THREAD_GUARD;
	Object::get_signal_connection_list(p_signal, p_connections);
}

void Entity::get_all_signal_connections(List<Connection> *p_connections) const {
	ERR_THREAD_GUARD;
	Object::get_all_signal_connections(p_connections);
}

int Entity::get_persistent_signal_connection_count() const {
	ERR_THREAD_GUARD_V(0);
	return Object::get_persistent_signal_connection_count();
}

uint32_t Entity::get_signal_connection_flags(const StringName &p_signal, const Callable &p_callable) const {
	ERR_THREAD_GUARD_V(0);
	return Object::get_signal_connection_flags(p_signal, p_callable);
}

void Entity::get_signals_connected_to_this(List<Connection> *p_connections) const {
	ERR_THREAD_GUARD;
	Object::get_signals_connected_to_this(p_connections);
}

Error Entity::connect(const StringName &p_signal, const Callable &p_callable, uint32_t p_flags) {
	ERR_THREAD_GUARD_V(ERR_INVALID_PARAMETER);

	Error retval = Object::connect(p_signal, p_callable, p_flags);
#ifdef TOOLS_ENABLED
	if (p_flags & CONNECT_PERSIST) {
		_emit_editor_state_changed();
	}
#endif

	return retval;
}

void Entity::disconnect(const StringName &p_signal, const Callable &p_callable) {
	ERR_THREAD_GUARD;

#ifdef TOOLS_ENABLED
	// Already under thread guard, don't check again.
	uint32_t connection_flags = Object::get_signal_connection_flags(p_signal, p_callable);
#endif

	[[maybe_unused]] bool changed = Object::_disconnect(p_signal, p_callable);

#ifdef TOOLS_ENABLED
	if (changed && connection_flags & CONNECT_PERSIST) {
		_emit_editor_state_changed();
	}
#endif
}

bool Entity::is_connected(const StringName &p_signal, const Callable &p_callable) const {
	ERR_THREAD_GUARD_V(false);
	return Object::is_connected(p_signal, p_callable);
}

bool Entity::has_connections(const StringName &p_signal) const {
	ERR_THREAD_GUARD_V(false);
	return Object::has_connections(p_signal);
}

#endif
