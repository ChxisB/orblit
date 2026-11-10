/**************************************************************************/
/*  test_height_map_shape_spatial.cpp                                          */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_height_map_shape_spatial)

#ifndef PHYSICS_SPATIAL_DISABLED

#include "core/io/image.h"
#include "scene/resources/spatial/height_map_shape_spatial.h"

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

} // namespace TestHeightMapShape3D

#endif // PHYSICS_SPATIAL_DISABLED
