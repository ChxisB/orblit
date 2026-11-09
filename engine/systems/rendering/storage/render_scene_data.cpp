/**************************************************************************/
/*  render_scene_data.cpp                                                 */

#include "render_scene_data.h"

#include "core/object/class_registry.h"

void RenderSceneData::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_cam_transform"), &RenderSceneData::get_cam_transform);
	ClassRegistry::bind_method(D_METHOD("get_cam_projection"), &RenderSceneData::get_cam_projection);

	ClassRegistry::bind_method(D_METHOD("get_view_count"), &RenderSceneData::get_view_count);
	ClassRegistry::bind_method(D_METHOD("get_view_eye_offset", "view"), &RenderSceneData::get_view_eye_offset);
	ClassRegistry::bind_method(D_METHOD("get_view_projection", "view"), &RenderSceneData::get_view_projection);

	ClassRegistry::bind_method(D_METHOD("get_uniform_buffer"), &RenderSceneData::get_uniform_buffer);
}
