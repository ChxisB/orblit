/**************************************************************************/
/*  fog.cpp                                                               */

#include "fog.h"

#ifdef GLES3_ENABLED

#include "core/math/aabb.h"
#include "core/containers/rid.h"

using namespace GLES3;

/* FOG */

RID Fog::fog_volume_allocate() {
	return RID();
}

void Fog::fog_volume_initialize(RID p_rid) {
}

void Fog::fog_volume_free(RID p_rid) {
}

void Fog::fog_volume_set_shape(RID p_fog_volume, RSE::FogVolumeShape p_shape) {
}

void Fog::fog_volume_set_size(RID p_fog_volume, const Vector3 &p_size) {
}

void Fog::fog_volume_set_material(RID p_fog_volume, RID p_material) {
}

AABB Fog::fog_volume_get_aabb(RID p_fog_volume) const {
	return AABB();
}

RSE::FogVolumeShape Fog::fog_volume_get_shape(RID p_fog_volume) const {
	return RSE::FOG_VOLUME_SHAPE_BOX;
}

#endif // GLES3_ENABLED
