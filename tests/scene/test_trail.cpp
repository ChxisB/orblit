/**************************************************************************/
/*  test_trail.cpp                                                        */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_trail)

#include "core/object/class_registry.h"
#include "scene/spatial/trail_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "systems/rendering/rendering_system.h"
#include "tests/test_tools.h"

namespace TestTrail {

TEST_CASE("[Stage][Trail3D] A length trail with a queued redraw enters the stage without an error") {
	Trail3D *trail = Object::cast_to<Trail3D>(ClassRegistry::instantiate("Trail3D"));
	REQUIRE(trail != nullptr);
	trail->set_limit_mode(Trail3D::LIMIT_MODE_MAX_LENGTH);
	trail->set_width(2.0);

	ErrorDetector errors;
	Stage::get_singleton()->get_root()->add_child(trail);
	CHECK_FALSE(errors.has_error);
	memdelete(trail);
}

TEST_CASE("[Stage][Trail3D] A lifetime trail set before it enters the stage connects once") {
	Trail3D *trail = Object::cast_to<Trail3D>(ClassRegistry::instantiate("Trail3D"));
	REQUIRE(trail != nullptr);
	trail->set_limit_mode(Trail3D::LIMIT_MODE_MAX_LENGTH);
	trail->set_limit_mode(Trail3D::LIMIT_MODE_LIFETIME);

	ErrorDetector errors;
	Stage::get_singleton()->get_root()->add_child(trail);
	CHECK_FALSE(errors.has_error);
	memdelete(trail);
}

} // namespace TestTrail
