/**************************************************************************/
/*  test_physics_area_events_spatial.cpp                                      */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_physics_area_events_spatial)

#include "modules/modules_enabled.gen.h" // For physics 3D.

#ifdef MODULE_PHYSICS_SPATIAL_ENABLED

#include "core/object/callable_mp.h"
#include "systems/physics_spatial/physics_system_spatial.h"

namespace TestPhysicsAreaEvents3D {

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

void step_world(PhysicsSystem3D *p_system) {
	p_system->step(1.0 / 60.0);
	p_system->flush_queries();
}

TEST_CASE("[Stage][PhysicsSystem3D] A body moving between two shapes of one area enters before it leaves") {
	PhysicsSystem3D *system = PhysicsSystem3D::get_singleton();
	area_events.clear();

	RID space = system->space_create();
	system->space_set_active(space, true);

	RID box = system->box_shape_create();
	system->shape_set_data(box, Vector3(1, 1, 1));
	RID sphere = system->sphere_shape_create();
	system->shape_set_data(sphere, 0.5);

	// Two separate boxes in one area, ten units apart.
	RID area = system->area_create();
	system->area_set_space(area, space);
	system->area_add_shape(area, box, Transform3D(Basis(), Vector3(0, 0, 0)));
	system->area_add_shape(area, box, Transform3D(Basis(), Vector3(10, 0, 0)));
	system->area_set_monitor_callback(area, callable_mp_static(&record_area_event));

	RID body = system->body_create();
	system->body_set_mode(body, PS3DE::BODY_MODE_KINEMATIC);
	system->body_set_space(body, space);
	system->body_add_shape(body, sphere);
	system->body_set_state(body, PS3DE::BODY_STATE_TRANSFORM, Transform3D(Basis(), Vector3(0, 0, 0)));

	step_world(system);
	REQUIRE(area_events.size() == 1);
	CHECK(area_events[0].status == PS3DE::AREA_BODY_ADDED);
	CHECK(area_events[0].area_shape == 0);

	area_events.clear();
	system->body_set_state(body, PS3DE::BODY_STATE_TRANSFORM, Transform3D(Basis(), Vector3(10, 0, 0)));
	step_world(system);
	step_world(system);

	REQUIRE(area_events.size() == 2);
	CHECK(area_events[0].status == PS3DE::AREA_BODY_ADDED);
	CHECK(area_events[0].area_shape == 1);
	CHECK(area_events[1].status == PS3DE::AREA_BODY_REMOVED);
	CHECK(area_events[1].area_shape == 0);

	system->free_rid(body);
	system->free_rid(area);
	system->free_rid(sphere);
	system->free_rid(box);
	system->free_rid(space);
}

} // namespace TestPhysicsAreaEvents3D

#endif // MODULE_PHYSICS_SPATIAL_ENABLED
