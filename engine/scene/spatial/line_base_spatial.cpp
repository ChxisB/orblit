/**************************************************************************/
/*  line_base_spatial.cpp                                                      */

#include "line_base_spatial.h"

#include "core/object/class_registry.h"

PackedStringArray LineBase3D::get_configuration_warnings() const {
	PackedStringArray warnings = VisualInstance3D::get_configuration_warnings();
	EXPERIMENTAL_NODE_WARNING;
	return warnings;
}

void LineBase3D::register_members() {
	BIND_ENUM_CONSTANT(MESH_ALIGNMENT_LOCAL);
	BIND_ENUM_CONSTANT(MESH_ALIGNMENT_BILLBOARD);
	BIND_ENUM_CONSTANT(MESH_ALIGNMENT_MAX);

	BIND_ENUM_CONSTANT(TILING_MODE_STRETCH);
	BIND_ENUM_CONSTANT(TILING_MODE_TILE);
	BIND_ENUM_CONSTANT(TILING_MAX);

	BIND_ENUM_CONSTANT(MATERIAL_MODE_MIX);
	BIND_ENUM_CONSTANT(MATERIAL_MODE_ADD);
	BIND_ENUM_CONSTANT(MATERIAL_MODE_CUSTOM);
	BIND_ENUM_CONSTANT(MATERIAL_MODE_MAX);
}
