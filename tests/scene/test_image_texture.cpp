/**************************************************************************/
/*  test_image_texture.cpp                                                */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_image_texture)

#include "core/io/image.h"
#include "scene/resources/image_texture.h"
#include "tests/test_utils.h"

namespace TestImageTexture {

// [Stage] in a test case name enables initializing a mock render server,
// which ImageTexture is dependent on.
TEST_CASE("[Stage][ImageTexture] constructor") {
	Ref<ImageTexture> image_texture = memnew(ImageTexture);
	CHECK(image_texture->get_width() == 0);
	CHECK(image_texture->get_height() == 0);
	CHECK(image_texture->get_format() == 0);
	CHECK(image_texture->has_alpha() == false);
	CHECK(image_texture->get_image() == Ref<Image>());
}

TEST_CASE("[Stage][ImageTexture] create_from_image") {
	Ref<Image> image = memnew(Image(16, 8, true, Image::FORMAT_RGBA8));
	Ref<ImageTexture> image_texture = ImageTexture::create_from_image(image);
	CHECK(image_texture->get_width() == 16);
	CHECK(image_texture->get_height() == 8);
	CHECK(image_texture->get_format() == Image::FORMAT_RGBA8);
	CHECK(image_texture->has_alpha() == true);
	CHECK(image_texture->get_rid().is_valid() == true);
}

TEST_CASE("[Stage][ImageTexture] set_image") {
	Ref<ImageTexture> image_texture = memnew(ImageTexture);
	Ref<Image> image = memnew(Image(8, 4, false, Image::FORMAT_RGB8));
	image_texture->set_image(image);
	CHECK(image_texture->get_width() == 8);
	CHECK(image_texture->get_height() == 4);
	CHECK(image_texture->get_format() == Image::FORMAT_RGB8);
	CHECK(image_texture->has_alpha() == false);
	CHECK(image_texture->get_width() == image_texture->get_image()->get_width());
	CHECK(image_texture->get_height() == image_texture->get_image()->get_height());
	CHECK(image_texture->get_format() == image_texture->get_image()->get_format());
}

TEST_CASE("[Stage][ImageTexture] set_size_override") {
	Ref<Image> image = memnew(Image(16, 8, false, Image::FORMAT_RGB8));
	Ref<ImageTexture> image_texture = ImageTexture::create_from_image(image);
	CHECK(image_texture->get_width() == 16);
	CHECK(image_texture->get_height() == 8);
	image_texture->set_size_override(Size2i(32, 16));
	CHECK(image_texture->get_width() == 32);
	CHECK(image_texture->get_height() == 16);
}

TEST_CASE("[Stage][ImageTexture] is_pixel_opaque") {
	Ref<Image> image = memnew(Image(8, 8, false, Image::FORMAT_RGBA8));
	image->set_pixel(0, 0, Color(0.0, 0.0, 0.0, 0.0)); // not opaque
	image->set_pixel(0, 1, Color(0.0, 0.0, 0.0, 0.09)); // not opaque
	image->set_pixel(0, 2, Color(0.0, 0.0, 0.0, 0.5)); // opaque
	image->set_pixel(0, 3, Color(0.0, 0.0, 0.0, 0.9)); // opaque
	image->set_pixel(0, 4, Color(0.0, 0.0, 0.0, 1.0)); // opaque

	Ref<ImageTexture> image_texture = ImageTexture::create_from_image(image);
	CHECK(image_texture->is_pixel_opaque(0, 0) == false);
	CHECK(image_texture->is_pixel_opaque(0, 1) == false);
	CHECK(image_texture->is_pixel_opaque(0, 2) == true);
	CHECK(image_texture->is_pixel_opaque(0, 3) == true);
	CHECK(image_texture->is_pixel_opaque(0, 4) == true);
}

TEST_CASE("[Stage][ImageTexture] set_path") {
	Ref<ImageTexture> image_texture = memnew(ImageTexture);
	String path = TestUtils::get_data_path("images/icon.png");
	image_texture->set_path(path, true);
	CHECK(image_texture->get_path() == path);
}

} // namespace TestImageTexture
