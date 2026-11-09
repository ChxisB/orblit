/**************************************************************************/
/*  test_grid_map.h                                                       */

#pragma once

#include "../grid_map.h"

#include "scene/resources/spatial/primitive_meshes.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "tests/test_macros.h"

namespace TestGridMap {

TEST_CASE("[Stage][GridMap][MeshLibrary] Octant querying") {
	Ref<MeshLibrary> mesh_library;
	mesh_library.instantiate();

	int item1 = 0;
	int item2 = 1;

	{
		Vector<int> item_list = mesh_library->get_item_list();
		CHECK(item_list.size() == 0);
	}

	mesh_library->create_item(item1);
	mesh_library->create_item(item2);

	int last_unused_item_id = mesh_library->get_last_unused_item_id();
	CHECK(last_unused_item_id == 2);

	mesh_library->set_item_name(item1, "Item1");
	mesh_library->set_item_name(item2, "Item2");

	Ref<BoxMesh> box_mesh;
	box_mesh.instantiate();
	box_mesh->set_size(Vector3(1.0, 1.0, 1.0));

	mesh_library->set_item_mesh(item1, box_mesh);
	mesh_library->set_item_mesh(item2, box_mesh);

	{
		Vector<int> item_list = mesh_library->get_item_list();
		CHECK(item_list.size() == 2);
		CHECK(item_list[0] == item1);
		CHECK(item_list[1] == item2);
	}

	GridMap *grid_map = memnew(GridMap);
	grid_map->set_mesh_library(mesh_library);

	Stage::get_singleton()->get_root()->add_child(grid_map);

	grid_map->set_octant_size(8);
	grid_map->set_cell_size(Vector3(1.0, 1.0, 1.0));

	grid_map->set_cell_item(Vector3i(0, 0, 0), -1);

	CHECK(grid_map->get_used_cells().size() == 0);
	CHECK(grid_map->get_used_octants().size() == 0);

	grid_map->set_cell_item(Vector3i(0, 0, 0), item1);
	grid_map->set_cell_item(Vector3i(7, 7, 7), item2);

	CHECK(grid_map->get_used_cells().size() == 2);
	CHECK(grid_map->get_used_octants().size() == 1);
	CHECK(grid_map->get_used_octants()[0] == Vector3i(0, 0, 0));

	grid_map->set_cell_item(Vector3i(8, 8, 8), item2);

	CHECK(grid_map->get_used_cells().size() == 3);
	CHECK(grid_map->get_used_octants().size() == 2);
	CHECK(grid_map->get_used_octants()[0] == Vector3i(0, 0, 0));
	CHECK(grid_map->get_used_octants()[1] == Vector3i(1, 1, 1));

	AABB bounds;
	bounds.position = Vector3(0.0, 0.0, 0.0);
	bounds.size = Vector3(8.0, 8.0, 8.0);

	CHECK(grid_map->get_octants_in_bounds(bounds).size() == 1);
	CHECK(grid_map->get_used_octants_in_bounds(bounds).size() == 1);
	CHECK(grid_map->get_octants_in_bounds(bounds)[0] == Vector3i(0, 0, 0));

	// Edge margin is -CMP_EPSILON.
	bounds.size = Vector3(8.001, 8.001, 8.001);
	CHECK(grid_map->get_octants_in_bounds(bounds).size() == 8);
	CHECK(grid_map->get_used_octants_in_bounds(bounds).size() == 2);

	Stage::get_singleton()->get_root()->remove_child(grid_map);

	memdelete(grid_map);
}

} // namespace TestGridMap
