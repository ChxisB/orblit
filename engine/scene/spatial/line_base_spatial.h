/**************************************************************************/
/*  line_base_spatial.h                                                        */

#pragma once

#include "scene/spatial/visual_instance_spatial.h"

class LineBase3D : public GeometryInstance3D {
	ORBLIT_CLASS(LineBase3D, GeometryInstance3D);

protected:
	static void register_members();

public:
	enum MeshAlignment {
		MESH_ALIGNMENT_LOCAL,
		MESH_ALIGNMENT_BILLBOARD,
		MESH_ALIGNMENT_MAX,
	};

	enum TilingMode {
		TILING_MODE_STRETCH,
		TILING_MODE_TILE,
		TILING_MAX,
	};

	enum MaterialMode {
		MATERIAL_MODE_MIX,
		MATERIAL_MODE_ADD,
		MATERIAL_MODE_CUSTOM,
		MATERIAL_MODE_MAX,
	};

	PackedStringArray get_configuration_warnings() const override;
};

VARIANT_ENUM_CAST(LineBase3D::TilingMode)
VARIANT_ENUM_CAST(LineBase3D::MeshAlignment)
VARIANT_ENUM_CAST(LineBase3D::MaterialMode)
