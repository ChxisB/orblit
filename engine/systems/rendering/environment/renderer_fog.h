/**************************************************************************/
/*  renderer_fog.h                                                        */

#pragma once

#include "systems/rendering/rendering_system_enums.h"

class RID;
struct AABB;
struct Vector3;

class RendererFog {
public:
	virtual ~RendererFog() {}

	/* FOG VOLUMES */

	virtual RID fog_volume_allocate() = 0;
	virtual void fog_volume_initialize(RID p_rid) = 0;
	virtual void fog_volume_free(RID p_rid) = 0;

	virtual void fog_volume_set_shape(RID p_fog_volume, RSE::FogVolumeShape p_shape) = 0;
	virtual void fog_volume_set_size(RID p_fog_volume, const Vector3 &p_size) = 0;
	virtual void fog_volume_set_material(RID p_fog_volume, RID p_material) = 0;
	virtual AABB fog_volume_get_aabb(RID p_fog_volume) const = 0;
	virtual RSE::FogVolumeShape fog_volume_get_shape(RID p_fog_volume) const = 0;
};
