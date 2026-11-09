/**************************************************************************/
/*  test_gltf_emissive.h                                                  */

#pragma once

#include "test_gltf.h"

#ifdef TOOLS_ENABLED

namespace TestGltf {

TEST_CASE("[Stage][Entity] GLTF emissiveTexture without emissiveFactor uses white emission") {
	init("gltf_emissive_no_factor", "res://");

	Entity *loaded = gltf_import("res://emissive_no_factor.gltf");
	CHECK_MESSAGE(loaded != nullptr, "Failed to load GLB.");

	MeshInstance3D *mesh = Object::cast_to<MeshInstance3D>(loaded->find_child("Cube", true, true));
	CHECK_MESSAGE(mesh != nullptr, "Mesh not found.");

	Ref<StandardMaterial3D> mat = mesh->get_active_material(0);
	CHECK_MESSAGE(mat.is_valid(), "Material not found.");

	// Emission should be enabled.
	CHECK(mat->get_feature(BaseMaterial3D::FEATURE_EMISSION));

	// Emission operator should be MULTIPLY per glTF spec.
	CHECK(mat->get_emission_operator() == BaseMaterial3D::EMISSION_OP_MULTIPLY);

	// Without emissiveFactor, emission color should be WHITE, not BLACK.
	Color c = mat->get_emission();
	CHECK_MESSAGE(c.r > 0.9f, "Emission red should be ~1.0 when emissiveFactor is absent.");
	CHECK_MESSAGE(c.g > 0.9f, "Emission green should be ~1.0 when emissiveFactor is absent.");
	CHECK_MESSAGE(c.b > 0.9f, "Emission blue should be ~1.0 when emissiveFactor is absent.");

	memdelete(loaded);
}

} // namespace TestGltf

#endif // TOOLS_ENABLED
