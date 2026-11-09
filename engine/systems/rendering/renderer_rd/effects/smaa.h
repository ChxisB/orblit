/**************************************************************************/
/*  smaa.h                                                                */

#pragma once

#include "systems/rendering/renderer_rd/pipeline_cache_rd.h"
#include "systems/rendering/renderer_rd/shaders/effects/smaa_blending.glsl.gen.h"
#include "systems/rendering/renderer_rd/shaders/effects/smaa_edge_detection.glsl.gen.h"
#include "systems/rendering/renderer_rd/shaders/effects/smaa_weight_calculation.glsl.gen.h"
#include "systems/rendering/renderer_rd/storage_rd/render_scene_buffers_rd.h"

#define RB_SCOPE_SMAA SNAME("rb_smaa")

#define RB_EDGES SNAME("edges")
#define RB_BLEND SNAME("blend")
#define RB_STENCIL SNAME("stencil")

namespace RendererRD {

class SMAA {
private:
	enum SMAAMode {
		SMAA_EDGE_DETECTION_COLOR,
		SMAA_WEIGHT_FULL,
		SMAA_BLENDING,
		SMAA_MAX,
	};

	struct SMAAEdgePushConstant {
		float inv_size[2];
		float threshold;
		float pad;
	};

	struct SMAAWeightPushConstant {
		float inv_size[2];
		uint32_t size[2];

		float subsample_indices[4];
	};

	struct SMAABlendPushConstant {
		float inv_size[2];
		uint32_t use_debanding;
		float pad;
	};

	enum SMAABlendFlags {
		SMAA_BLEND_FLAG_USE_8_BIT_DEBANDING = (1 << 0),
		SMAA_BLEND_FLAG_USE_10_BIT_DEBANDING = (1 << 1),
	};

	struct SMAAEffect {
		SMAAEdgePushConstant edge_push_constant;
		SmaaEdgeDetectionShaderRD edge_shader;
		RID edge_shader_version;

		SMAAWeightPushConstant weight_push_constant;
		SmaaWeightCalculationShaderRD weight_shader;
		RID weight_shader_version;

		SMAABlendPushConstant blend_push_constant;
		SmaaBlendingShaderRD blend_shader;
		RID blend_shader_version;

		RID search_tex;
		RID area_tex;

		RD::DataFormat stencil_format;

		PipelineCacheRD pipelines[SMAA_MAX];
	} smaa;

	float edge_detection_threshold;

public:
	SMAA();
	~SMAA();

	void allocate_render_targets(Ref<RenderSceneBuffersRD> p_render_buffers);
	void process(Ref<RenderSceneBuffersRD> p_render_buffers, RID p_source_color, RID p_dst_framebuffer, bool p_use_debanding);
};

} // namespace RendererRD
