/**************************************************************************/
/*  test_triangle_mesh.cpp                                                */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_triangle_mesh)

#ifndef SPATIAL_DISABLED

#include "core/math/triangle_mesh.h"
#include "scene/resources/spatial/primitive_meshes.h"

namespace TestTriangleMesh {

TEST_CASE("[Stage][TriangleMesh] BVH creation and intersection") {
	Ref<BoxMesh> box_mesh;
	box_mesh.instantiate();

	const Vector<Face3> faces = box_mesh->get_faces();

	Ref<TriangleMesh> triangle_mesh;
	triangle_mesh.instantiate();
	CHECK(triangle_mesh->create_from_faces(Variant(faces)));

	const Vector3 begin = Vector3(0.0, 2.0, 0.0);
	const Vector3 end = Vector3(0.0, -2.0, 0.0);

	{
		Vector3 point;
		Vector3 normal;
		int32_t *surf_index = nullptr;
		int32_t face_index = -1;
		const bool has_result = triangle_mesh->intersect_segment(begin, end, point, normal, surf_index, &face_index);
		CHECK(has_result);
		CHECK(point.is_equal_approx(Vector3(0.0, 0.5, 0.0)));
		CHECK(normal.is_equal_approx(Vector3(0.0, 1.0, 0.0)));
		CHECK(surf_index == nullptr);
		REQUIRE(face_index != -1);
		CHECK(face_index == 8);
	}

	{
		Vector3 dir = begin.direction_to(end);
		Vector3 point;
		Vector3 normal;
		int32_t *surf_index = nullptr;
		int32_t face_index = -1;
		const bool has_result = triangle_mesh->intersect_ray(begin, dir, point, normal, surf_index, &face_index);
		CHECK(has_result);
		CHECK(point.is_equal_approx(Vector3(0.0, 0.5, 0.0)));
		CHECK(normal.is_equal_approx(Vector3(0.0, 1.0, 0.0)));
		CHECK(surf_index == nullptr);
		REQUIRE(face_index != -1);
		CHECK(face_index == 8);
	}
}

} // namespace TestTriangleMesh

#endif // SPATIAL_DISABLED
