/**************************************************************************/
/*  navigation_system_spatial_manager.h                                        */

#pragma once

#include "core/object/object.h"

class NavigationSystem3D;

class NavigationSystem3DManager : public Object {
	ORBLIT_CLASS(NavigationSystem3DManager, Object);

	static inline NavigationSystem3DManager *singleton = nullptr;

	struct ClassInfo {
		String name;
		Callable create_callback;

		ClassInfo() {}

		ClassInfo(const String &p_name, const Callable &p_create_callback) :
				name(p_name),
				create_callback(p_create_callback) {}

		ClassInfo(const ClassInfo &p_ci) :
				name(p_ci.name),
				create_callback(p_ci.create_callback) {}

		void operator=(const ClassInfo &p_ci) {
			name = p_ci.name;
			create_callback = p_ci.create_callback;
		}
	};

	Vector<ClassInfo> navigation_systems;
	int default_system_id = -1;
	int default_system_priority = -1;

	void on_systems_changed();

protected:
	static void register_members();

public:
	static const String setting_property_name;

	static NavigationSystem3DManager *get_singleton();

	void register_system(const String &p_name, const Callable &p_create_callback);
	void set_default_system(const String &p_name, int p_priority = 0);
	int find_system_id(const String &p_name);
	int get_systems_count();
	String get_system_name(int p_id);
	NavigationSystem3D *new_default_system();
	NavigationSystem3D *new_system(const String &p_name);

	static void initialize_system();
	static void finalize_system();

	static void initialize_system_manager();
	static void finalize_system_manager();
	static NavigationSystem3D *create_dummy_system_callback();
};
