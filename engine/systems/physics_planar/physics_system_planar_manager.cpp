/**************************************************************************/
/*  physics_system_planar_manager.cpp                                         */

#include "physics_system_planar_manager.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "systems/physics_planar/physics_system_planar.h"
#include "systems/physics_planar/physics_system_planar_dummy.h"

static PhysicsSystem2D *physics_system_2d = nullptr;

PhysicsSystem2DManager *PhysicsSystem2DManager::singleton = nullptr;
const String PhysicsSystem2DManager::setting_property_name(PNAME("physics/planar/physics_engine"));

void PhysicsSystem2DManager::initialize_system_manager() {
	ERR_FAIL_COND(singleton != nullptr);
	singleton = memnew(PhysicsSystem2DManager);
}

void PhysicsSystem2DManager::finalize_system_manager() {
	ERR_FAIL_NULL(singleton);
	memdelete(singleton);
	singleton = nullptr;
}

void PhysicsSystem2DManager::initialize_system() {
#ifndef PHYSICS_PLANAR_DISABLED
	// 2D Physics server
	physics_system_2d = PhysicsSystem2DManager::get_singleton()->new_system(
			GLOBAL_GET(PhysicsSystem2DManager::get_singleton()->setting_property_name));
	if (!physics_system_2d) {
		// Physics server not found, Use the default physics
		physics_system_2d = PhysicsSystem2DManager::get_singleton()->new_default_system();
	}

	// Fall back to dummy if no default server has been registered.
	if (!physics_system_2d) {
		WARN_PRINT(vformat("Falling back to dummy PhysicsSystem2D; 2D physics functionality will be disabled. If this is intended, set the %s project setting to Dummy.", PhysicsSystem2DManager::setting_property_name));
		physics_system_2d = memnew(PhysicsSystem2DDummy);
	}

	// Should be impossible, but make sure it's not null.
	ERR_FAIL_NULL_MSG(physics_system_2d, "Failed to initialize PhysicsSystem2D.");
	physics_system_2d->init();
#endif // PHYSICS_PLANAR_DISABLED
}

void PhysicsSystem2DManager::finalize_system() {
	ERR_FAIL_NULL(physics_system_2d);
	physics_system_2d->finish();
	memdelete(physics_system_2d);
	physics_system_2d = nullptr;
}

void PhysicsSystem2DManager::on_systems_changed() {
	String physics_systems("DEFAULT");
	for (int i = get_systems_count() - 1; 0 <= i; --i) {
		physics_systems += "," + get_system_name(i);
	}
	ProjectSettings::get_singleton()->set_custom_property_info(PropertyInfo(Variant::STRING, setting_property_name, PROPERTY_HINT_ENUM, physics_systems));
	ProjectSettings::get_singleton()->set_restart_if_changed(setting_property_name, true);
	ProjectSettings::get_singleton()->set_as_basic(setting_property_name, true);
}

void PhysicsSystem2DManager::register_members() {
	ClassRegistry::bind_method(D_METHOD("register_system", "name", "create_callback"), &PhysicsSystem2DManager::register_system);
	ClassRegistry::bind_method(D_METHOD("set_default_system", "name", "priority"), &PhysicsSystem2DManager::set_default_system);
}

PhysicsSystem2DManager *PhysicsSystem2DManager::get_singleton() {
	return singleton;
}

void PhysicsSystem2DManager::register_system(const String &p_name, const Callable &p_create_callback) {
	//ERR_FAIL_COND(!p_create_callback.is_valid());
	ERR_FAIL_COND(find_system_id(p_name) != -1);
	physics_2d_systems.push_back(ClassInfo(p_name, p_create_callback));
	on_systems_changed();
}

void PhysicsSystem2DManager::set_default_system(const String &p_name, int p_priority) {
	const int id = find_system_id(p_name);
	ERR_FAIL_COND(id == -1); // Not found
	if (default_system_priority < p_priority) {
		default_system_id = id;
		default_system_priority = p_priority;
	}
}

int PhysicsSystem2DManager::find_system_id(const String &p_name) {
	for (int i = physics_2d_systems.size() - 1; 0 <= i; --i) {
		if (p_name == physics_2d_systems[i].name) {
			return i;
		}
	}
	return -1;
}

int PhysicsSystem2DManager::get_systems_count() {
	return physics_2d_systems.size();
}

String PhysicsSystem2DManager::get_system_name(int p_id) {
	ERR_FAIL_INDEX_V(p_id, get_systems_count(), "");
	return physics_2d_systems[p_id].name;
}

PhysicsSystem2D *PhysicsSystem2DManager::new_default_system() {
	if (default_system_id == -1) {
		return nullptr;
	}
	Variant ret;
	Callable::CallError ce;
	physics_2d_systems[default_system_id].create_callback.callp(nullptr, 0, ret, ce);
	ERR_FAIL_COND_V(ce.error != Callable::CallError::CALL_OK, nullptr);
	return Object::cast_to<PhysicsSystem2D>(ret.get_validated_object());
}

PhysicsSystem2D *PhysicsSystem2DManager::new_system(const String &p_name) {
	int id = find_system_id(p_name);
	if (id == -1) {
		return nullptr;
	} else {
		Variant ret;
		Callable::CallError ce;
		physics_2d_systems[id].create_callback.callp(nullptr, 0, ret, ce);
		ERR_FAIL_COND_V(ce.error != Callable::CallError::CALL_OK, nullptr);
		return Object::cast_to<PhysicsSystem2D>(ret.get_validated_object());
	}
}
