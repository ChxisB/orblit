/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "csg_shape.h"

#include "core/object/class_registry.h"

#ifdef TOOLS_ENABLED
#include "editor/csg_gizmos.h"
#endif

void initialize_csg_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_ABSTRACT_CLASS(CSGShape3D);
		ORBLIT_REGISTER_ABSTRACT_CLASS(CSGPrimitive3D);
		ORBLIT_REGISTER_CLASS(CSGMesh3D);
		ORBLIT_REGISTER_CLASS(CSGSphere3D);
		ORBLIT_REGISTER_CLASS(CSGBox3D);
		ORBLIT_REGISTER_CLASS(CSGCylinder3D);
		ORBLIT_REGISTER_CLASS(CSGTorus3D);
		ORBLIT_REGISTER_CLASS(CSGPolygon3D);
		ORBLIT_REGISTER_CLASS(CSGCombiner3D);
#ifndef NAVIGATION_SPATIAL_DISABLED
		CSGShape3D::navmesh_parse_init();
#endif // NAVIGATION_SPATIAL_DISABLED
	}
#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		EditorPlugins::add_by_type<EditorPluginCSG>();
	}
#endif
}

void uninitialize_csg_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
