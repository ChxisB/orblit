/**************************************************************************/
/*  test_gltf_accessor.h                                                  */

#pragma once

#include "../gltf_state.h"
#include "../structures/gltf_accessor.h"

#include "tests/test_macros.h"

namespace TestGltfAccessor {

TEST_CASE("[GLTF] An accessor decodes into one packed array per element") {
	Ref<GLTFState> state;
	state.instantiate();
	PackedVector3Array input;
	input.push_back(Vector3(1, 2, 3));
	input.push_back(Vector3(4, 5, 6));
	const GLTFAccessorIndex index = GLTFAccessor::encode_new_accessor_from_vector3s(state, input);
	REQUIRE(index >= 0);
	const Ref<GLTFAccessor> accessor = state->get_accessors()[index];

	SUBCASE("Float64") {
		const Array decoded = accessor->decode_as_variants(state, Variant::PACKED_FLOAT64_ARRAY);
		REQUIRE(decoded.size() == 2);
		CHECK(PackedFloat64Array(decoded[0]) == PackedFloat64Array({ 1, 2, 3 }));
		CHECK(PackedFloat64Array(decoded[1]) == PackedFloat64Array({ 4, 5, 6 }));
	}

	SUBCASE("Float32") {
		const Array decoded = accessor->decode_as_variants(state, Variant::PACKED_FLOAT32_ARRAY);
		REQUIRE(decoded.size() == 2);
		CHECK(PackedFloat32Array(decoded[1]) == PackedFloat32Array({ 4, 5, 6 }));
	}

	SUBCASE("Int32") {
		const Array decoded = accessor->decode_as_variants(state, Variant::PACKED_INT32_ARRAY);
		REQUIRE(decoded.size() == 2);
		CHECK(PackedInt32Array(decoded[1]) == PackedInt32Array({ 4, 5, 6 }));
	}

	SUBCASE("Int64") {
		const Array decoded = accessor->decode_as_variants(state, Variant::PACKED_INT64_ARRAY);
		REQUIRE(decoded.size() == 2);
		CHECK(PackedInt64Array(decoded[1]) == PackedInt64Array({ 4, 5, 6 }));
	}

	SUBCASE("Byte") {
		const Array decoded = accessor->decode_as_variants(state, Variant::PACKED_BYTE_ARRAY);
		REQUIRE(decoded.size() == 2);
		CHECK(PackedByteArray(decoded[1]) == PackedByteArray({ 4, 5, 6 }));
	}
}

} // namespace TestGltfAccessor
