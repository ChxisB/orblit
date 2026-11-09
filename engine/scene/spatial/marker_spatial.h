/**************************************************************************/
/*  marker_spatial.h                                                           */

#pragma once

#include "scene/spatial/entity_spatial.h"

class Marker3D : public Entity3D {
	ORBLIT_CLASS(Marker3D, Entity3D);

	real_t gizmo_extents = 0.25;

protected:
	static void register_members();

public:
	void set_gizmo_extents(real_t p_extents);
	real_t get_gizmo_extents() const;

	Marker3D();
};
