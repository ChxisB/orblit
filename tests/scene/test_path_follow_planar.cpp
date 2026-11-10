/**************************************************************************/
/*  test_path_follow_planar.cpp                                               */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_path_follow_planar)

#include "scene/planar/path_planar.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestPathFollow2D {

bool is_equal_approx(const Vector2 &p_a, const Vector2 &p_b) {
	const real_t tolerance = 0.001;
	return Math::is_equal_approx(p_a.x, p_b.x, tolerance) &&
			Math::is_equal_approx(p_a.y, p_b.y, tolerance);
}

TEST_CASE("[Stage][PathFollow2D] Sampling with progress ratio") {
	Ref<Curve2D> curve = memnew(Curve2D);
	curve->set_bake_interval(1);
	curve->add_point(Vector2(0, 0));
	curve->add_point(Vector2(100, 0));
	curve->add_point(Vector2(100, 100));
	curve->add_point(Vector2(0, 100));
	curve->add_point(Vector2(0, 0));
	Path2D *path = memnew(Path2D);
	path->set_curve(curve);
	PathFollow2D *path_follow_2d = memnew(PathFollow2D);
	path_follow_2d->set_loop(false);
	path->add_child(path_follow_2d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_2d->set_progress_ratio(0);
	CHECK(is_equal_approx(Vector2(0, 0), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress_ratio(0.125);
	CHECK(is_equal_approx(Vector2(50, 0), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress_ratio(0.25);
	CHECK(is_equal_approx(Vector2(100, 0), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress_ratio(0.375);
	CHECK(is_equal_approx(Vector2(100, 50), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress_ratio(0.5);
	CHECK(is_equal_approx(Vector2(100, 100), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress_ratio(0.625);
	CHECK(is_equal_approx(Vector2(50, 100), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress_ratio(0.75);
	CHECK(is_equal_approx(Vector2(0, 100), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress_ratio(0.875);
	CHECK(is_equal_approx(Vector2(0, 50), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress_ratio(1);
	CHECK(is_equal_approx(Vector2(0, 0), path_follow_2d->get_transform().get_origin()));

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow2D] Sampling with progress") {
	Ref<Curve2D> curve = memnew(Curve2D);
	curve->set_bake_interval(1);
	curve->add_point(Vector2(0, 0));
	curve->add_point(Vector2(100, 0));
	curve->add_point(Vector2(100, 100));
	curve->add_point(Vector2(0, 100));
	curve->add_point(Vector2(0, 0));
	Path2D *path = memnew(Path2D);
	path->set_curve(curve);
	PathFollow2D *path_follow_2d = memnew(PathFollow2D);
	path_follow_2d->set_loop(false);
	path->add_child(path_follow_2d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_2d->set_progress(0);
	CHECK(is_equal_approx(Vector2(0, 0), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress(50);
	CHECK(is_equal_approx(Vector2(50, 0), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress(100);
	CHECK(is_equal_approx(Vector2(100, 0), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress(150);
	CHECK(is_equal_approx(Vector2(100, 50), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress(200);
	CHECK(is_equal_approx(Vector2(100, 100), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress(250);
	CHECK(is_equal_approx(Vector2(50, 100), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress(300);
	CHECK(is_equal_approx(Vector2(0, 100), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress(350);
	CHECK(is_equal_approx(Vector2(0, 50), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_progress(400);
	CHECK(is_equal_approx(Vector2(0, 0), path_follow_2d->get_transform().get_origin()));

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow2D] Removal of a point in curve") {
	Ref<Curve2D> curve = memnew(Curve2D);
	curve->add_point(Vector2(0, 0));
	curve->add_point(Vector2(100, 0));
	curve->add_point(Vector2(100, 100));
	Path2D *path = memnew(Path2D);
	path->set_curve(curve);
	PathFollow2D *path_follow_2d = memnew(PathFollow2D);
	path->add_child(path_follow_2d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_2d->set_progress_ratio(0.5);
	CHECK(is_equal_approx(Vector2(100, 0), path_follow_2d->get_transform().get_origin()));

	curve->remove_point(1);

	path_follow_2d->set_progress_ratio(0.5);
	CHECK_MESSAGE(
			is_equal_approx(Vector2(50, 50), path_follow_2d->get_transform().get_origin()),
			"Path follow's position should be updated after removing a point from the curve");

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow2D] Setting h_offset and v_offset") {
	Ref<Curve2D> curve = memnew(Curve2D);
	curve->add_point(Vector2(0, 0));
	curve->add_point(Vector2(100, 0));
	Path2D *path = memnew(Path2D);
	path->set_curve(curve);
	PathFollow2D *path_follow_2d = memnew(PathFollow2D);
	path->add_child(path_follow_2d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_2d->set_progress_ratio(0.5);
	CHECK(is_equal_approx(Vector2(50, 0), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_h_offset(25);
	CHECK(is_equal_approx(Vector2(75, 0), path_follow_2d->get_transform().get_origin()));

	path_follow_2d->set_v_offset(25);
	CHECK(is_equal_approx(Vector2(75, 25), path_follow_2d->get_transform().get_origin()));

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow2D] Progress ratio out of range") {
	Ref<Curve2D> curve = memnew(Curve2D);
	curve->add_point(Vector2(0, 0));
	curve->add_point(Vector2(100, 0));
	Path2D *path = memnew(Path2D);
	path->set_curve(curve);
	PathFollow2D *path_follow_2d = memnew(PathFollow2D);
	path->add_child(path_follow_2d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_2d->set_loop(true);

	path_follow_2d->set_progress_ratio(-0.3);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_2d->get_progress_ratio(), (real_t)0.7),
			"Progress Ratio should loop back from the end in the opposite direction");

	path_follow_2d->set_progress_ratio(1.3);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_2d->get_progress_ratio(), (real_t)0.3),
			"Progress Ratio should loop back from the end in the opposite direction");

	path_follow_2d->set_loop(false);

	path_follow_2d->set_progress_ratio(-0.3);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_2d->get_progress_ratio(), 0),
			"Progress Ratio should be clamped at 0");

	path_follow_2d->set_progress_ratio(1.3);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_2d->get_progress_ratio(), 1),
			"Progress Ratio should be clamped at 1");

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow2D] Progress out of range") {
	Ref<Curve2D> curve = memnew(Curve2D);
	curve->add_point(Vector2(0, 0));
	curve->add_point(Vector2(100, 0));
	Path2D *path = memnew(Path2D);
	path->set_curve(curve);
	PathFollow2D *path_follow_2d = memnew(PathFollow2D);
	path->add_child(path_follow_2d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_2d->set_loop(true);

	path_follow_2d->set_progress(-50);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_2d->get_progress(), 50),
			"Progress should loop back from the end in the opposite direction");

	path_follow_2d->set_progress(150);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_2d->get_progress(), 50),
			"Progress should loop back from the end in the opposite direction");

	path_follow_2d->set_loop(false);

	path_follow_2d->set_progress(-50);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_2d->get_progress(), 0),
			"Progress should be clamped at 0");

	path_follow_2d->set_progress(150);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_2d->get_progress(), 100),
			"Progress should be clamped at 1");

	memdelete(path);
}

} // namespace TestPathFollow2D
