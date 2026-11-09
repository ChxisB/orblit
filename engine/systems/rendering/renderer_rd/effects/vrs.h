/**************************************************************************/
/*  vrs.h                                                                 */

#pragma once

#include "systems/rendering/renderer_rd/pipeline_cache_rd.h"
#include "systems/rendering/renderer_rd/shaders/effects/vrs.glsl.gen.h"

namespace RendererRD {

class VRS {
private:
	enum VRSMode {
		VRS_DEFAULT,
		VRS_MULTIVIEW,
		VRS_RG,
		VRS_RG_MULTIVIEW,
		VRS_MAX,
	};

	struct VRSPushConstant {
		float max_texel_factor; // 4x8, 8x4 and 8x8 are only available on some GPUs.
		float res1;
		float res2;
		float res3;
	};

	struct VRSShader {
		// VRSPushConstant push_constant;
		VrsShaderRD shader;
		RID shader_version;
		PipelineCacheRD pipelines[VRS_MAX];
	} vrs_shader;

public:
	VRS();
	~VRS();

	void copy_vrs(RID p_source_rd_texture, RID p_dest_framebuffer, bool p_multiview = false);

	Size2i get_vrs_texture_size(const Size2i p_base_size) const;
	void update_vrs_texture(RID p_vrs_fb, RID p_render_target);
};

} // namespace RendererRD
