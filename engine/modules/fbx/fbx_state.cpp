/**************************************************************************/
/*  fbx_state.cpp                                                         */

#include "fbx_state.h"

#include "core/object/class_registry.h"

void FBXState::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_allow_geometry_helper_nodes"), &FBXState::get_allow_geometry_helper_nodes);
	ClassRegistry::bind_method(D_METHOD("set_allow_geometry_helper_nodes", "allow"), &FBXState::set_allow_geometry_helper_nodes);

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "allow_geometry_helper_nodes"), "set_allow_geometry_helper_nodes", "get_allow_geometry_helper_nodes");
}

bool FBXState::get_allow_geometry_helper_nodes() {
	return allow_geometry_helper_nodes;
}

void FBXState::set_allow_geometry_helper_nodes(bool p_allow_geometry_helper_nodes) {
	allow_geometry_helper_nodes = p_allow_geometry_helper_nodes;
}
