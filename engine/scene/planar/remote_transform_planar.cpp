/**************************************************************************/
/*  remote_transform_planar.cpp                                               */

#include "remote_transform_planar.h"

#include "core/object/class_registry.h"

void RemoteTransform2D::_update_cache() {
	cache = ObjectID();
	if (has_entity(remote_node)) {
		Entity *node = get_entity(remote_node);
		if (!node || this == node || node->is_ancestor_of(this) || is_ancestor_of(node)) {
			return;
		}

		cache = node->get_instance_id();
	}
}

void RemoteTransform2D::_track_remote_scale(const Transform2D &p_remote) {
	if (update_remote_scale) {
		return;
	}
	const Size2 remote_scale = p_remote.get_scale();
	if (!kept_remote_scale.is_equal_approx(remote_scale)) {
		kept_remote_scale = remote_scale;
	}
}

Transform2D RemoteTransform2D::_compose_remote_transform(const Transform2D &p_remote, const Transform2D &p_ours) const {
	// There are more steps in the operation of set_rotation, so avoid calling it.
	Transform2D trans = update_remote_rotation ? p_ours : p_remote;

	if (update_remote_rotation ^ update_remote_position) {
		trans.set_origin(update_remote_position ? p_ours.get_origin() : p_remote.get_origin());
	}
	if (update_remote_rotation ^ update_remote_scale) {
		trans.set_scale(update_remote_scale ? p_ours.get_scale() : kept_remote_scale);
	}
	return trans;
}

void RemoteTransform2D::_update_remote() {
	if (!is_inside_stage()) {
		return;
	}

	if (cache.is_null()) {
		return;
	}

	Entity2D *n = ObjectRegistry::get_instance<Entity2D>(cache);
	if (!n) {
		return;
	}

	if (!n->is_inside_stage()) {
		return;
	}

	if (!(update_remote_position || update_remote_rotation || update_remote_scale)) {
		return; // The transform data of the RemoteTransform2D is not used at all.
	}

	//todo make faster
	if (use_global_coordinates) {
		if (update_remote_position && update_remote_rotation && update_remote_scale) {
			n->set_global_transform(get_global_transform());
			return;
		}

		const Transform2D n_trans = n->get_global_transform();
		_track_remote_scale(n_trans);
		n->set_global_transform(_compose_remote_transform(n_trans, get_global_transform()));
	} else {
		if (update_remote_position && update_remote_rotation && update_remote_scale) {
			n->set_transform(get_transform());
			return;
		}

		const Transform2D n_trans = n->get_transform();
		_track_remote_scale(n_trans);
		n->set_transform(_compose_remote_transform(n_trans, get_transform()));
	}
}

void RemoteTransform2D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			_update_cache();
		} break;

		case NOTIFICATION_RESET_PHYSICS_INTERPOLATION: {
			if (cache.is_valid()) {
				_update_remote();
				Entity2D *n = ObjectRegistry::get_instance<Entity2D>(cache);
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

void RemoteTransform2D::set_remote_node(const EntityPath &p_remote_node) {
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

EntityPath RemoteTransform2D::get_remote_node() const {
	return remote_node;
}

void RemoteTransform2D::set_use_global_coordinates(const bool p_enable) {
	if (use_global_coordinates == p_enable) {
		return;
	}

	use_global_coordinates = p_enable;
	set_notify_transform(use_global_coordinates);
	set_notify_local_transform(!use_global_coordinates);
	_update_remote();
}

bool RemoteTransform2D::get_use_global_coordinates() const {
	return use_global_coordinates;
}

void RemoteTransform2D::set_update_position(const bool p_update) {
	if (update_remote_position == p_update) {
		return;
	}
	update_remote_position = p_update;
	_update_remote();
}

bool RemoteTransform2D::get_update_position() const {
	return update_remote_position;
}

void RemoteTransform2D::set_update_rotation(const bool p_update) {
	if (update_remote_rotation == p_update) {
		return;
	}
	update_remote_rotation = p_update;
	_update_remote();
}

bool RemoteTransform2D::get_update_rotation() const {
	return update_remote_rotation;
}

void RemoteTransform2D::set_update_scale(const bool p_update) {
	if (update_remote_scale == p_update) {
		return;
	}
	update_remote_scale = p_update;
	_update_remote();
}

bool RemoteTransform2D::get_update_scale() const {
	return update_remote_scale;
}

void RemoteTransform2D::force_update_cache() {
	_update_cache();
}

PackedStringArray RemoteTransform2D::get_configuration_warnings() const {
	PackedStringArray warnings = Entity2D::get_configuration_warnings();

	if (!has_entity(remote_node) || !Object::cast_to<Entity2D>(get_entity(remote_node))) {
		warnings.push_back(RTR("Path property must point to a valid Node2D node to work."));
	}

	return warnings;
}

void RemoteTransform2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_remote_node", "path"), &RemoteTransform2D::set_remote_node);
	ClassRegistry::bind_method(D_METHOD("get_remote_node"), &RemoteTransform2D::get_remote_node);
	ClassRegistry::bind_method(D_METHOD("force_update_cache"), &RemoteTransform2D::force_update_cache);

	ClassRegistry::bind_method(D_METHOD("set_use_global_coordinates", "use_global_coordinates"), &RemoteTransform2D::set_use_global_coordinates);
	ClassRegistry::bind_method(D_METHOD("get_use_global_coordinates"), &RemoteTransform2D::get_use_global_coordinates);

	ClassRegistry::bind_method(D_METHOD("set_update_position", "update_remote_position"), &RemoteTransform2D::set_update_position);
	ClassRegistry::bind_method(D_METHOD("get_update_position"), &RemoteTransform2D::get_update_position);
	ClassRegistry::bind_method(D_METHOD("set_update_rotation", "update_remote_rotation"), &RemoteTransform2D::set_update_rotation);
	ClassRegistry::bind_method(D_METHOD("get_update_rotation"), &RemoteTransform2D::get_update_rotation);
	ClassRegistry::bind_method(D_METHOD("set_update_scale", "update_remote_scale"), &RemoteTransform2D::set_update_scale);
	ClassRegistry::bind_method(D_METHOD("get_update_scale"), &RemoteTransform2D::get_update_scale);

	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "remote_path", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "Entity2D"), "set_remote_node", "get_remote_node");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "use_global_coordinates"), "set_use_global_coordinates", "get_use_global_coordinates");

	ADD_GROUP("Update", "update_");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "update_position"), "set_update_position", "get_update_position");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "update_rotation"), "set_update_rotation", "get_update_rotation");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "update_scale"), "set_update_scale", "get_update_scale");
}

RemoteTransform2D::RemoteTransform2D() {
	set_notify_transform(use_global_coordinates);
	set_notify_local_transform(!use_global_coordinates);
	set_hide_clip_children(true);
}
