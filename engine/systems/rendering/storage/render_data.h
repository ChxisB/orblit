/**************************************************************************/
/*  render_data.h                                                         */

#pragma once

#include "core/object/object.h"
#include "systems/rendering/storage/render_scene_buffers.h"
#include "systems/rendering/storage/render_scene_data.h"

class RenderData : public Object {
	ORBLIT_CLASS(RenderData, Object);

protected:
	static void register_members();

public:
	virtual Ref<RenderSceneBuffers> get_render_scene_buffers() const = 0;
	virtual RenderSceneData *get_render_scene_data() const = 0;

	virtual RID get_environment() const = 0;
	virtual RID get_camera_attributes() const = 0;
};
