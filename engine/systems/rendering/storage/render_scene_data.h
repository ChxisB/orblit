/**************************************************************************/
/*  render_scene_data.h                                                   */

#pragma once

#include "core/object/object.h"

class RenderSceneData : public Object {
	ORBLIT_CLASS(RenderSceneData, Object);

protected:
	static void register_members();

public:
	virtual Transform3D get_cam_transform() const = 0;
	virtual Projection get_cam_projection() const = 0;

	virtual uint32_t get_view_count() const = 0;
	virtual Vector3 get_view_eye_offset(uint32_t p_view) const = 0;
	virtual Projection get_view_projection(uint32_t p_view) const = 0;

	virtual RID get_uniform_buffer() const = 0;
};
