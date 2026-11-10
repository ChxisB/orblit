/**************************************************************************/
/*  physics_system_planar_manager.h                                           */

#pragma once

#include "core/object/object.h"

class PhysicsSystem2D;

class PhysicsSystem2DManager : public Object {
	ORBLIT_CLASS(PhysicsSystem2DManager, Object);

	static PhysicsSystem2DManager *singleton;

	struct ClassInfo {
		String name;
		Callable create_callback;

		ClassInfo() {}

		ClassInfo(String p_name, Callable p_create_callback) :
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

	Vector<ClassInfo> physics_2d_systems;
	int default_system_id = -1;
	int default_system_priority = -1;

	void on_systems_changed();

protected:
	static void register_members();

public:
	static const String setting_property_name;

	static PhysicsSystem2DManager *get_singleton();

	void register_system(const String &p_name, const Callable &p_create_callback);
	void set_default_system(const String &p_name, int p_priority = 0);
	int find_system_id(const String &p_name);
	int get_systems_count();
	String get_system_name(int p_id);
	PhysicsSystem2D *new_default_system();
	PhysicsSystem2D *new_system(const String &p_name);

	static void initialize_system();
	static void finalize_system();

	static void initialize_system_manager();
	static void finalize_system_manager();
};
