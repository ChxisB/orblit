/**************************************************************************/
/*  world_environment.cpp                                                 */

#include "world_environment.h"

#include "core/object/class_registry.h"
#include "scene/spatial/entity_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/viewport.h"

void WorldEnvironment::_notification(int p_what) {
	switch (p_what) {
		case Entity3D::NOTIFICATION_ENTER_WORLD:
		case Entity3D::NOTIFICATION_ENTER_STAGE: {
			if (environment.is_valid()) {
				add_to_group("_world_environment_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
				_update_current_environment();
			}

			if (camera_attributes.is_valid()) {
				add_to_group("_world_camera_attributes_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
				_update_current_camera_attributes();
			}

			if (compositor.is_valid()) {
				add_to_group("_world_compositor_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
				_update_current_compositor();
			}
		} break;

		case Entity3D::NOTIFICATION_EXIT_WORLD:
		case Entity3D::NOTIFICATION_EXIT_STAGE: {
			if (environment.is_valid()) {
				remove_from_group("_world_environment_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
				_update_current_environment();
			}

			if (camera_attributes.is_valid()) {
				remove_from_group("_world_camera_attributes_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
				_update_current_camera_attributes();
			}

			if (compositor.is_valid()) {
				remove_from_group("_world_compositor_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
				_update_current_compositor();
			}
		} break;
	}
}

void WorldEnvironment::_update_current_environment() {
	WorldEnvironment *first = Object::cast_to<WorldEnvironment>(get_stage()->get_first_entity_in_group("_world_environment_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id())));

	if (first) {
		get_viewport()->find_world_3d()->set_environment(first->environment);
	} else {
		get_viewport()->find_world_3d()->set_environment(Ref<Environment>());
	}
	get_stage()->call_group_flags(Stage::GROUP_CALL_DEFERRED, "_world_environment_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()), "update_configuration_warnings");
}

void WorldEnvironment::_update_current_camera_attributes() {
	WorldEnvironment *first = Object::cast_to<WorldEnvironment>(get_stage()->get_first_entity_in_group("_world_camera_attributes_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id())));
	if (first) {
		get_viewport()->find_world_3d()->set_camera_attributes(first->camera_attributes);
	} else {
		get_viewport()->find_world_3d()->set_camera_attributes(Ref<CameraAttributes>());
	}

	get_stage()->call_group_flags(Stage::GROUP_CALL_DEFERRED, "_world_camera_attributes_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()), "update_configuration_warnings");
}

void WorldEnvironment::_update_current_compositor() {
	WorldEnvironment *first = Object::cast_to<WorldEnvironment>(get_stage()->get_first_entity_in_group("_world_compositor_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id())));
	if (first) {
		get_viewport()->find_world_3d()->set_compositor(first->compositor);
	} else {
		get_viewport()->find_world_3d()->set_compositor(Ref<Compositor>());
	}

	get_stage()->call_group_flags(Stage::GROUP_CALL_DEFERRED, "_world_compositor_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()), "update_configuration_warnings");
}

void WorldEnvironment::set_environment(const Ref<Environment> &p_environment) {
	if (environment == p_environment) {
		return;
	}
	if (is_inside_stage() && environment.is_valid()) {
		remove_from_group("_world_environment_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
	}

	environment = p_environment;

	if (is_inside_stage() && environment.is_valid()) {
		add_to_group("_world_environment_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
	}

	if (is_inside_stage()) {
		_update_current_environment();
	} else {
		update_configuration_warnings();
	}
}

Ref<Environment> WorldEnvironment::get_environment() const {
	return environment;
}

void WorldEnvironment::set_camera_attributes(const Ref<CameraAttributes> &p_camera_attributes) {
	if (camera_attributes == p_camera_attributes) {
		return;
	}

	if (is_inside_stage() && camera_attributes.is_valid() && get_viewport()->find_world_3d()->get_camera_attributes() == camera_attributes) {
		remove_from_group("_world_camera_attributes_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
	}

	camera_attributes = p_camera_attributes;
	if (is_inside_stage() && camera_attributes.is_valid()) {
		add_to_group("_world_camera_attributes_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
	}

	if (is_inside_stage()) {
		_update_current_camera_attributes();
	} else {
		update_configuration_warnings();
	}
}

Ref<CameraAttributes> WorldEnvironment::get_camera_attributes() const {
	return camera_attributes;
}

void WorldEnvironment::set_compositor(const Ref<Compositor> &p_compositor) {
	if (compositor == p_compositor) {
		return;
	}
	if (is_inside_stage() && compositor.is_valid()) {
		remove_from_group("_world_compositor_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
	}

	compositor = p_compositor;

	if (is_inside_stage() && compositor.is_valid()) {
		add_to_group("_world_compositor_" + itos(get_viewport()->find_world_3d()->get_scenario().get_id()));
	}

	if (is_inside_stage()) {
		_update_current_compositor();
	} else {
		update_configuration_warnings();
	}
}

Ref<Compositor> WorldEnvironment::get_compositor() const {
	return compositor;
}

PackedStringArray WorldEnvironment::get_configuration_warnings() const {
	PackedStringArray warnings = Entity::get_configuration_warnings();

	if (environment.is_null() && camera_attributes.is_null()) {
		warnings.push_back(RTR("To have any visible effect, WorldEnvironment requires its \"Environment\" property to contain an Environment, its \"Camera Attributes\" property to contain a CameraAttributes resource, or both."));
	}

	if (!is_inside_stage()) {
		return warnings;
	}

	if (environment.is_valid() && get_viewport()->find_world_3d()->get_environment() != environment) {
		warnings.push_back(("Only the first Environment has an effect in a scene (or set of instantiated scenes)."));
	}

	if (camera_attributes.is_valid() && get_viewport()->find_world_3d()->get_camera_attributes() != camera_attributes) {
		warnings.push_back(RTR("Only one WorldEnvironment is allowed per scene (or set of instantiated scenes)."));
	}

	if (compositor.is_valid() && get_viewport()->find_world_3d()->get_compositor() != compositor) {
		warnings.push_back(("Only the first Compositor has an effect in a scene (or set of instantiated scenes)."));
	}

	return warnings;
}

void WorldEnvironment::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_environment", "env"), &WorldEnvironment::set_environment);
	ClassRegistry::bind_method(D_METHOD("get_environment"), &WorldEnvironment::get_environment);
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "environment", PROPERTY_HINT_RESOURCE_TYPE, Environment::get_static_class_name(), PROPERTY_USAGE_DEFAULT | PROPERTY_USAGE_EDITOR_INSTANTIATE_OBJECT), "set_environment", "get_environment");

	ClassRegistry::bind_method(D_METHOD("set_camera_attributes", "camera_attributes"), &WorldEnvironment::set_camera_attributes);
	ClassRegistry::bind_method(D_METHOD("get_camera_attributes"), &WorldEnvironment::get_camera_attributes);
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "camera_attributes", PROPERTY_HINT_RESOURCE_TYPE, "CameraAttributesPractical,CameraAttributesPhysical"), "set_camera_attributes", "get_camera_attributes");

	ClassRegistry::bind_method(D_METHOD("set_compositor", "compositor"), &WorldEnvironment::set_compositor);
	ClassRegistry::bind_method(D_METHOD("get_compositor"), &WorldEnvironment::get_compositor);
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "compositor", PROPERTY_HINT_RESOURCE_TYPE, Compositor::get_static_class_name()), "set_compositor", "get_compositor");
}

WorldEnvironment::WorldEnvironment() {
}
