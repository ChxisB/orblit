/**************************************************************************/
/*  test_navigation_agent_planar.cpp                                          */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_navigation_agent_planar)

#include "modules/modules_enabled.gen.h" // For navigation 2D.

#ifdef MODULE_NAVIGATION_PLANAR_ENABLED

#include "scene/planar/navigation/navigation_agent_planar.h"
#include "scene/planar/entity_planar.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/resources/world_planar.h"

namespace TestNavigationAgent2D {

TEST_SUITE("[Navigation2D]") {
	TEST_CASE("[Stage][NavigationAgent2D] New agent should have valid RID") {
		NavigationAgent2D *agent_node = memnew(NavigationAgent2D);
		CHECK(agent_node->get_rid().is_valid());
		memdelete(agent_node);
	}

	TEST_CASE("[Stage][NavigationAgent2D] New agent should attach to default map") {
		Entity2D *node_2d = memnew(Entity2D);
		Stage::get_singleton()->get_root()->add_child(node_2d);

		NavigationAgent2D *agent_node = memnew(NavigationAgent2D);

		// agent should not be attached to any map when outside of tree
		CHECK_FALSE(agent_node->get_navigation_map().is_valid());

		SUBCASE("Agent should attach to default map when it enters the tree") {
			node_2d->add_child(agent_node);
			CHECK(agent_node->get_navigation_map().is_valid());
			CHECK(agent_node->get_navigation_map() == node_2d->get_world_2d()->get_navigation_map());
		}

		memdelete(agent_node);
		memdelete(node_2d);
	}
}

} // namespace TestNavigationAgent2D

#endif // MODULE_NAVIGATION_PLANAR_ENABLED
