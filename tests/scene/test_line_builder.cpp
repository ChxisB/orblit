/**************************************************************************/
/*  test_line_builder.cpp                                                 */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_line_builder)

#include "scene/planar/line_builder.h"
#include "scene/resources/gradient.h"

namespace TestLineBuilder {

TEST_CASE("[LineBuilder] The ends use the gradient colors at offsets 0 and 1") {
	// The outer points lie outside the 0 to 1 range, so no point colour sits at either end.
	Ref<Gradient> gradient = memnew(Gradient);
	Vector<float> offsets;
	offsets.push_back(-1.0f);
	offsets.push_back(0.5f);
	offsets.push_back(2.0f);
	Vector<Color> colors;
	colors.push_back(Color(1, 0, 0));
	colors.push_back(Color(0, 1, 0));
	colors.push_back(Color(0, 0, 1));
	gradient->set_offsets(offsets);
	gradient->set_colors(colors);

	LineBuilder builder;
	builder.points.push_back(Vector2(0, 0));
	builder.points.push_back(Vector2(100, 0));
	builder.gradient = gradient.ptr();
	builder.build();

	REQUIRE(builder.colors.size() == 4);
	const Color start = gradient->get_color_at_offset(0.0f);
	const Color end = gradient->get_color_at_offset(1.0f);
	CHECK(builder.colors[0].is_equal_approx(start));
	CHECK(builder.colors[builder.colors.size() - 1].is_equal_approx(end));
}

TEST_CASE("[LineBuilder] A round end cap starts from the gradient color at offset 1") {
	Ref<Gradient> gradient = memnew(Gradient);
	Vector<float> offsets;
	offsets.push_back(0.0f);
	offsets.push_back(2.0f);
	Vector<Color> colors;
	colors.push_back(Color(1, 0, 0));
	colors.push_back(Color(0, 0, 1));
	gradient->set_offsets(offsets);
	gradient->set_colors(colors);

	LineBuilder builder;
	builder.points.push_back(Vector2(0, 0));
	builder.points.push_back(Vector2(100, 0));
	builder.gradient = gradient.ptr();
	builder.end_cap_mode = Line2D::LINE_CAP_ROUND;
	builder.build();

	const Color end = gradient->get_color_at_offset(1.0f);
	REQUIRE(builder.colors.size() > 4);
	CHECK(builder.colors[builder.colors.size() - 1].is_equal_approx(end));
}

} // namespace TestLineBuilder
