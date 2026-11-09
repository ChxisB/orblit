/**************************************************************************/
/*  test_gradient_texture.cpp                                             */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_gradient_texture)

#include "scene/resources/gradient_texture.h"

namespace TestGradientTexture {

// [Stage] in a test case name enables initializing a mock render server,
// which ImageTexture is dependent on.
TEST_CASE("[Stage][GradientTexture1D] Create GradientTexture1D") {
	Ref<GradientTexture1D> gradient_texture = memnew(GradientTexture1D);

	Ref<Gradient> test_gradient = memnew(Gradient);
	gradient_texture->set_gradient(test_gradient);
	CHECK(gradient_texture->get_gradient() == test_gradient);

	gradient_texture->set_width(83);
	CHECK(gradient_texture->get_width() == 83);

	gradient_texture->set_use_hdr(true);
	CHECK(gradient_texture->is_using_hdr());
}

TEST_CASE("[Stage][GradientTexture2D] Create GradientTexture2D") {
	Ref<GradientTexture2D> gradient_texture = memnew(GradientTexture2D);

	Ref<Gradient> test_gradient = memnew(Gradient);
	gradient_texture->set_gradient(test_gradient);
	CHECK(gradient_texture->get_gradient() == test_gradient);

	gradient_texture->set_width(82);
	CHECK(gradient_texture->get_width() == 82);

	gradient_texture->set_height(81);
	CHECK(gradient_texture->get_height() == 81);

	gradient_texture->set_use_hdr(true);
	CHECK(gradient_texture->is_using_hdr());

	gradient_texture->set_fill(GradientTexture2D::Fill::FILL_SQUARE);
	CHECK(gradient_texture->get_fill() == GradientTexture2D::Fill::FILL_SQUARE);

	gradient_texture->set_fill_from(Vector2(0.2, 0.25));
	CHECK(gradient_texture->get_fill_from() == Vector2(0.2, 0.25));

	gradient_texture->set_fill_to(Vector2(0.35, 0.5));
	CHECK(gradient_texture->get_fill_to() == Vector2(0.35, 0.5));

	gradient_texture->set_repeat(GradientTexture2D::Repeat::REPEAT);
	CHECK(gradient_texture->get_repeat() == GradientTexture2D::Repeat::REPEAT);
}

} // namespace TestGradientTexture
