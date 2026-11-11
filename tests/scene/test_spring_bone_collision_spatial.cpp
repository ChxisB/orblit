/**************************************************************************/
/*  test_spring_bone_collision_spatial.cpp                                     */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_spring_bone_collision_spatial)

#ifndef SPATIAL_DISABLED

#include "scene/spatial/skeleton_spatial.h"
#include "scene/spatial/spring_bone_collision_sphere_spatial.h"
#include "scene/spatial/spring_bone_simulator_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestSpringBoneCollision3D {

TEST_CASE("[Stage][SpringBoneCollision3D] A collider is placed in the space of the center") {
	Skeleton3D *skeleton = memnew(Skeleton3D);
	SpringBoneSimulator3D *simulator = memnew(SpringBoneSimulator3D);
	SpringBoneCollisionSphere3D *collision = memnew(SpringBoneCollisionSphere3D);
	Entity3D *center_node = memnew(Entity3D);

	Stage::get_singleton()->get_root()->add_child(skeleton);
	Stage::get_singleton()->get_root()->add_child(center_node);
	skeleton->add_child(simulator);
	simulator->add_child(collision);

	// The skeleton and the center node are turned differently, so the order of the
	// multiplication shows in the result.
	skeleton->set_global_transform(Transform3D(Basis(Vector3(0, 1, 0), Math::deg_to_rad(90.0)), Vector3(1, 0, 0)));
	center_node->set_global_transform(Transform3D(Basis(Vector3(1, 0, 0), Math::deg_to_rad(30.0)), Vector3(0, 2, 0)));
	collision->set_global_transform(Transform3D(Basis(), Vector3(3, 0, 4)));

	// The center a simulator caches for a node center.
	const Transform3D center = center_node->get_global_transform().affine_inverse() * skeleton->get_global_transform();
	const Transform3D expected = center_node->get_global_transform().affine_inverse() * collision->get_global_transform();

	CHECK(collision->get_transform_from_skeleton(center).origin.is_equal_approx(expected.origin));

	memdelete(center_node);
	memdelete(skeleton);
}

} // namespace TestSpringBoneCollision3D

#endif // SPATIAL_DISABLED
