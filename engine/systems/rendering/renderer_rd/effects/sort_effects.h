/**************************************************************************/
/*  sort_effects.h                                                        */

#pragma once

#include "systems/rendering/renderer_rd/shaders/effects/sort.glsl.gen.h"

namespace RendererRD {

class SortEffects {
private:
	enum SortMode {
		SORT_MODE_BLOCK,
		SORT_MODE_STEP,
		SORT_MODE_INNER,
		SORT_MODE_MAX
	};

	struct PushConstant {
		uint32_t total_elements;
		uint32_t pad[3];
		int32_t job_params[4];
	};

	SortShaderRD shader;
	RID shader_version;
	RID pipelines[SORT_MODE_MAX];

protected:
public:
	SortEffects();
	~SortEffects();

	void sort_buffer(RID p_uniform_set, int p_size);
};

} // namespace RendererRD
