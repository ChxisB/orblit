/**************************************************************************/
/*  roughness_limiter.h                                                   */

#pragma once

#include "systems/rendering/renderer_rd/shaders/effects/roughness_limiter.glsl.gen.h"

namespace RendererRD {

// Note, this logic is unused at the time of writing. It should be re-incorporated into the renderer at some point.

class RoughnessLimiter {
private:
	struct RoughnessLimiterPushConstant {
		int32_t screen_size[2];
		float curve;
		uint32_t pad;
	};

	RoughnessLimiterPushConstant push_constant;
	RoughnessLimiterShaderRD shader;
	RID shader_version;
	RID pipeline;

protected:
public:
	RoughnessLimiter();
	~RoughnessLimiter();

	void roughness_limit(RID p_source_normal, RID p_roughness, const Size2i &p_size, float p_curve);
};

} // namespace RendererRD
