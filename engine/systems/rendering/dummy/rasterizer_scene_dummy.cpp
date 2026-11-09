/**************************************************************************/
/*  rasterizer_scene_dummy.cpp                                            */

#include "rasterizer_scene_dummy.h"

#include "core/io/image.h"
#include "core/variant/typed_array.h"
#include "systems/rendering/rendering_system_globals.h"

TypedArray<Image> RasterizerSceneDummy::bake_render_uv2(RID p_base, const TypedArray<RID> &p_material_overrides, const Size2i &p_image_size) {
	return TypedArray<Image>();
}

bool RasterizerSceneDummy::free(RID p_rid) {
	if (is_environment(p_rid)) {
		environment_free(p_rid);
		return true;
	} else if (is_compositor(p_rid)) {
		compositor_free(p_rid);
		return true;
	} else if (is_compositor_effect(p_rid)) {
		compositor_effect_free(p_rid);
		return true;
	} else if (RSG::camera_attributes->owns_camera_attributes(p_rid)) {
		RSG::camera_attributes->camera_attributes_free(p_rid);
		return true;
	} else {
		return false;
	}
}
