/**************************************************************************/
/*  test_path_planar.cpp                                                      */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_path_planar)

#include "scene/planar/path_planar.h"

namespace TestPath2D {

TEST_CASE("[Stage][Path2D] Initialization") {
	SUBCASE("Path should be empty right after initialization") {
		Path2D *test_path = memnew(Path2D);
		CHECK(test_path->get_curve().is_null());
		memdelete(test_path);
	}
}

TEST_CASE("[Stage][Path2D] Curve setter and getter") {
	Path2D *test_path = memnew(Path2D);
	const Ref<Curve2D> &curve = memnew(Curve2D);

	SUBCASE("Curve passed to the class should remain the same") {
		test_path->set_curve(curve);
		CHECK(test_path->get_curve() == curve);
	}
	SUBCASE("Curve passed many times to the class should remain the same") {
		test_path->set_curve(curve);
		test_path->set_curve(curve);
		test_path->set_curve(curve);
		CHECK(test_path->get_curve() == curve);
	}
	SUBCASE("Curve rewrite testing") {
		const Ref<Curve2D> &curve1 = memnew(Curve2D);
		const Ref<Curve2D> &curve2 = memnew(Curve2D);

		test_path->set_curve(curve1);
		test_path->set_curve(curve2);
		CHECK_MESSAGE(test_path->get_curve() != curve1,
				"After rewrite, second curve should be in class");
		CHECK_MESSAGE(test_path->get_curve() == curve2,
				"After rewrite, second curve should be in class");
	}

	SUBCASE("Assign same curve to two paths") {
		Path2D *path2 = memnew(Path2D);

		test_path->set_curve(curve);
		path2->set_curve(curve);
		CHECK_MESSAGE(test_path->get_curve() == path2->get_curve(),
				"Both paths have the same curve.");
		memdelete(path2);
	}

	SUBCASE("Swapping curves between two paths") {
		Path2D *path2 = memnew(Path2D);
		const Ref<Curve2D> &curve1 = memnew(Curve2D);
		const Ref<Curve2D> &curve2 = memnew(Curve2D);

		test_path->set_curve(curve1);
		path2->set_curve(curve2);
		CHECK(test_path->get_curve() == curve1);
		CHECK(path2->get_curve() == curve2);

		// Do the swap
		Ref<Curve2D> temp = test_path->get_curve();
		test_path->set_curve(path2->get_curve());
		path2->set_curve(temp);

		CHECK(test_path->get_curve() == curve2);
		CHECK(path2->get_curve() == curve1);
		memdelete(path2);
	}

	memdelete(test_path);
}

} // namespace TestPath2D
