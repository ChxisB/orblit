/**************************************************************************/
/*  test_gltf_light.h                                                     */

#pragma once

#include "../extensions/gltf_light.h"

#include "scene/spatial/light_spatial.h"
#include "tests/test_macros.h"

namespace TestGltfLight {

TEST_CASE("[GLTF] A spot light with equal cone angles gets a finite attenuation") {
	Ref<GLTFLight> gltf_light;
	gltf_light.instantiate();
	gltf_light->set_light_type("spot");
	gltf_light->set_inner_cone_angle(0.09f);
	gltf_light->set_outer_cone_angle(0.09f);

	Light3D *light = gltf_light->to_node();
	REQUIRE(light != nullptr);
	CHECK(Math::is_finite(light->get_param(Light3D::PARAM_SPOT_ATTENUATION)));
	CHECK(light->get_param(Light3D::PARAM_SPOT_ATTENUATION) > 1000.0f);
	memdelete(light);
}

TEST_CASE("[GLTF] A spot light with no cone angle gets a finite attenuation") {
	Ref<GLTFLight> gltf_light;
	gltf_light.instantiate();
	gltf_light->set_light_type("spot");
	gltf_light->set_inner_cone_angle(0.0f);
	gltf_light->set_outer_cone_angle(0.0f);

	Light3D *light = gltf_light->to_node();
	REQUIRE(light != nullptr);
	CHECK(Math::is_finite(light->get_param(Light3D::PARAM_SPOT_ATTENUATION)));
	memdelete(light);
}

TEST_CASE("[GLTF] A spot light with a very hard edge exports distinct cone angles") {
	SpotLight3D *light = memnew(SpotLight3D);
	light->set_param(Light3D::PARAM_SPOT_ATTENUATION, 1.0e9f);

	const Ref<GLTFLight> gltf_light = GLTFLight::from_node(light);
	CHECK(gltf_light->get_inner_cone_angle() < gltf_light->get_outer_cone_angle());
	memdelete(light);
}

} // namespace TestGltfLight
