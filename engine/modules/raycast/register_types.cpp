/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "lightmap_raycaster_embree.h"
#include "raycast_occlusion_cull.h"
#include "static_raycaster_embree.h"

RaycastOcclusionCull *raycast_occlusion_cull = nullptr;

void initialize_raycast_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

#ifdef TOOLS_ENABLED
	LightmapRaycasterEmbree::make_default_raycaster();
	StaticRaycasterEmbree::make_default_raycaster();
#endif
	raycast_occlusion_cull = memnew(RaycastOcclusionCull);
}

void uninitialize_raycast_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	memdelete(raycast_occlusion_cull);
#ifdef TOOLS_ENABLED
	StaticRaycasterEmbree::free();
#endif
}
