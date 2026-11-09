/**************************************************************************/
/*  test_navigation_obstacle_spatial.cpp                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_navigation_obstacle_spatial)

#include "modules/modules_enabled.gen.h" // For navigation 3D.

#ifdef MODULE_NAVIGATION_SPATIAL_ENABLED

#include "scene/spatial/navigation/navigation_obstacle_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestNavigationObstacle3D {

TEST_SUITE("[Navigation3D]") {
	TEST_CASE("[Stage][NavigationObstacle3D] New obstacle should have valid RID") {
		NavigationObstacle3D *obstacle_node = memnew(NavigationObstacle3D);
		CHECK(obstacle_node->get_rid().is_valid());
		memdelete(obstacle_node);
	}

	TEST_CASE("[Stage][NavigationObstacle3D] New obstacle should attach to default map") {
		Entity3D *node_3d = memnew(Entity3D);
		Stage::get_singleton()->get_root()->add_child(node_3d);

		NavigationObstacle3D *obstacle_node = memnew(NavigationObstacle3D);
		// obstacle should not be attached to any map when outside of tree
		CHECK_FALSE(obstacle_node->get_navigation_map().is_valid());

		SUBCASE("Obstacle should attach to default map when it enters the tree") {
			node_3d->add_child(obstacle_node);
			CHECK(obstacle_node->get_navigation_map().is_valid());
			CHECK(obstacle_node->get_navigation_map() == node_3d->get_world_3d()->get_navigation_map());
		}

		memdelete(obstacle_node);
		memdelete(node_3d);
	}
}

} // namespace TestNavigationObstacle3D

#endif // MODULE_NAVIGATION_SPATIAL_ENABLED
