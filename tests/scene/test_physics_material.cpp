/**************************************************************************/
/*  test_physics_material.cpp                                             */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_physics_material)

#ifndef PHYSICS_SPATIAL_DISABLED

#include "scene/resources/physics_material.h"

namespace TestPhysicsMaterial {

TEST_CASE("[Physics_material] Defaults") {
	Ref<PhysicsMaterial> physics_material;
	physics_material.instantiate();

	CHECK(physics_material->get_friction() == 1.);
	CHECK(physics_material->is_rough() == false);
	CHECK(physics_material->get_bounce() == 0.);
	CHECK(physics_material->is_absorbent() == false);
}

TEST_CASE("[Physics_material] Friction") {
	Ref<PhysicsMaterial> physics_material;
	physics_material.instantiate();

	real_t friction = 0.314;
	physics_material->set_friction(friction);
	CHECK(physics_material->get_friction() == friction);
}

TEST_CASE("[Physics_material] Rough") {
	Ref<PhysicsMaterial> physics_material;
	physics_material.instantiate();

	bool rough = true;
	physics_material->set_rough(rough);
	CHECK(physics_material->is_rough() == rough);

	real_t friction = 0.314;
	physics_material->set_friction(friction);
	CHECK(physics_material->computed_friction() == -friction);

	rough = false;
	physics_material->set_rough(rough);
	CHECK(physics_material->is_rough() == rough);

	CHECK(physics_material->computed_friction() == friction);
}

TEST_CASE("[Physics_material] Bounce") {
	Ref<PhysicsMaterial> physics_material;
	physics_material.instantiate();

	real_t bounce = 0.271;
	physics_material->set_bounce(bounce);
	CHECK(physics_material->get_bounce() == bounce);
}

TEST_CASE("[Physics_material] Absorbent") {
	Ref<PhysicsMaterial> physics_material;
	physics_material.instantiate();

	bool absorbent = true;
	physics_material->set_absorbent(absorbent);
	CHECK(physics_material->is_absorbent() == absorbent);

	real_t bounce = 0.271;
	physics_material->set_bounce(bounce);
	CHECK(physics_material->computed_bounce() == -bounce);

	absorbent = false;
	physics_material->set_absorbent(absorbent);
	CHECK(physics_material->is_absorbent() == absorbent);

	CHECK(physics_material->computed_bounce() == bounce);
}

} // namespace TestPhysicsMaterial

#endif // PHYSICS_SPATIAL_DISABLED
