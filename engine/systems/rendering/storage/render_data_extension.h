/**************************************************************************/
/*  render_data_extension.h                                               */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "systems/rendering/storage/render_data.h"
#include "systems/rendering/storage/render_scene_buffers.h"
#include "systems/rendering/storage/render_scene_data.h"

class RenderSceneBuffersExtension : public RenderSceneBuffers {
	ORBLIT_CLASS(RenderSceneBuffersExtension, RenderSceneBuffers);

protected:
	static void register_members();

	ORBLIT_VIRTUAL1(_configure, const RenderSceneBuffersConfiguration *)
	ORBLIT_VIRTUAL1(_set_fsr_sharpness, float)
	ORBLIT_VIRTUAL1(_set_texture_mipmap_bias, float)
	ORBLIT_VIRTUAL1(_set_anisotropic_filtering_level, int)
	ORBLIT_VIRTUAL1(_set_use_debanding, bool)

public:
	virtual ~RenderSceneBuffersExtension() {}

	virtual void configure(const RenderSceneBuffersConfiguration *p_config) override;

	virtual void set_fsr_sharpness(float p_fsr_sharpness) override;
	virtual void set_texture_mipmap_bias(float p_texture_mipmap_bias) override;
	virtual void set_anisotropic_filtering_level(RSE::ViewportAnisotropicFiltering p_anisotropic_filtering_level) override;
	virtual void set_use_debanding(bool p_use_debanding) override;
};

class RenderSceneDataExtension : public RenderSceneData {
	ORBLIT_CLASS(RenderSceneDataExtension, RenderSceneData);

protected:
	static void register_members();

public:
	virtual Transform3D get_cam_transform() const override;
	virtual Projection get_cam_projection() const override;

	virtual uint32_t get_view_count() const override;
	virtual Vector3 get_view_eye_offset(uint32_t p_view) const override;
	virtual Projection get_view_projection(uint32_t p_view) const override;

	virtual RID get_uniform_buffer() const override;

	ORBLIT_VIRTUAL0RC(Transform3D, _get_cam_transform)
	ORBLIT_VIRTUAL0RC(Projection, _get_cam_projection)

	ORBLIT_VIRTUAL0RC(uint32_t, _get_view_count)
	ORBLIT_VIRTUAL1RC(Vector3, _get_view_eye_offset, uint32_t)
	ORBLIT_VIRTUAL1RC(Projection, _get_view_projection, uint32_t)

	ORBLIT_VIRTUAL0RC(RID, _get_uniform_buffer)
};

class RenderDataExtension : public RenderData {
	ORBLIT_CLASS(RenderDataExtension, RenderData);

protected:
	static void register_members();

	virtual Ref<RenderSceneBuffers> get_render_scene_buffers() const override;
	virtual RenderSceneData *get_render_scene_data() const override;

	virtual RID get_environment() const override;
	virtual RID get_camera_attributes() const override;

	ORBLIT_VIRTUAL0RC(Ref<RenderSceneBuffers>, _get_render_scene_buffers)
	ORBLIT_VIRTUAL0RC(RenderSceneData *, _get_render_scene_data)
	ORBLIT_VIRTUAL0RC(RID, _get_environment)
	ORBLIT_VIRTUAL0RC(RID, _get_camera_attributes)
};
