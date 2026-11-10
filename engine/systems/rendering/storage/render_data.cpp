/**************************************************************************/
/*  render_data.cpp                                                       */

#include "render_data.h"

#include "core/object/class_registry.h"

void RenderData::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_render_scene_buffers"), &RenderData::get_render_scene_buffers);
	ClassRegistry::bind_method(D_METHOD("get_render_scene_data"), &RenderData::get_render_scene_data);
	ClassRegistry::bind_method(D_METHOD("get_environment"), &RenderData::get_environment);
	ClassRegistry::bind_method(D_METHOD("get_camera_attributes"), &RenderData::get_camera_attributes);
}
