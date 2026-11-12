/**************************************************************************/
/*  test_physics_query_after_shape_edit_spatial.cpp                           */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_physics_query_after_shape_edit_spatial)

#include "modules/modules_enabled.gen.h" // For physics 3D.

#ifdef MODULE_PHYSICS_SPATIAL_ENABLED

#include "systems/physics_spatial/physics_system_spatial.h"

namespace TestPhysicsQueryAfterShapeEdit3D {

TEST_CASE("[Stage][PhysicsSystem3D] A ray sees a shape edit made since the last step") {
	PhysicsSystem3D *system = PhysicsSystem3D::get_singleton();

	RID space = system->space_create();
	system->space_set_active(space, true);

	RID box = system->box_shape_create();
	system->shape_set_data(box, Vector3(8, 8, 8));

	// Two boxes in one body, 100 units apart.
	RID body = system->body_create();
	system->body_set_mode(body, PS3DE::BODY_MODE_STATIC);
	system->body_set_space(body, space);
	system->body_add_shape(body, box, Transform3D(Basis(), Vector3(0, 0, 0)));
	system->body_add_shape(body, box, Transform3D(Basis(), Vector3(100, 0, 0)));

	system->step(1.0 / 60.0);
	system->flush_queries();

	PS3DT::RayParameters to_first;
	to_first.from = Vector3(-50, 0, 0);
	to_first.to = Vector3(50, 0, 0);
	PS3DT::RayParameters to_second;
	to_second.from = Vector3(50, 0, 0);
	to_second.to = Vector3(200, 0, 0);
	PS3DT::RayResult result;
	CHECK(system->space_get_direct_state(space)->intersect_ray(to_first, result));
	CHECK(system->space_get_direct_state(space)->intersect_ray(to_second, result));

	// Remove the first box. No step runs before the next query.
	system->body_clear_shapes(body);
	system->body_add_shape(body, box, Transform3D(Basis(), Vector3(100, 0, 0)));

	CHECK_FALSE(system->space_get_direct_state(space)->intersect_ray(to_first, result));
	REQUIRE(system->space_get_direct_state(space)->intersect_ray(to_second, result));
	CHECK(result.position.x == doctest::Approx(92));

	system->free_rid(body);
	system->free_rid(box);
	system->free_rid(space);
}

} // namespace TestPhysicsQueryAfterShapeEdit3D

#endif // MODULE_PHYSICS_SPATIAL_ENABLED
