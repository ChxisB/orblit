/**************************************************************************/
/*  test_navigation_region_spatial.cpp                                         */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_navigation_region_spatial)

#include "modules/modules_enabled.gen.h" // For navigation 3D.

#ifdef MODULE_NAVIGATION_SPATIAL_ENABLED

#include "scene/spatial/mesh_instance_spatial.h"
#include "scene/spatial/navigation/navigation_region_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/resources/spatial/primitive_meshes.h"

namespace TestNavigationRegion3D {

TEST_SUITE("[Navigation3D]") {
	TEST_CASE("[Stage][NavigationRegion3D] New region should have valid RID") {
		NavigationRegion3D *region_node = memnew(NavigationRegion3D);
		CHECK(region_node->get_rid().is_valid());
		memdelete(region_node);
	}

	TEST_CASE("[Stage][NavigationRegion3D] Region should bake successfully from valid geometry") {
		Entity3D *node_3d = memnew(Entity3D);
		Stage::get_singleton()->get_root()->add_child(node_3d);
		Ref<NavigationMesh> navigation_mesh = memnew(NavigationMesh);
		NavigationRegion3D *navigation_region = memnew(NavigationRegion3D);
		navigation_region->set_navigation_mesh(navigation_mesh);
		node_3d->add_child(navigation_region);
		Ref<PlaneMesh> plane_mesh = memnew(PlaneMesh);
		plane_mesh->set_size(Size2(10.0, 10.0));
		MeshInstance3D *mesh_instance = memnew(MeshInstance3D);
		mesh_instance->set_mesh(plane_mesh);
		navigation_region->add_child(mesh_instance);

		CHECK_FALSE(navigation_region->is_baking());
		CHECK_EQ(navigation_mesh->get_polygon_count(), 0);
		CHECK_EQ(navigation_mesh->get_vertices().size(), 0);

		SUBCASE("Synchronous bake should have immediate effects") {
			ERR_PRINT_OFF; // Suppress warning about baking from visual meshes as source geometry.
			navigation_region->bake_navigation_mesh(false);
			ERR_PRINT_ON;
			CHECK_FALSE(navigation_region->is_baking());
			CHECK_NE(navigation_mesh->get_polygon_count(), 0);
			CHECK_NE(navigation_mesh->get_vertices().size(), 0);
		}

		memdelete(mesh_instance);
		memdelete(navigation_region);
		memdelete(node_3d);
	}
}

} // namespace TestNavigationRegion3D

#endif // MODULE_NAVIGATION_SPATIAL_ENABLED
