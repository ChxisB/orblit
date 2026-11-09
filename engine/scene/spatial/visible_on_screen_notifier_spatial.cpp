/**************************************************************************/
/*  visible_on_screen_notifier_spatial.cpp                                     */

#include "visible_on_screen_notifier_spatial.h"

#include "core/config/engine.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "systems/rendering/rendering_system.h"

void VisibleOnScreenNotifier3D::_visibility_enter() {
	if (!is_inside_stage() || Engine::get_singleton()->is_editor_hint()) {
		return;
	}

	on_screen = true;
	emit_signal(SceneStringName(screen_entered));
	_screen_enter();
}
void VisibleOnScreenNotifier3D::_visibility_exit() {
	if (!is_inside_stage() || Engine::get_singleton()->is_editor_hint()) {
		return;
	}

	on_screen = false;
	emit_signal(SceneStringName(screen_exited));
	_screen_exit();
}

void VisibleOnScreenNotifier3D::set_aabb(const AABB &p_aabb) {
	if (aabb == p_aabb) {
		return;
	}
	aabb = p_aabb;

	RS::get_singleton()->visibility_notifier_set_aabb(get_base(), aabb);

	update_gizmos();
}

AABB VisibleOnScreenNotifier3D::get_aabb() const {
	return aabb;
}

bool VisibleOnScreenNotifier3D::is_on_screen() const {
	return on_screen;
}

void VisibleOnScreenNotifier3D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE:
		case NOTIFICATION_EXIT_STAGE: {
			on_screen = false;
		} break;
	}
}

void VisibleOnScreenNotifier3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_aabb", "rect"), &VisibleOnScreenNotifier3D::set_aabb);
	ClassRegistry::bind_method(D_METHOD("is_on_screen"), &VisibleOnScreenNotifier3D::is_on_screen);

	ADD_PROPERTY(PropertyInfo(Variant::AABB, "aabb", PROPERTY_HINT_NONE, "suffix:m"), "set_aabb", "get_aabb");

	ADD_SIGNAL(MethodInfo("screen_entered"));
	ADD_SIGNAL(MethodInfo("screen_exited"));
}

VisibleOnScreenNotifier3D::VisibleOnScreenNotifier3D() {
	RID notifier = RS::get_singleton()->visibility_notifier_create();
	RS::get_singleton()->visibility_notifier_set_aabb(notifier, aabb);
	RS::get_singleton()->visibility_notifier_set_callbacks(notifier, callable_mp(this, &VisibleOnScreenNotifier3D::_visibility_enter), callable_mp(this, &VisibleOnScreenNotifier3D::_visibility_exit));
	set_base(notifier);
}

VisibleOnScreenNotifier3D::~VisibleOnScreenNotifier3D() {
	RID base_old = get_base();
	set_base(RID());
	ERR_FAIL_NULL(RenderingSystem::get_singleton());
	RS::get_singleton()->free_rid(base_old);
}

//////////////////////////////////////

void VisibleOnScreenEnabler3D::_screen_enter() {
	_update_enable_mode(true);
}

void VisibleOnScreenEnabler3D::_screen_exit() {
	_update_enable_mode(false);
}

void VisibleOnScreenEnabler3D::set_enable_mode(EnableMode p_mode) {
	enable_mode = p_mode;
	if (is_inside_stage()) {
		_update_enable_mode(is_on_screen());
	}
}
VisibleOnScreenEnabler3D::EnableMode VisibleOnScreenEnabler3D::get_enable_mode() {
	return enable_mode;
}

void VisibleOnScreenEnabler3D::set_enable_node_path(EntityPath p_path) {
	if (enable_node_path == p_path) {
		return;
	}
	enable_node_path = p_path;
	if (enable_node_path.is_empty()) {
		node_id = ObjectID();
		return;
	}
	if (is_inside_stage() && !Engine::get_singleton()->is_editor_hint()) {
		node_id = ObjectID();
		Entity *node = get_entity(enable_node_path);
		if (node) {
			node_id = node->get_instance_id();
			_update_enable_mode(is_on_screen());
		}
	}
}
EntityPath VisibleOnScreenEnabler3D::get_enable_node_path() {
	return enable_node_path;
}

void VisibleOnScreenEnabler3D::_update_enable_mode(bool p_enable) {
	Entity *node = ObjectRegistry::get_instance<Entity>(node_id);
	if (node) {
		if (p_enable) {
			switch (enable_mode) {
				case ENABLE_MODE_INHERIT: {
					node->set_run_mode(RUN_MODE_INHERIT);
				} break;
				case ENABLE_MODE_ALWAYS: {
					node->set_run_mode(RUN_MODE_ALWAYS);
				} break;
				case ENABLE_MODE_WHEN_PAUSED: {
					node->set_run_mode(RUN_MODE_WHEN_PAUSED);
				} break;
			}
		} else {
			node->set_run_mode(RUN_MODE_DISABLED);
		}
	}
}
void VisibleOnScreenEnabler3D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			if (Engine::get_singleton()->is_editor_hint()) {
				return;
			}
			node_id = ObjectID();
			if (enable_node_path.is_empty()) {
				return;
			}

			Entity *node = get_entity(enable_node_path);
			if (node) {
				node_id = node->get_instance_id();
				node->set_run_mode(RUN_MODE_DISABLED);
			}
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			node_id = ObjectID();
		} break;
	}
}

void VisibleOnScreenEnabler3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_enable_mode", "mode"), &VisibleOnScreenEnabler3D::set_enable_mode);
	ClassRegistry::bind_method(D_METHOD("get_enable_mode"), &VisibleOnScreenEnabler3D::get_enable_mode);

	ClassRegistry::bind_method(D_METHOD("set_enable_node_path", "path"), &VisibleOnScreenEnabler3D::set_enable_node_path);
	ClassRegistry::bind_method(D_METHOD("get_enable_node_path"), &VisibleOnScreenEnabler3D::get_enable_node_path);

	ADD_GROUP("Enabling", "enable_");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "enable_mode", PROPERTY_HINT_ENUM, "Inherit,Always,When Paused"), "set_enable_mode", "get_enable_mode");
	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "enable_node_path"), "set_enable_node_path", "get_enable_node_path");

	BIND_ENUM_CONSTANT(ENABLE_MODE_INHERIT);
	BIND_ENUM_CONSTANT(ENABLE_MODE_ALWAYS);
	BIND_ENUM_CONSTANT(ENABLE_MODE_WHEN_PAUSED);
}

VisibleOnScreenEnabler3D::VisibleOnScreenEnabler3D() {
}
