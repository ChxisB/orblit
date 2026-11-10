/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "physics_system_spatial.h"

#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "systems/physics_spatial/physics_system_spatial.h"
#include "systems/physics_spatial/physics_system_spatial_manager.h"
#include "systems/physics_spatial/physics_system_spatial_wrap_mt.h"

static PhysicsSystem3D *_createOrblitPhysics3DCallback() {
#ifdef THREADS_ENABLED
	bool using_threads = GLOBAL_GET("physics/spatial/run_on_separate_thread");
#else
	bool using_threads = false;
#endif

	PhysicsSystem3D *physics_system_3d = memnew(OrblitPhysicsSystem3D(using_threads));

	return memnew(PhysicsSystem3DWrapMT(physics_system_3d, using_threads));
}

void initialize_physics_spatial_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}
	PhysicsSystem3DManager::get_singleton()->register_system(PhysicsSystem3DManager::ORBLIT_PHYSICS_3D_NAME, callable_mp_static(_createOrblitPhysics3DCallback));
	PhysicsSystem3DManager::get_singleton()->set_default_system(PhysicsSystem3DManager::ORBLIT_PHYSICS_3D_NAME);
}

void uninitialize_physics_spatial_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}
}
