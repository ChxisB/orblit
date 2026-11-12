/**************************************************************************/
/*  test_physics_area_events_planar.cpp                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_physics_area_events_planar)

#include "modules/modules_enabled.gen.h" // For physics 2D.

#ifdef MODULE_PHYSICS_PLANAR_ENABLED

#include "core/object/callable_mp.h"
#include "systems/physics_planar/physics_system_planar.h"

namespace TestPhysicsAreaEvents2D {

struct AreaEvent {
	int status = 0;
	int area_shape = 0;
};

Vector<AreaEvent> area_events;

void record_area_event(int p_status, RID p_body, int64_t p_instance_id, int p_body_shape, int p_area_shape) {
	AreaEvent event;
	event.status = p_status;
	event.area_shape = p_area_shape;
	area_events.push_back(event);
}

void step_world(PhysicsSystem2D *p_system) {
	p_system->step(1.0 / 60.0);
	p_system->flush_queries();
}

TEST_CASE("[Stage][PhysicsSystem2D] A body moving between two shapes of one area enters before it leaves") {
	PhysicsSystem2D *system = PhysicsSystem2D::get_singleton();
	area_events.clear();

	RID space = system->space_create();
	system->space_set_active(space, true);

	RID box = system->rectangle_shape_create();
	system->shape_set_data(box, Vector2(2, 2));
	RID circle = system->circle_shape_create();
	system->shape_set_data(circle, 1.0);

	// Two separate boxes in one area, ten units apart.
	RID area = system->area_create();
	system->area_set_space(area, space);
	system->area_add_shape(area, box, Transform2D(0, Vector2(0, 0)));
	system->area_add_shape(area, box, Transform2D(0, Vector2(10, 0)));
	system->area_set_monitor_callback(area, callable_mp_static(&record_area_event));

	RID body = system->body_create();
	system->body_set_mode(body, PS2DE::BODY_MODE_KINEMATIC);
	system->body_set_space(body, space);
	system->body_add_shape(body, circle);
	system->body_set_state(body, PS2DE::BODY_STATE_TRANSFORM, Transform2D(0, Vector2(0, 0)));

	step_world(system);
	REQUIRE(area_events.size() == 1);
	CHECK(area_events[0].status == PS2DE::AREA_BODY_ADDED);
	CHECK(area_events[0].area_shape == 0);

	area_events.clear();
	system->body_set_state(body, PS2DE::BODY_STATE_TRANSFORM, Transform2D(0, Vector2(10, 0)));
	step_world(system);
	step_world(system);

	REQUIRE(area_events.size() == 2);
	CHECK(area_events[0].status == PS2DE::AREA_BODY_ADDED);
	CHECK(area_events[0].area_shape == 1);
	CHECK(area_events[1].status == PS2DE::AREA_BODY_REMOVED);
	CHECK(area_events[1].area_shape == 0);

	system->free_rid(body);
	system->free_rid(area);
	system->free_rid(circle);
	system->free_rid(box);
	system->free_rid(space);
}

} // namespace TestPhysicsAreaEvents2D

#endif // MODULE_PHYSICS_PLANAR_ENABLED
