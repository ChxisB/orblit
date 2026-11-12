/**************************************************************************/
/*  test_spring_arm_spatial.cpp                                               */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_spring_arm_spatial)

#ifndef PHYSICS_SPATIAL_DISABLED

#include "scene/resources/spatial/box_shape_spatial.h"
#include "scene/resources/spatial/sphere_shape_spatial.h"
#include "scene/spatial/camera_spatial.h"
#include "scene/spatial/physics/collision_shape_spatial.h"
#include "scene/spatial/physics/spring_arm_spatial.h"
#include "scene/spatial/physics/static_body_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestSpringArm3D {

// A wall whose near face is at z = 5, with a spring arm at the origin pointing along +z.
struct ArmAgainstWall {
	StaticBody3D *wall = memnew(StaticBody3D);
	SpringArm3D *arm = memnew(SpringArm3D);

	ArmAgainstWall() {
		CollisionShape3D *wall_shape = memnew(CollisionShape3D);
		Ref<BoxShape3D> box = memnew(BoxShape3D);
		box->set_size(Vector3(20, 20, 2));
		wall_shape->set_shape(box);
		wall->add_child(wall_shape);
		wall->set_position(Vector3(0, 0, 6));
		arm->set_length(10);
		Stage::get_singleton()->get_root()->add_child(wall);
		Stage::get_singleton()->get_root()->add_child(arm);
	}

	~ArmAgainstWall() {
		memdelete(arm);
		memdelete(wall);
	}

	real_t hit_length_with_margin(real_t p_margin) {
		arm->set_margin(p_margin);
		arm->notification(Entity::NOTIFICATION_INTERNAL_STEP);
		return arm->get_hit_length();
	}
};

TEST_CASE("[Stage][SpringArm3D] The margin shortens a ray cast") {
	ArmAgainstWall scene;
	const real_t bare = scene.hit_length_with_margin(0.0);
	CHECK(bare < 10.0);
	CHECK(scene.hit_length_with_margin(1.0) == doctest::Approx(bare - 1.0));
}

TEST_CASE("[Stage][SpringArm3D] The margin shortens a shape cast") {
	ArmAgainstWall scene;
	Ref<SphereShape3D> sphere = memnew(SphereShape3D);
	sphere->set_radius(0.25);
	scene.arm->set_shape(sphere);
	const real_t bare = scene.hit_length_with_margin(0.0);
	CHECK(bare < 10.0);
	CHECK(scene.hit_length_with_margin(1.0) == doctest::Approx(bare - 1.0).epsilon(0.01));
}

TEST_CASE("[Stage][SpringArm3D] The margin shortens a camera cast") {
	ArmAgainstWall scene;
	Camera3D *camera = memnew(Camera3D);
	scene.arm->add_child(camera);
	const real_t bare = scene.hit_length_with_margin(0.0);
	CHECK(bare < 10.0);
	CHECK(scene.hit_length_with_margin(1.0) == doctest::Approx(bare - 1.0).epsilon(0.01));
}

TEST_CASE("[Stage][SpringArm3D] A margin larger than the hit distance stops at the origin") {
	ArmAgainstWall scene;
	CHECK(scene.hit_length_with_margin(50.0) == doctest::Approx(0.0));
}

} // namespace TestSpringArm3D

#endif // PHYSICS_SPATIAL_DISABLED
