/**************************************************************************/
/*  test_physics_query_after_shape_edit_planar.cpp                            */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_physics_query_after_shape_edit_planar)

#include "modules/modules_enabled.gen.h" // For physics 2D.

#ifdef MODULE_PHYSICS_PLANAR_ENABLED

#include "systems/physics_planar/physics_system_planar.h"

namespace TestPhysicsQueryAfterShapeEdit2D {

TEST_CASE("[Stage][PhysicsSystem2D] A ray sees a shape edit made since the last step") {
	PhysicsSystem2D *system = PhysicsSystem2D::get_singleton();

	RID space = system->space_create();
	system->space_set_active(space, true);

	RID box = system->rectangle_shape_create();
	system->shape_set_data(box, Vector2(8, 8));

	// Two boxes in one body, 100 units apart, like two tiles of a tile map.
	RID body = system->body_create();
	system->body_set_mode(body, PS2DE::BODY_MODE_STATIC);
	system->body_set_space(body, space);
	system->body_add_shape(body, box, Transform2D(0, Vector2(0, 0)));
	system->body_add_shape(body, box, Transform2D(0, Vector2(100, 0)));

	system->step(1.0 / 60.0);
	system->flush_queries();

	PS2DT::RayParameters to_first;
	to_first.from = Vector2(-50, 0);
	to_first.to = Vector2(50, 0);
	PS2DT::RayParameters to_second;
	to_second.from = Vector2(50, 0);
	to_second.to = Vector2(200, 0);
	PS2DT::RayResult result;
	CHECK(system->space_get_direct_state(space)->intersect_ray(to_first, result));
	CHECK(system->space_get_direct_state(space)->intersect_ray(to_second, result));

	// Remove the first box. No step runs before the next query.
	system->body_clear_shapes(body);
	system->body_add_shape(body, box, Transform2D(0, Vector2(100, 0)));

	CHECK_FALSE(system->space_get_direct_state(space)->intersect_ray(to_first, result));
	REQUIRE(system->space_get_direct_state(space)->intersect_ray(to_second, result));
	CHECK(result.position.x == doctest::Approx(92));

	system->free_rid(body);
	system->free_rid(box);
	system->free_rid(space);
}

} // namespace TestPhysicsQueryAfterShapeEdit2D

#endif // MODULE_PHYSICS_PLANAR_ENABLED
