/**************************************************************************/
/*  spatial_upscaler.h                                                    */

#pragma once

#include "core/object/ref_counted.h"

class RenderSceneBuffersRD;

class SpatialUpscaler {
public:
	virtual const Span<char> get_label() const = 0;
	virtual void ensure_context(Ref<RenderSceneBuffersRD> p_render_buffers) = 0;
	virtual void process(Ref<RenderSceneBuffersRD> p_render_buffers, RID p_source_rd_texture, RID p_destination_texture) = 0;

	SpatialUpscaler() = default;
	virtual ~SpatialUpscaler() = default;
};
