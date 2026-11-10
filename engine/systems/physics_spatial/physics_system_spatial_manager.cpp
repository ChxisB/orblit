/**************************************************************************/
/*  physics_system_spatial_manager.cpp                                         */

#include "physics_system_spatial_manager.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "systems/physics_spatial/physics_system_spatial.h"
#include "systems/physics_spatial/physics_system_spatial_dummy.h"

static PhysicsSystem3D *physics_system_3d = nullptr;

PhysicsSystem3DManager *PhysicsSystem3DManager::singleton = nullptr;
const String PhysicsSystem3DManager::setting_property_name(PNAME("physics/spatial/physics_engine"));

void PhysicsSystem3DManager::initialize_system_manager() {
	ERR_FAIL_COND(singleton != nullptr);
	singleton = memnew(PhysicsSystem3DManager);
}

void PhysicsSystem3DManager::finalize_system_manager() {
	ERR_FAIL_NULL(singleton);
	memdelete(singleton);
	singleton = nullptr;
}

void PhysicsSystem3DManager::initialize_system() {
#ifndef PHYSICS_SPATIAL_DISABLED
	/// 3D Physics Server
	physics_system_3d = PhysicsSystem3DManager::get_singleton()->new_system(
			GLOBAL_GET(PhysicsSystem3DManager::setting_property_name));
	if (!physics_system_3d) {
		// Physics server not found, Use the default physics
		physics_system_3d = PhysicsSystem3DManager::get_singleton()->new_default_system();
	}

	// Fall back to dummy if no default server has been registered.
	if (!physics_system_3d) {
		WARN_PRINT(vformat("Falling back to dummy PhysicsSystem3D; 3D physics functionality will be disabled. If this is intended, set the %s project setting to Dummy.", PhysicsSystem3DManager::setting_property_name));
		physics_system_3d = memnew(PhysicsSystem3DDummy);
	}

	// Should be impossible, but make sure it's not null.
	ERR_FAIL_NULL_MSG(physics_system_3d, "Failed to initialize PhysicsSystem3D.");
	physics_system_3d->init();
#endif // PHYSICS_SPATIAL_DISABLED
}

void PhysicsSystem3DManager::finalize_system() {
	ERR_FAIL_NULL(physics_system_3d);
	physics_system_3d->finish();
	memdelete(physics_system_3d);
	physics_system_3d = nullptr;
}

void PhysicsSystem3DManager::on_systems_changed() {
	String physics_systems2("DEFAULT");
	for (int i = get_systems_count() - 1; 0 <= i; --i) {
		physics_systems2 += "," + get_system_name(i);
	}
	ProjectSettings::get_singleton()->set_custom_property_info(PropertyInfo(Variant::STRING, setting_property_name, PROPERTY_HINT_ENUM, physics_systems2));
	ProjectSettings::get_singleton()->set_restart_if_changed(setting_property_name, true);
	ProjectSettings::get_singleton()->set_as_basic(setting_property_name, true);
}

void PhysicsSystem3DManager::register_members() {
	ClassRegistry::bind_method(D_METHOD("register_system", "name", "create_callback"), &PhysicsSystem3DManager::register_system);
	ClassRegistry::bind_method(D_METHOD("set_default_system", "name", "priority"), &PhysicsSystem3DManager::set_default_system);
}

PhysicsSystem3DManager *PhysicsSystem3DManager::get_singleton() {
	return singleton;
}

void PhysicsSystem3DManager::register_system(const String &p_name, const Callable &p_create_callback) {
	//ERR_FAIL_COND(!p_create_callback.is_valid());
	ERR_FAIL_COND(find_system_id(p_name) != -1);
	physics_systems.push_back(ClassInfo(p_name, p_create_callback));
	on_systems_changed();
}

void PhysicsSystem3DManager::set_default_system(const String &p_name, int p_priority) {
	const int id = find_system_id(p_name);
	ERR_FAIL_COND(id == -1); // Not found
	if (default_system_priority < p_priority) {
		default_system_id = id;
		default_system_priority = p_priority;
	}
}

int PhysicsSystem3DManager::find_system_id(const String &p_name) {
	for (int i = physics_systems.size() - 1; 0 <= i; --i) {
		if (p_name == physics_systems[i].name) {
			return i;
		}
	}
	return -1;
}

int PhysicsSystem3DManager::get_systems_count() {
	return physics_systems.size();
}

String PhysicsSystem3DManager::get_system_name(int p_id) {
	ERR_FAIL_INDEX_V(p_id, get_systems_count(), "");
	return physics_systems[p_id].name;
}

PhysicsSystem3D *PhysicsSystem3DManager::new_default_system() {
	if (default_system_id == -1) {
		return nullptr;
	}
	Variant ret;
	Callable::CallError ce;
	physics_systems[default_system_id].create_callback.callp(nullptr, 0, ret, ce);
	ERR_FAIL_COND_V(ce.error != Callable::CallError::CALL_OK, nullptr);
	return Object::cast_to<PhysicsSystem3D>(ret.get_validated_object());
}

PhysicsSystem3D *PhysicsSystem3DManager::new_system(const String &p_name) {
	int id = find_system_id(p_name);
	if (id == -1) {
		return nullptr;
	} else {
		Variant ret;
		Callable::CallError ce;
		physics_systems[id].create_callback.callp(nullptr, 0, ret, ce);
		ERR_FAIL_COND_V(ce.error != Callable::CallError::CALL_OK, nullptr);
		return Object::cast_to<PhysicsSystem3D>(ret.get_validated_object());
	}
}
