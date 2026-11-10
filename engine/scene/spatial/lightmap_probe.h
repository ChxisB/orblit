/**************************************************************************/
/*  lightmap_probe.h                                                      */

#pragma once

#include "scene/spatial/entity_spatial.h"

class LightmapProbe : public Entity3D {
	ORBLIT_CLASS(LightmapProbe, Entity3D)
public:
	LightmapProbe();
};
