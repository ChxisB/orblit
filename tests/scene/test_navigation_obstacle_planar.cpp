/**************************************************************************/
/*  test_navigation_obstacle_planar.cpp                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_navigation_obstacle_planar)

#include "modules/modules_enabled.gen.h" // For navigation 2D.

#ifdef MODULE_NAVIGATION_PLANAR_ENABLED

#include "scene/planar/navigation/navigation_obstacle_planar.h"
#include "scene/resources/world_planar.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestNavigationObstacle2D {

TEST_SUITE("[Navigation2D]") {
	TEST_CASE("[Stage][NavigationObstacle2D] New obstacle should have valid RID") {
		NavigationObstacle2D *obstacle_node = memnew(NavigationObstacle2D);
		CHECK(obstacle_node->get_rid().is_valid());
		memdelete(obstacle_node);
	}

	TEST_CASE("[Stage][NavigationObstacle2D] New obstacle should attach to default map") {
		Entity2D *node_2d = memnew(Entity2D);
		Stage::get_singleton()->get_root()->add_child(node_2d);

		NavigationObstacle2D *obstacle_node = memnew(NavigationObstacle2D);
		// obstacle should not be attached to any map when outside of tree
		CHECK_FALSE(obstacle_node->get_navigation_map().is_valid());

		SUBCASE("Obstacle should attach to default map when it enters the tree") {
			node_2d->add_child(obstacle_node);
			CHECK(obstacle_node->get_navigation_map().is_valid());
			CHECK(obstacle_node->get_navigation_map() == node_2d->get_world_2d()->get_navigation_map());
		}

		memdelete(obstacle_node);
		memdelete(node_2d);
	}
}

} // namespace TestNavigationObstacle2D

#endif // MODULE_NAVIGATION_PLANAR_ENABLED
