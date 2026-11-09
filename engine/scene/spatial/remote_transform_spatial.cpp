/**************************************************************************/
/*  remote_transform_spatial.cpp                                               */

#include "remote_transform_spatial.h"

#include "core/object/class_registry.h"

void RemoteTransform3D::_update_cache() {
	cache = ObjectID();
	if (has_entity(remote_node)) {
		Entity *node = get_entity(remote_node);
		if (!node || this == node || node->is_ancestor_of(this) || is_ancestor_of(node)) {
			return;
		}

		cache = node->get_instance_id();
	}
}

void RemoteTransform3D::_update_remote() {
	if (!is_inside_stage()) {
		return;
	}

	if (cache.is_null()) {
		return;
	}

	Entity3D *target_node = ObjectRegistry::get_instance<Entity3D>(cache);
	if (!target_node) {
		return;
	}

	if (!target_node->is_inside_stage()) {
		return;
	}

	Transform3D our_trans = use_global_coordinates ? get_global_transform() : get_transform();

	if (update_remote_position && update_remote_rotation && update_remote_scale) {
		if (use_global_coordinates) {
			target_node->set_global_transform(our_trans);
		} else {
			target_node->set_transform(our_trans);
		}
	} else {
		Transform3D target_trans = use_global_coordinates ? target_node->get_global_transform() : target_node->get_transform();

		if (update_remote_rotation && update_remote_scale) {
			target_trans.basis = our_trans.basis;
		} else if (update_remote_rotation) {
			for (int i = 0; i < 3; i++) {
				Vector3 our_col = our_trans.basis.get_column(i);
				Vector3 target_col = target_trans.basis.get_column(i);
				target_trans.basis.set_column(i, our_col.normalized() * target_col.length());
			}
		} else if (update_remote_scale) {
			for (int i = 0; i < 3; i++) {
				Vector3 our_col = our_trans.basis.get_column(i);
				Vector3 target_col = target_trans.basis.get_column(i);
				target_trans.basis.set_column(i, target_col.normalized() * our_col.length());
			}
		}

		if (update_remote_position) {
			target_trans.origin = our_trans.origin;
		}

		if (use_global_coordinates) {
			target_node->set_global_transform(target_trans);
		} else {
			target_node->set_transform(target_trans);
		}
	}
}

void RemoteTransform3D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			_update_cache();
		} break;

		case NOTIFICATION_RESET_PHYSICS_INTERPOLATION: {
			if (cache.is_valid()) {
				_update_remote();
				Entity3D *n = ObjectRegistry::get_instance<Entity3D>(cache);
				if (n) {
					n->reset_physics_interpolation();
				}
			}
		} break;

		case NOTIFICATION_LOCAL_TRANSFORM_CHANGED:
		case NOTIFICATION_TRANSFORM_CHANGED: {
			if (!is_inside_stage()) {
				break;
			}

			if (cache.is_valid()) {
				_update_remote();
			}
		} break;
	}
}

void RemoteTransform3D::set_remote_node(const EntityPath &p_remote_node) {
	if (remote_node == p_remote_node) {
		return;
	}

	remote_node = p_remote_node;
	if (is_inside_stage()) {
		_update_cache();
		_update_remote();
	}

	update_configuration_warnings();
}

EntityPath RemoteTransform3D::get_remote_node() const {
	return remote_node;
}

void RemoteTransform3D::set_use_global_coordinates(const bool p_enable) {
	if (use_global_coordinates == p_enable) {
		return;
	}

	use_global_coordinates = p_enable;

	set_notify_transform(use_global_coordinates);
	set_notify_local_transform(!use_global_coordinates);
	_update_remote();
}

bool RemoteTransform3D::get_use_global_coordinates() const {
	return use_global_coordinates;
}

void RemoteTransform3D::set_update_position(const bool p_update) {
	if (update_remote_position == p_update) {
		return;
	}
	update_remote_position = p_update;
	_update_remote();
}

bool RemoteTransform3D::get_update_position() const {
	return update_remote_position;
}

void RemoteTransform3D::set_update_rotation(const bool p_update) {
	if (update_remote_rotation == p_update) {
		return;
	}
	update_remote_rotation = p_update;
	_update_remote();
}

bool RemoteTransform3D::get_update_rotation() const {
	return update_remote_rotation;
}

void RemoteTransform3D::set_update_scale(const bool p_update) {
	if (update_remote_scale == p_update) {
		return;
	}
	update_remote_scale = p_update;
	_update_remote();
}

bool RemoteTransform3D::get_update_scale() const {
	return update_remote_scale;
}

void RemoteTransform3D::force_update_cache() {
	_update_cache();
}

PackedStringArray RemoteTransform3D::get_configuration_warnings() const {
	PackedStringArray warnings = Entity3D::get_configuration_warnings();

	if (!has_entity(remote_node) || !Object::cast_to<Entity3D>(get_entity(remote_node))) {
		warnings.push_back(RTR("The \"Remote Path\" property must point to a valid Node3D or Node3D-derived node to work."));
	}

	return warnings;
}

void RemoteTransform3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_remote_node", "path"), &RemoteTransform3D::set_remote_node);
	ClassRegistry::bind_method(D_METHOD("get_remote_node"), &RemoteTransform3D::get_remote_node);
	ClassRegistry::bind_method(D_METHOD("force_update_cache"), &RemoteTransform3D::force_update_cache);

	ClassRegistry::bind_method(D_METHOD("set_use_global_coordinates", "use_global_coordinates"), &RemoteTransform3D::set_use_global_coordinates);
	ClassRegistry::bind_method(D_METHOD("get_use_global_coordinates"), &RemoteTransform3D::get_use_global_coordinates);

	ClassRegistry::bind_method(D_METHOD("set_update_position", "update_remote_position"), &RemoteTransform3D::set_update_position);
	ClassRegistry::bind_method(D_METHOD("get_update_position"), &RemoteTransform3D::get_update_position);
	ClassRegistry::bind_method(D_METHOD("set_update_rotation", "update_remote_rotation"), &RemoteTransform3D::set_update_rotation);
	ClassRegistry::bind_method(D_METHOD("get_update_rotation"), &RemoteTransform3D::get_update_rotation);
	ClassRegistry::bind_method(D_METHOD("set_update_scale", "update_remote_scale"), &RemoteTransform3D::set_update_scale);
	ClassRegistry::bind_method(D_METHOD("get_update_scale"), &RemoteTransform3D::get_update_scale);

	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "remote_path", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "Entity3D"), "set_remote_node", "get_remote_node");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "use_global_coordinates"), "set_use_global_coordinates", "get_use_global_coordinates");

	ADD_GROUP("Update", "update_");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "update_position"), "set_update_position", "get_update_position");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "update_rotation"), "set_update_rotation", "get_update_rotation");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "update_scale"), "set_update_scale", "get_update_scale");
}

RemoteTransform3D::RemoteTransform3D() {
	set_notify_transform(use_global_coordinates);
	set_notify_local_transform(!use_global_coordinates);
}
