/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "physics_system_planar.h"

#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "systems/physics_planar/physics_system_planar.h"
#include "systems/physics_planar/physics_system_planar_manager.h"
#include "systems/physics_planar/physics_system_planar_wrap_mt.h"

static PhysicsSystem2D *_createOrblitPhysics2DCallback() {
#ifdef THREADS_ENABLED
	bool using_threads = GLOBAL_GET("physics/planar/run_on_separate_thread");
#else
	bool using_threads = false;
#endif

	PhysicsSystem2D *physics_system_2d = memnew(OrblitPhysicsSystem2D(using_threads));

	return memnew(PhysicsSystem2DWrapMT(physics_system_2d, using_threads));
}

void initialize_physics_planar_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}
	PhysicsSystem2DManager::get_singleton()->register_system("OrblitPhysics2D", callable_mp_static(_createOrblitPhysics2DCallback));
	PhysicsSystem2DManager::get_singleton()->set_default_system("OrblitPhysics2D");
}

void uninitialize_physics_planar_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}
}
