/**************************************************************************/
/*  fsr.h                                                                 */

#pragma once

#include "systems/rendering/renderer_rd/effects/spatial_upscaler.h"
#include "systems/rendering/renderer_rd/pipeline_deferred_rd.h"
#include "systems/rendering/renderer_rd/shaders/effects/fsr_upscale.glsl.gen.h"
#include "systems/rendering/renderer_rd/storage_rd/render_scene_buffers_rd.h"

namespace RendererRD {

class FSR : public SpatialUpscaler {
public:
	FSR();
	~FSR();

	virtual const Span<char> get_label() const final { return "FSR 1.0 Upscale"; }
	virtual void ensure_context(Ref<RenderSceneBuffersRD> p_render_buffers) final {}
	virtual void process(Ref<RenderSceneBuffersRD> p_render_buffers, RID p_source_rd_texture, RID p_destination_texture) final;

private:
	enum FSRShaderVariant {
		FSR_SHADER_VARIANT_NORMAL,
		FSR_SHADER_VARIANT_FALLBACK,
	};

	enum FSRUpscalePass {
		FSR_UPSCALE_PASS_EASU = 0,
		FSR_UPSCALE_PASS_RCAS = 1
	};

	struct FSRUpscalePushConstant {
		float resolution_width;
		float resolution_height;
		float upscaled_width;
		float upscaled_height;
		float sharpness;
		int pass;
		int pad[2];
	};

	FsrUpscaleShaderRD fsr_shader;
	RID shader_version;
	PipelineDeferredRD pipeline;
};

} // namespace RendererRD
