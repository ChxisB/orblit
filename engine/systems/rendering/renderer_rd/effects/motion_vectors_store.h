/**************************************************************************/
/*  motion_vectors_store.h                                                */

#pragma once

#include "systems/rendering/renderer_rd/shaders/effects/motion_vectors_store.glsl.gen.h"
#include "systems/rendering/renderer_rd/storage_rd/render_scene_buffers_rd.h"

namespace RendererRD {
class MotionVectorsStore {
	struct PushConstant {
		float reprojection_matrix[16];
		float resolution[2];
		uint32_t pad[2];
	};

	MotionVectorsStoreShaderRD motion_shader;
	RID shader_version;
	RID pipeline;

public:
	MotionVectorsStore();
	~MotionVectorsStore();

	void process(Ref<RenderSceneBuffersRD> p_render_buffers,
			const Projection &p_current_projection, const Transform3D &p_current_transform,
			const Projection &p_previous_projection, const Transform3D &p_previous_transform);
};
} //namespace RendererRD
