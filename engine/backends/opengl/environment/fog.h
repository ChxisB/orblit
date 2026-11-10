/**************************************************************************/
/*  fog.h                                                                 */

#pragma once

#ifdef GLES3_ENABLED

#include "systems/rendering/environment/renderer_fog.h"

namespace GLES3 {

class Fog : public RendererFog {
public:
	/* FOG VOLUMES */

	virtual RID fog_volume_allocate() override;
	virtual void fog_volume_initialize(RID p_rid) override;
	virtual void fog_volume_free(RID p_rid) override;

	virtual void fog_volume_set_shape(RID p_fog_volume, RSE::FogVolumeShape p_shape) override;
	virtual void fog_volume_set_size(RID p_fog_volume, const Vector3 &p_size) override;
	virtual void fog_volume_set_material(RID p_fog_volume, RID p_material) override;
	virtual AABB fog_volume_get_aabb(RID p_fog_volume) const override;
	virtual RSE::FogVolumeShape fog_volume_get_shape(RID p_fog_volume) const override;
};

} // namespace GLES3

#endif // GLES3_ENABLED
