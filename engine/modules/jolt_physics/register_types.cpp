/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "jolt_globals.h"
#include "jolt_physics_system_spatial.h"
#include "jolt_project_settings.h"

#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "systems/physics_spatial/physics_system_spatial_manager.h"
#include "systems/physics_spatial/physics_system_spatial_wrap_mt.h"

PhysicsSystem3D *create_jolt_physics_system() {
#ifdef THREADS_ENABLED
	bool run_on_separate_thread = GLOBAL_GET("physics/spatial/run_on_separate_thread");
#else
	bool run_on_separate_thread = false;
#endif

	JoltPhysicsSystem3D *physics_system = memnew(JoltPhysicsSystem3D(run_on_separate_thread));

	return memnew(PhysicsSystem3DWrapMT(physics_system, run_on_separate_thread));
}

void initialize_jolt_physics_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}

	jolt_initialize();
	PhysicsSystem3DManager::get_singleton()->register_system(PhysicsSystem3DManager::JOLT_PHYSICS_NAME, callable_mp_static(&create_jolt_physics_system));
	JoltProjectSettings::register_settings();
}

void uninitialize_jolt_physics_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}

	jolt_deinitialize();
}
