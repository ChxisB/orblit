/**************************************************************************/
/*  roughness_limiter.cpp                                                 */

#include "roughness_limiter.h"

#include "systems/rendering/renderer_rd/storage_rd/material_storage.h"
#include "systems/rendering/renderer_rd/uniform_set_cache_rd.h"

using namespace RendererRD;

RoughnessLimiter::RoughnessLimiter() {
	// Initialize roughness limiter
	Vector<String> shader_modes;
	shader_modes.push_back("");

	shader.initialize(shader_modes);

	shader_version = shader.version_create();

	pipeline = RD::get_singleton()->compute_pipeline_create(shader.version_get_shader(shader_version, 0));
}

RoughnessLimiter::~RoughnessLimiter() {
	shader.version_free(shader_version);
}

void RoughnessLimiter::roughness_limit(RID p_source_normal, RID p_roughness, const Size2i &p_size, float p_curve) {
	UniformSetCacheRD *uniform_set_cache = UniformSetCacheRD::get_singleton();
	ERR_FAIL_NULL(uniform_set_cache);
	MaterialStorage *material_storage = MaterialStorage::get_singleton();
	ERR_FAIL_NULL(material_storage);

	push_constant.screen_size[0] = p_size.x;
	push_constant.screen_size[1] = p_size.y;
	push_constant.curve = p_curve;

	RID default_sampler = material_storage->sampler_rd_get_default(RSE::CANVAS_ITEM_TEXTURE_FILTER_LINEAR, RSE::CANVAS_ITEM_TEXTURE_REPEAT_DISABLED);
	RID rl_shader = shader.version_get_shader(shader_version, 0);

	RD::Uniform u_source_normal(RD::UNIFORM_TYPE_SAMPLER_WITH_TEXTURE, 0, Vector<RID>({ default_sampler, p_source_normal }));
	RD::Uniform u_roughness(RD::UNIFORM_TYPE_IMAGE, 0, Vector<RID>({ p_roughness }));

	RD::ComputeListID compute_list = RD::get_singleton()->compute_list_begin();
	RD::get_singleton()->compute_list_bind_compute_pipeline(compute_list, pipeline);
	RD::get_singleton()->compute_list_bind_uniform_set(compute_list, uniform_set_cache->get_cache(rl_shader, 0, u_source_normal), 0);
	RD::get_singleton()->compute_list_bind_uniform_set(compute_list, uniform_set_cache->get_cache(rl_shader, 1, u_roughness), 1);

	RD::get_singleton()->compute_list_set_push_constant(compute_list, &push_constant, sizeof(RoughnessLimiterPushConstant)); //not used but set anyway

	RD::get_singleton()->compute_list_dispatch_threads(compute_list, p_size.x, p_size.y, 1);

	RD::get_singleton()->compute_list_end();
}
