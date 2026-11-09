/**************************************************************************/
/*  test_image_texture_spatial.cpp                                             */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_image_texture_spatial)

#include "core/io/image.h"
#include "scene/resources/image_texture.h"
#include "tests/test_utils.h"

namespace TestImageTexture3D {

// [Stage] in a test case name enables initializing a mock render server,
// which ImageTexture3D is dependent on.
TEST_CASE("[Stage][ImageTexture3D] Constructor") {
	Ref<ImageTexture3D> image_texture_3d = memnew(ImageTexture3D);
	CHECK(image_texture_3d->get_format() == Image::FORMAT_L8);
	CHECK(image_texture_3d->get_width() == 1);
	CHECK(image_texture_3d->get_height() == 1);
	CHECK(image_texture_3d->get_depth() == 1);
	CHECK(image_texture_3d->has_mipmaps() == false);
}

TEST_CASE("[Stage][ImageTexture3D] get_format") {
	Ref<ImageTexture3D> image_texture_3d = memnew(ImageTexture3D);
	CHECK(image_texture_3d->get_format() == Image::FORMAT_L8);
}

TEST_CASE("[Stage][ImageTexture3D] get_width") {
	Ref<ImageTexture3D> image_texture_3d = memnew(ImageTexture3D);
	CHECK(image_texture_3d->get_width() == 1);
}

TEST_CASE("[Stage][ImageTexture3D] get_height") {
	Ref<ImageTexture3D> image_texture_3d = memnew(ImageTexture3D);
	CHECK(image_texture_3d->get_height() == 1);
}

TEST_CASE("[Stage][ImageTexture3D] get_depth") {
	Ref<ImageTexture3D> image_texture_3d = memnew(ImageTexture3D);
	CHECK(image_texture_3d->get_depth() == 1);
}

TEST_CASE("[Stage][ImageTexture3D] has_mipmaps") {
	const Vector<Ref<Image>> images = { memnew(Image(8, 8, false, Image::FORMAT_RGBA8)), memnew(Image(8, 8, false, Image::FORMAT_RGBA8)) };
	Ref<ImageTexture3D> image_texture_3d = memnew(ImageTexture3D);
	CHECK(image_texture_3d->has_mipmaps() == false); // No mipmaps.
	image_texture_3d->create(Image::FORMAT_RGBA8, 2, 2, 2, true, images);
	CHECK(image_texture_3d->has_mipmaps() == true); // Mipmaps.
}

TEST_CASE("[Stage][ImageTexture3D] create") {
	const Vector<Ref<Image>> images = { memnew(Image(8, 8, false, Image::FORMAT_RGBA8)), memnew(Image(8, 8, false, Image::FORMAT_RGBA8)) };
	Ref<ImageTexture3D> image_texture_3d = memnew(ImageTexture3D);
	CHECK(image_texture_3d->create(Image::FORMAT_RGBA8, 2, 2, 2, true, images) == OK); // Run create and check return value simultaneously.
	CHECK(image_texture_3d->get_format() == Image::FORMAT_RGBA8);
	CHECK(image_texture_3d->get_width() == 2);
	CHECK(image_texture_3d->get_height() == 2);
	CHECK(image_texture_3d->get_depth() == 2);
	CHECK(image_texture_3d->has_mipmaps() == true);
}

TEST_CASE("[Stage][ImageTexture3D] set_path") {
	Ref<ImageTexture3D> image_texture_3d = memnew(ImageTexture3D);
	String path = TestUtils::get_data_path("images/icon.png");
	image_texture_3d->set_path(path, true);
	CHECK(image_texture_3d->get_path() == path);
}

} // namespace TestImageTexture3D
