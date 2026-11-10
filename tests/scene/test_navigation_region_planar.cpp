/**************************************************************************/
/*  test_navigation_region_planar.cpp                                         */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_navigation_region_planar)

#include "modules/modules_enabled.gen.h" // For navigation 2D.

#ifdef MODULE_NAVIGATION_PLANAR_ENABLED

#include "scene/planar/navigation/navigation_region_planar.h"

namespace TestNavigationRegion2D {

TEST_SUITE("[Navigation2D]") {
	TEST_CASE("[Stage][NavigationRegion2D] New region should have valid RID") {
		NavigationRegion2D *region_node = memnew(NavigationRegion2D);
		CHECK(region_node->get_rid().is_valid());
		memdelete(region_node);
	}
}

} // namespace TestNavigationRegion2D

#endif // MODULE_NAVIGATION_PLANAR_ENABLED
