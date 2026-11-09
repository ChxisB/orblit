/**************************************************************************/
/*  taa.h                                                                 */

#pragma once

#include "systems/rendering/renderer_rd/shaders/effects/taa_resolve.glsl.gen.h"
#include "systems/rendering/renderer_rd/storage_rd/render_scene_buffers_rd.h"

namespace RendererRD {

class TAA {
public:
	TAA();
	~TAA();

	void process(Ref<RenderSceneBuffersRD> p_render_buffers, RD::DataFormat p_format, float p_z_near, float p_z_far);

private:
	struct TAAResolvePushConstant {
		float resolution_width;
		float resolution_height;
		float disocclusion_threshold;
		float variance_dynamic;
	};

	TaaResolveShaderRD taa_shader;
	RID shader_version;
	RID pipeline;

	void resolve(RID p_frame, RID p_temp, RID p_depth, RID p_velocity, RID p_prev_velocity, RID p_history, Size2 p_resolution, float p_z_near, float p_z_far);
};

} // namespace RendererRD
