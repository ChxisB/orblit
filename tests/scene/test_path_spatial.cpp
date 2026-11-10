/**************************************************************************/
/*  test_path_spatial.cpp                                                      */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_path_spatial)

#ifndef SPATIAL_DISABLED

#include "scene/spatial/path_spatial.h"

namespace TestPath3D {

TEST_CASE("[Path3D] Initialization") {
	SUBCASE("Path should be empty right after initialization") {
		Path3D *test_path = memnew(Path3D);
		CHECK(test_path->get_curve().is_null());
		memdelete(test_path);
	}
}

TEST_CASE("[Path3D] Curve setter and getter") {
	SUBCASE("Curve passed to the class should remain the same") {
		Path3D *test_path = memnew(Path3D);
		const Ref<Curve3D> &curve = memnew(Curve3D);

		test_path->set_curve(curve);
		CHECK(test_path->get_curve() == curve);
		memdelete(test_path);
	}
	SUBCASE("Curve passed many times to the class should remain the same") {
		Path3D *test_path = memnew(Path3D);
		const Ref<Curve3D> &curve = memnew(Curve3D);

		test_path->set_curve(curve);
		test_path->set_curve(curve);
		test_path->set_curve(curve);
		CHECK(test_path->get_curve() == curve);
		memdelete(test_path);
	}
	SUBCASE("Curve rewrite testing") {
		Path3D *test_path = memnew(Path3D);
		const Ref<Curve3D> &curve1 = memnew(Curve3D);
		const Ref<Curve3D> &curve2 = memnew(Curve3D);

		test_path->set_curve(curve1);
		test_path->set_curve(curve2);
		CHECK_MESSAGE(test_path->get_curve() != curve1,
				"After rewrite, second curve should be in class");
		CHECK_MESSAGE(test_path->get_curve() == curve2,
				"After rewrite, second curve should be in class");
		memdelete(test_path);
	}
}

} // namespace TestPath3D

#endif // SPATIAL_DISABLED
