/**************************************************************************/
/*  test_triangle2.cpp                                                    */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_triangle2)

#include "modules/modules_enabled.gen.h" // For navigation 2D.

#ifdef MODULE_NAVIGATION_PLANAR_ENABLED

#include "modules/navigation_planar/triangle2.h"

namespace TestTriangle2 {

TEST_SUITE("[Triangle2]") {
	TEST_CASE("[Triangle2] Test get_area") {
		const Vector2 p0(5.0, 5.0);
		const Vector2 p1(6.0, 7.0);
		const Vector2 p2(7.0, 6.0);

		CHECK_EQ(Triangle2(p0, p1, p2).get_area(), doctest::Approx(1.5));
		CHECK_EQ(Triangle2(p0, p2, p1).get_area(), doctest::Approx(1.5));

		CHECK_EQ(Triangle2(p0, p2, p2).get_area(), doctest::Approx(0.0));
		CHECK_EQ(Triangle2(p0, p1, p1).get_area(), doctest::Approx(0.0));
	}

	TEST_CASE("[Triangle2] Test get_closest_point_to") {
		const Vector2 p0(5.0, 5.0);
		const Vector2 p1(6.0, 7.0);
		const Vector2 p2(7.0, 6.0);

		const Vector2 p3(0.0, 0.0);
		const Vector2 p4(6.0, 6.5);

		const Triangle2 t(p0, p1, p2);

		CHECK(t.get_closest_point_to(p0).is_equal_approx(p0));
		CHECK(t.get_closest_point_to(p1).is_equal_approx(p1));
		CHECK(t.get_closest_point_to(p2).is_equal_approx(p2));

		CHECK(t.get_closest_point_to(p3).is_equal_approx(p0));

		CHECK(t.get_closest_point_to(p4).is_equal_approx(p4));
	}
}

} // namespace TestTriangle2

#endif // MODULE_NAVIGATION_PLANAR_ENABLED
