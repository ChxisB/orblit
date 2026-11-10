/**************************************************************************/
/*  navigation_system_spatial_manager.cpp                                      */

#include "navigation_system_spatial_manager.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "systems/navigation_spatial/navigation_system_spatial.h"
#include "systems/navigation_spatial/navigation_system_spatial_dummy.h"

static NavigationSystem3D *navigation_system_3d = nullptr;

void NavigationSystem3DManager::initialize_system() {
	ERR_FAIL_COND(navigation_system_3d != nullptr);

	// Init 3D Navigation Server.
	navigation_system_3d = NavigationSystem3DManager::get_singleton()->new_system(
			GLOBAL_GET(NavigationSystem3DManager::setting_property_name));
	if (!navigation_system_3d) {
		// Navigation server not found, use the default.
		navigation_system_3d = NavigationSystem3DManager::get_singleton()->new_default_system();
	}

	// Fall back to dummy if no default server has been registered.
	if (!navigation_system_3d) {
		WARN_VERBOSE("Failed to initialize NavigationSystem3D. Fall back to dummy server.");
		navigation_system_3d = memnew(NavigationSystem3DDummy);
	}

	// Should be impossible, but make sure it's not null.
	ERR_FAIL_NULL_MSG(navigation_system_3d, "Failed to initialize NavigationSystem3D.");
	navigation_system_3d->init();
}

void NavigationSystem3DManager::finalize_system() {
	ERR_FAIL_NULL(navigation_system_3d);
	navigation_system_3d->finish();
	memdelete(navigation_system_3d);
	navigation_system_3d = nullptr;
}

const String NavigationSystem3DManager::setting_property_name(PNAME("navigation/spatial/navigation_engine"));

void NavigationSystem3DManager::on_systems_changed() {
	String navigation_systems_enum_str("DEFAULT");
	for (int i = get_systems_count() - 1; 0 <= i; --i) {
		navigation_systems_enum_str += "," + get_system_name(i);
	}
	ProjectSettings::get_singleton()->set_custom_property_info(PropertyInfo(Variant::STRING, setting_property_name, PROPERTY_HINT_ENUM, navigation_systems_enum_str));
	ProjectSettings::get_singleton()->set_restart_if_changed(setting_property_name, true);
	ProjectSettings::get_singleton()->set_as_basic(setting_property_name, true);
}

void NavigationSystem3DManager::register_members() {
	ClassRegistry::bind_method(D_METHOD("register_system", "name", "create_callback"), &NavigationSystem3DManager::register_system);
	ClassRegistry::bind_method(D_METHOD("set_default_system", "name", "priority"), &NavigationSystem3DManager::set_default_system);
}

NavigationSystem3DManager *NavigationSystem3DManager::get_singleton() {
	return singleton;
}

void NavigationSystem3DManager::register_system(const String &p_name, const Callable &p_create_callback) {
	ERR_FAIL_COND(find_system_id(p_name) != -1);
	navigation_systems.push_back(ClassInfo(p_name, p_create_callback));
	on_systems_changed();
}

void NavigationSystem3DManager::set_default_system(const String &p_name, int p_priority) {
	const int id = find_system_id(p_name);
	ERR_FAIL_COND(id == -1); // Not found
	if (default_system_priority < p_priority) {
		default_system_id = id;
		default_system_priority = p_priority;
	}
}

int NavigationSystem3DManager::find_system_id(const String &p_name) {
	for (int i = navigation_systems.size() - 1; 0 <= i; --i) {
		if (p_name == navigation_systems[i].name) {
			return i;
		}
	}
	return -1;
}

int NavigationSystem3DManager::get_systems_count() {
	return navigation_systems.size();
}

String NavigationSystem3DManager::get_system_name(int p_id) {
	ERR_FAIL_INDEX_V(p_id, get_systems_count(), "");
	return navigation_systems[p_id].name;
}

NavigationSystem3D *NavigationSystem3DManager::new_default_system() {
	if (default_system_id == -1) {
		return nullptr;
	}
	Variant ret;
	Callable::CallError ce;
	navigation_systems[default_system_id].create_callback.callp(nullptr, 0, ret, ce);
	ERR_FAIL_COND_V(ce.error != Callable::CallError::CALL_OK, nullptr);
	return Object::cast_to<NavigationSystem3D>(ret.get_validated_object());
}

NavigationSystem3D *NavigationSystem3DManager::new_system(const String &p_name) {
	int id = find_system_id(p_name);
	if (id == -1) {
		return nullptr;
	} else {
		Variant ret;
		Callable::CallError ce;
		navigation_systems[id].create_callback.callp(nullptr, 0, ret, ce);
		ERR_FAIL_COND_V(ce.error != Callable::CallError::CALL_OK, nullptr);
		return Object::cast_to<NavigationSystem3D>(ret.get_validated_object());
	}
}

NavigationSystem3D *NavigationSystem3DManager::create_dummy_system_callback() {
	return memnew(NavigationSystem3DDummy);
}

void NavigationSystem3DManager::initialize_system_manager() {
	ERR_FAIL_COND(singleton != nullptr);
	singleton = memnew(NavigationSystem3DManager);
}

void NavigationSystem3DManager::finalize_system_manager() {
	ERR_FAIL_NULL(singleton);
	memdelete(singleton);
	singleton = nullptr;
}
