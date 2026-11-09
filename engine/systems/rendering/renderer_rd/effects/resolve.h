/**************************************************************************/
/*  resolve.h                                                             */

#pragma once

#include "systems/rendering/renderer_rd/pipeline_cache_rd.h"
#include "systems/rendering/renderer_rd/shaders/effects/resolve.glsl.gen.h"
#include "systems/rendering/renderer_rd/shaders/effects/resolve_raster.glsl.gen.h"

namespace RendererRD {

class Resolve {
private:
	bool prefer_raster_effects;

	struct ResolvePushConstant {
		int32_t screen_size[2];
		int32_t samples;
		uint32_t pad;
	};

	enum ResolveMode {
		RESOLVE_MODE_GI,
		RESOLVE_MODE_GI_VOXEL_GI,
		RESOLVE_MODE_DEPTH,
		RESOLVE_MODE_MAX
	};

	struct ResolveShader {
		ResolvePushConstant push_constant;
		ResolveShaderRD shader;
		RID shader_version;
		RID pipelines[RESOLVE_MODE_MAX]; //3 quality levels
	} resolve;

	struct ResolveRasterShader {
		ResolvePushConstant push_constant;
		ResolveRasterShaderRD shader;
		RID shader_version;
		PipelineCacheRD pipeline;
	} resolve_raster;

public:
	Resolve(bool p_prefer_raster_effects);
	~Resolve();

	void resolve_gi(RID p_source_depth, RID p_source_normal_roughness, RID p_source_voxel_gi, RID p_dest_depth, RID p_dest_normal_roughness, RID p_dest_voxel_gi, Vector2i p_screen_size, int p_samples);
	void resolve_depth(RID p_source_depth, RID p_dest_depth, Vector2i p_screen_size, int p_samples);
	void resolve_depth_raster(RID p_source_rd_texture, RID p_dest_framebuffer, int p_samples);
};

} // namespace RendererRD
