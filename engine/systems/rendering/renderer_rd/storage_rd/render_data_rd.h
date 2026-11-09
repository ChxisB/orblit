/**************************************************************************/
/*  render_data_rd.h                                                      */

#pragma once

#include "systems/rendering/renderer_rd/storage_rd/render_scene_buffers_rd.h"
#include "systems/rendering/renderer_rd/storage_rd/render_scene_data_rd.h"
#include "systems/rendering/rendering_system_types.h"
#include "systems/rendering/storage/render_data.h"

class RenderDataRD : public RenderData {
	ORBLIT_CLASS(RenderDataRD, RenderData);

public:
	// Access methods to expose data externally
	virtual Ref<RenderSceneBuffers> get_render_scene_buffers() const override { return render_buffers; }
	virtual RenderSceneData *get_render_scene_data() const override { return scene_data; }

	virtual RID get_environment() const override { return environment; }
	virtual RID get_camera_attributes() const override { return camera_attributes; }

	// Members are publicly accessible within the render engine.
	Ref<RenderSceneBuffersRD> render_buffers;
	RenderSceneDataRD *scene_data = nullptr;

	const PagedArray<RenderGeometryInstance *> *instances = nullptr;
	const PagedArray<RID> *lights = nullptr;
	const PagedArray<RID> *reflection_probes = nullptr;
	const PagedArray<RID> *voxel_gi_instances = nullptr;
	const PagedArray<RID> *decals = nullptr;
	const PagedArray<RID> *lightmaps = nullptr;
	const PagedArray<RID> *fog_volumes = nullptr;
	RID environment;
	RID camera_attributes;
	RID compositor;
	RID shadow_atlas;
	RID occluder_debug_tex;
	RID reflection_atlas;
	RID reflection_probe;
	int reflection_probe_pass = 0;

	RID cluster_buffer;
	uint32_t cluster_size = 0;
	uint32_t cluster_max_elements = 0;

	uint32_t directional_light_count = 0;
	bool directional_light_soft_shadows = false;

	bool lightmap_bicubic_filter = false;

	float window_output_max_value = 1.0;

	RenderingSystemTypes::RenderInfo *render_info = nullptr;

	/* Viewport data */
	bool transparent_bg = false;
	Rect2i render_region;

	/* Shadow data */
	const RendererSceneRender::RenderShadowData *render_shadows = nullptr;
	int render_shadow_count = 0;

	LocalVector<int> cube_shadows;
	LocalVector<int> shadows;
	LocalVector<int> directional_shadows;
	LocalVector<int> contact_shadows;

	/* GI info */
	const RendererSceneRender::RenderSDFGIData *render_sdfgi_regions = nullptr;
	int render_sdfgi_region_count = 0;
	const RendererSceneRender::RenderSDFGIUpdateData *sdfgi_update_data = nullptr;

	uint32_t voxel_gi_count = 0;
};
