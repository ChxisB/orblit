/**************************************************************************/
/*  test_path_follow_spatial.cpp                                               */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_path_follow_spatial)

#ifndef SPATIAL_DISABLED

#include "scene/spatial/path_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestPathFollow3D {

bool is_equal_approx(const Vector3 &p_a, const Vector3 &p_b) {
	const real_t tolerance = 0.001;
	return Math::is_equal_approx(p_a.x, p_b.x, tolerance) &&
			Math::is_equal_approx(p_a.y, p_b.y, tolerance) &&
			Math::is_equal_approx(p_a.z, p_b.z, tolerance);
}

TEST_CASE("[Stage][PathFollow3D] Sampling with progress ratio") {
	Ref<Curve3D> curve;
	curve.instantiate();
	curve->add_point(Vector3(0, 0, 0));
	curve->add_point(Vector3(100, 0, 0));
	curve->add_point(Vector3(100, 100, 0));
	curve->add_point(Vector3(100, 100, 100));
	curve->add_point(Vector3(100, 0, 100));
	Path3D *path = memnew(Path3D);
	path->set_curve(curve);
	PathFollow3D *path_follow_3d = memnew(PathFollow3D);
	path_follow_3d->set_loop(false);
	path->add_child(path_follow_3d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_3d->set_progress_ratio(0);
	CHECK(is_equal_approx(Vector3(0, 0, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress_ratio(0.125);
	CHECK(is_equal_approx(Vector3(50, 0, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress_ratio(0.25);
	CHECK(is_equal_approx(Vector3(100, 0, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress_ratio(0.375);
	CHECK(is_equal_approx(Vector3(100, 50, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress_ratio(0.5);
	CHECK(is_equal_approx(Vector3(100, 100, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress_ratio(0.625);
	CHECK(is_equal_approx(Vector3(100, 100, 50), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress_ratio(0.75);
	CHECK(is_equal_approx(Vector3(100, 100, 100), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress_ratio(0.875);
	CHECK(is_equal_approx(Vector3(100, 50, 100), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress_ratio(1);
	CHECK(is_equal_approx(Vector3(100, 0, 100), path_follow_3d->get_transform().get_origin()));

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow3D] Sampling with progress") {
	Ref<Curve3D> curve;
	curve.instantiate();
	curve->add_point(Vector3(0, 0, 0));
	curve->add_point(Vector3(100, 0, 0));
	curve->add_point(Vector3(100, 100, 0));
	curve->add_point(Vector3(100, 100, 100));
	curve->add_point(Vector3(100, 0, 100));
	Path3D *path = memnew(Path3D);
	path->set_curve(curve);
	PathFollow3D *path_follow_3d = memnew(PathFollow3D);
	path_follow_3d->set_loop(false);
	path->add_child(path_follow_3d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_3d->set_progress(0);
	CHECK(is_equal_approx(Vector3(0, 0, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress(50);
	CHECK(is_equal_approx(Vector3(50, 0, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress(100);
	CHECK(is_equal_approx(Vector3(100, 0, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress(150);
	CHECK(is_equal_approx(Vector3(100, 50, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress(200);
	CHECK(is_equal_approx(Vector3(100, 100, 0), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress(250);
	CHECK(is_equal_approx(Vector3(100, 100, 50), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress(300);
	CHECK(is_equal_approx(Vector3(100, 100, 100), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress(350);
	CHECK(is_equal_approx(Vector3(100, 50, 100), path_follow_3d->get_transform().get_origin()));

	path_follow_3d->set_progress(400);
	CHECK(is_equal_approx(Vector3(100, 0, 100), path_follow_3d->get_transform().get_origin()));

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow3D] Removal of a point in curve") {
	Ref<Curve3D> curve;
	curve.instantiate();
	curve->add_point(Vector3(0, 0, 0));
	curve->add_point(Vector3(100, 0, 0));
	curve->add_point(Vector3(100, 100, 0));
	Path3D *path = memnew(Path3D);
	path->set_curve(curve);
	PathFollow3D *path_follow_3d = memnew(PathFollow3D);
	path->add_child(path_follow_3d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_3d->set_progress_ratio(0.5);
	CHECK(is_equal_approx(Vector3(100, 0, 0), path_follow_3d->get_transform().get_origin()));

	curve->remove_point(1);

	path_follow_3d->set_progress_ratio(0.5);
	CHECK_MESSAGE(
			is_equal_approx(Vector3(50, 50, 0), path_follow_3d->get_transform().get_origin()),
			"Path follow's position should be updated after removing a point from the curve");

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow3D] Progress ratio out of range") {
	Ref<Curve3D> curve;
	curve.instantiate();
	curve->add_point(Vector3(0, 0, 0));
	curve->add_point(Vector3(100, 0, 0));
	Path3D *path = memnew(Path3D);
	path->set_curve(curve);
	PathFollow3D *path_follow_3d = memnew(PathFollow3D);
	path->add_child(path_follow_3d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_3d->set_loop(true);

	path_follow_3d->set_progress_ratio(-0.3);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_3d->get_progress_ratio(), (real_t)0.7),
			"Progress Ratio should loop back from the end in the opposite direction");

	path_follow_3d->set_progress_ratio(1.3);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_3d->get_progress_ratio(), (real_t)0.3),
			"Progress Ratio should loop back from the end in the opposite direction");

	path_follow_3d->set_loop(false);

	path_follow_3d->set_progress_ratio(-0.3);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_3d->get_progress_ratio(), 0),
			"Progress Ratio should be clamped at 0");

	path_follow_3d->set_progress_ratio(1.3);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_3d->get_progress_ratio(), 1),
			"Progress Ratio should be clamped at 1");

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow3D] Progress out of range") {
	Ref<Curve3D> curve;
	curve.instantiate();
	curve->add_point(Vector3(0, 0, 0));
	curve->add_point(Vector3(100, 0, 0));
	Path3D *path = memnew(Path3D);
	path->set_curve(curve);
	PathFollow3D *path_follow_3d = memnew(PathFollow3D);
	path->add_child(path_follow_3d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_3d->set_loop(true);

	path_follow_3d->set_progress(-50);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_3d->get_progress(), 50),
			"Progress should loop back from the end in the opposite direction");

	path_follow_3d->set_progress(150);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_3d->get_progress(), 50),
			"Progress should loop back from the end in the opposite direction");

	path_follow_3d->set_loop(false);

	path_follow_3d->set_progress(-50);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_3d->get_progress(), 0),
			"Progress should be clamped at 0");

	path_follow_3d->set_progress(150);
	CHECK_MESSAGE(
			Math::is_equal_approx(path_follow_3d->get_progress(), 100),
			"Progress should be clamped at max value of curve");

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow3D] Calculate forward vector") {
	const real_t dist_cube_100 = 100 * Math::sqrt(3.0);
	Ref<Curve3D> curve;
	curve.instantiate();
	curve->add_point(Vector3(0, 0, 0));
	curve->add_point(Vector3(100, 0, 0));
	curve->add_point(Vector3(200, 100, -100));
	curve->add_point(Vector3(200, 100, 200));
	curve->add_point(Vector3(100, 0, 100));
	curve->add_point(Vector3(0, 0, 100));
	Path3D *path = memnew(Path3D);
	path->set_curve(curve);
	PathFollow3D *path_follow_3d = memnew(PathFollow3D);
	path->add_child(path_follow_3d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_3d->set_loop(false);
	path_follow_3d->set_rotation_mode(PathFollow3D::RotationMode::ROTATION_ORIENTED);

	path_follow_3d->set_progress(-50);
	CHECK(is_equal_approx(Vector3(-1, 0, 0), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(0);
	CHECK(is_equal_approx(Vector3(-1, 0, 0), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(50);
	CHECK(is_equal_approx(Vector3(-1, 0, 0), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(100);
	CHECK(is_equal_approx(Vector3(-1, 0, 0), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(100 + dist_cube_100 / 2);
	CHECK(is_equal_approx(Vector3(-0.577348, -0.577348, 0.577348), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(100 + dist_cube_100 - 0.01);
	CHECK(is_equal_approx(Vector3(-0.577348, -0.577348, 0.577348), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(250 + dist_cube_100);
	CHECK(is_equal_approx(Vector3(0, 0, -1), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(400 + dist_cube_100 - 0.01);
	CHECK(is_equal_approx(Vector3(0, 0, -1), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(400 + 1.5 * dist_cube_100);
	CHECK(is_equal_approx(Vector3(0.577348, 0.577348, 0.577348), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(400 + 2 * dist_cube_100 - 0.01);
	CHECK(is_equal_approx(Vector3(0.577348, 0.577348, 0.577348), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress(500 + 2 * dist_cube_100);
	CHECK(is_equal_approx(Vector3(1, 0, 0), path_follow_3d->get_transform().get_basis().get_column(2)));

	memdelete(path);
}

TEST_CASE("[Stage][PathFollow3D] Calculate forward vector with degenerate curves") {
	Ref<Curve3D> curve;
	curve.instantiate();
	curve->add_point(Vector3(0, 0, 1), Vector3(), Vector3(1, 0, 0));
	curve->add_point(Vector3(1, 0, 0), Vector3(0, 0, 0), Vector3(0, 0, 0));
	curve->add_point(Vector3(0, 0, -1), Vector3(1, 0, 0), Vector3(-1, 0, 0));
	curve->add_point(Vector3(-1, 0, 0), Vector3(0, 0, 0), Vector3(0, 0, 0));
	curve->add_point(Vector3(0, 0, 1), Vector3(-1, 0, 0), Vector3());
	Path3D *path = memnew(Path3D);
	path->set_curve(curve);
	PathFollow3D *path_follow_3d = memnew(PathFollow3D);
	path->add_child(path_follow_3d);
	Stage::get_singleton()->get_root()->add_child(path);

	path_follow_3d->set_loop(false);
	path_follow_3d->set_rotation_mode(PathFollow3D::RotationMode::ROTATION_ORIENTED);

	path_follow_3d->set_progress_ratio(0.00);
	CHECK(is_equal_approx(Vector3(-1, 0, 0), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress_ratio(0.25);
	CHECK(is_equal_approx(Vector3(0, 0, 1), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress_ratio(0.50);
	CHECK(is_equal_approx(Vector3(1, 0, 0), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress_ratio(0.75);
	CHECK(is_equal_approx(Vector3(0, 0, -1), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress_ratio(1.00);
	CHECK(is_equal_approx(Vector3(-1, 0, 0), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress_ratio(0.125);
	CHECK(is_equal_approx(Vector3(-0.688375, 0, 0.725355), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress_ratio(0.375);
	CHECK(is_equal_approx(Vector3(0.688375, 0, 0.725355), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress_ratio(0.625);
	CHECK(is_equal_approx(Vector3(0.688375, 0, -0.725355), path_follow_3d->get_transform().get_basis().get_column(2)));

	path_follow_3d->set_progress_ratio(0.875);
	CHECK(is_equal_approx(Vector3(-0.688375, 0, -0.725355), path_follow_3d->get_transform().get_basis().get_column(2)));

	memdelete(path);
}

} // namespace TestPathFollow3D

#endif // SPATIAL_DISABLED
