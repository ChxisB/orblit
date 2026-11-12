/**************************************************************************/
/*  test_height_map_shape_spatial.cpp                                          */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_height_map_shape_spatial)

#ifndef PHYSICS_SPATIAL_DISABLED

#include "core/io/image.h"
#include "scene/resources/spatial/height_map_shape_spatial.h"
#include "tests/test_tools.h"

#include "modules/modules_enabled.gen.h" // For the physics_spatial module.

#ifdef MODULE_PHYSICS_SPATIAL_ENABLED
#include "modules/physics_spatial/shape_spatial.h"
#endif

namespace TestHeightMapShape3D {

TEST_CASE("[Stage][HeightMapShape3D] Constructor") {
	Ref<HeightMapShape3D> height_map_shape = memnew(HeightMapShape3D);
	CHECK(height_map_shape->get_map_width() == 2);
	CHECK(height_map_shape->get_map_depth() == 2);
	CHECK(height_map_shape->get_map_data().size() == 4);
	CHECK(height_map_shape->get_min_height() == 0.0);
	CHECK(height_map_shape->get_max_height() == 0.0);
}

TEST_CASE("[Stage][HeightMapShape3D] set_map_width and get_map_width") {
	Ref<HeightMapShape3D> height_map_shape = memnew(HeightMapShape3D);
	height_map_shape->set_map_width(10);
	CHECK(height_map_shape->get_map_width() == 10);
}

TEST_CASE("[Stage][HeightMapShape3D] set_map_depth and get_map_depth") {
	Ref<HeightMapShape3D> height_map_shape = memnew(HeightMapShape3D);
	height_map_shape->set_map_depth(15);
	CHECK(height_map_shape->get_map_depth() == 15);
}

TEST_CASE("[Stage][HeightMapShape3D] set_map_data and get_map_data") {
	Ref<HeightMapShape3D> height_map_shape = memnew(HeightMapShape3D);
	Vector<real_t> map_data;
	map_data.push_back(1.0);
	map_data.push_back(2.0);
	height_map_shape->set_map_data(map_data);
	CHECK(height_map_shape->get_map_data().size() == 4.0);
	CHECK(height_map_shape->get_map_data()[0] == 0.0);
	CHECK(height_map_shape->get_map_data()[1] == 0.0);
}

TEST_CASE("[Stage][HeightMapShape3D] get_min_height") {
	Ref<HeightMapShape3D> height_map_shape = memnew(HeightMapShape3D);
	height_map_shape->set_map_width(3);
	height_map_shape->set_map_depth(1);
	height_map_shape->set_map_data(Vector<real_t>{ 1.0, 2.0, 0.5 });
	CHECK(height_map_shape->get_min_height() == 0.5);
}

TEST_CASE("[Stage][HeightMapShape3D] get_max_height") {
	Ref<HeightMapShape3D> height_map_shape = memnew(HeightMapShape3D);
	height_map_shape->set_map_width(3);
	height_map_shape->set_map_depth(1);
	height_map_shape->set_map_data(Vector<real_t>{ 1.0, 2.0, 0.5 });
	CHECK(height_map_shape->get_max_height() == 2.0);
}

TEST_CASE("[Stage][HeightMapShape3D] update_map_data_from_image") {
	// Create a HeightMapShape3D instance.
	Ref<HeightMapShape3D> height_map_shape = memnew(HeightMapShape3D);

	// Create a mock image with FORMAT_R8 and set its data.
	Vector<uint8_t> image_data;
	image_data.push_back(0);
	image_data.push_back(128);
	image_data.push_back(255);
	image_data.push_back(64);

	Ref<Image> image = memnew(Image);
	image->set_data(2, 2, false, Image::FORMAT_R8, image_data);

	height_map_shape->update_map_data_from_image(image, 0.0, 10.0);

	// Check the map data.
	Vector<real_t> expected_map_data = { 0.0, 5.0, 10.0, 2.5 };
	Vector<real_t> actual_map_data = height_map_shape->get_map_data();
	real_t tolerance = 0.1;

	for (int i = 0; i < expected_map_data.size(); ++i) {
		CHECK(Math::abs(actual_map_data[i] - expected_map_data[i]) < tolerance);
	}

	// Check the min and max heights.
	CHECK(height_map_shape->get_min_height() == 0.0);
	CHECK(height_map_shape->get_max_height() == 10.0);
}

#ifdef MODULE_PHYSICS_SPATIAL_ENABLED
namespace {
struct CullCount {
	int faces = 0;
	int non_finite = 0;
};

bool count_face(void *p_userdata, OrblitShape3D *p_face) {
	CullCount *count = static_cast<CullCount *>(p_userdata);
	const OrblitFaceShape3D *face = static_cast<const OrblitFaceShape3D *>(p_face);
	count->faces++;
	if (!face->normal.is_finite()) {
		count->non_finite++;
	}
	for (int i = 0; i < 3; i++) {
		if (!face->get_vertex(i).is_finite()) {
			count->non_finite++;
		}
	}
	return false;
}
} // namespace

TEST_CASE("[Stage][HeightMapShape3D] A NaN hole is skipped when culling") {
	Vector<real_t> heights;
	heights.resize(9);
	heights.fill(0.0);
	heights.write[4] = Math::NaN; // The center vertex is a hole.

	Dictionary data;
	data["width"] = 3;
	data["depth"] = 3;
	data["heights"] = heights;
	data["min_height"] = 0.0;
	data["max_height"] = 0.0;

	OrblitHeightMapShape3D shape;
	shape.set_data(data);

	ErrorDetector detector;
	CullCount count;
	shape.cull(AABB(Vector3(-2, -1, -2), Vector3(4, 2, 4)), count_face, &count, false);

	CHECK_FALSE(detector.has_error);
	CHECK(count.non_finite == 0);
	// Four cells, two triangles each. The center vertex touches all four cells.
	// Cells (0,0) and (1,1) lose one triangle. Cells (1,0) and (0,1) lose both.
	CHECK(count.faces == 2);
}

TEST_CASE("[Stage][HeightMapShape3D] Heights bound the shape when no range is given") {
	Vector<real_t> heights;
	heights.push_back(1.0);
	heights.push_back(2.0);
	heights.push_back(Math::NaN); // A hole does not count.
	heights.push_back(4.0);

	Dictionary data;
	data["width"] = 2;
	data["depth"] = 2;
	data["heights"] = heights;

	OrblitHeightMapShape3D shape;
	shape.set_data(data);

	const AABB aabb = shape.get_aabb();
	CHECK(aabb.position.y == doctest::Approx(1.0));
	CHECK(aabb.size.y == doctest::Approx(3.0));
}
#endif // MODULE_PHYSICS_SPATIAL_ENABLED

} // namespace TestHeightMapShape3D

#endif // PHYSICS_SPATIAL_DISABLED
