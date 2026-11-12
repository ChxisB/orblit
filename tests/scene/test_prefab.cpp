/**************************************************************************/
/*  test_prefab.cpp                                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_prefab)

#include "core/io/file_access.h"
#include "core/io/resource_loader.h"
#include "core/object/callable_mp.h"
#include "core/variant/variant_parser.h"
#include "scene/resources/prefab.h"
#include "tests/test_utils.h"

namespace TestPrefab {

TEST_CASE("[Prefab] Pack Scene and Retrieve State") {
	// Create a scene to pack.
	Entity *scene = memnew(Entity);
	scene->set_name("TestScene");

	// Pack the scene.
	Prefab prefab;
	const Error err = prefab.pack(scene);
	CHECK(err == OK);

	// Retrieve the packed state.
	Ref<SceneState> state = prefab.get_state();
	CHECK(state.is_valid());
	CHECK(state->get_entity_count() == 1);
	CHECK(state->get_entity_name(0) == "TestScene");

	memdelete(scene);
}

TEST_CASE("[Prefab] Signals Preserved when Packing Scene") {
	// Create main scene
	// root
	// `- sub_node (local)
	// `- sub_scene (instance of another scene)
	//    `- sub_scene_node (owned by sub_scene)
	Entity *main_scene_root = memnew(Entity);
	Entity *sub_node = memnew(Entity);
	Entity *sub_scene_root = memnew(Entity);
	Entity *sub_scene_node = memnew(Entity);

	main_scene_root->add_child(sub_node);
	sub_node->set_owner(main_scene_root);

	sub_scene_root->add_child(sub_scene_node);
	sub_scene_node->set_owner(sub_scene_root);

	main_scene_root->add_child(sub_scene_root);
	sub_scene_root->set_owner(main_scene_root);

	SUBCASE("Signals that should be saved") {
		int main_flags = Object::CONNECT_PERSIST;
		// sub node to a node in main scene
		sub_node->connect("started", callable_mp(main_scene_root, &Entity::is_ready), main_flags);
		// subscene root to a node in main scene
		sub_scene_root->connect("started", callable_mp(main_scene_root, &Entity::is_ready), main_flags);
		//subscene root to subscene root (connected within main scene)
		sub_scene_root->connect("started", callable_mp(sub_scene_root, &Entity::is_ready), main_flags);

		// Pack the scene.
		Ref<Prefab> prefab;
		prefab.instantiate();
		const Error err = prefab->pack(main_scene_root);
		CHECK(err == OK);

		// Make sure the right connections are in the prefab.
		Ref<SceneState> state = prefab->get_state();
		CHECK_EQ(state->get_connection_count(), 3);
	}

	/*
	// FIXME: This subcase requires GH-48064 to be fixed.
	SUBCASE("Signals that should not be saved") {
		int subscene_flags = Object::CONNECT_PERSIST | Object::CONNECT_INHERITED;
		// subscene node to itself
		sub_scene_node->connect("started", callable_mp(sub_scene_node, &Entity::is_ready), subscene_flags);
		// subscene node to subscene root
		sub_scene_node->connect("started", callable_mp(sub_scene_root, &Entity::is_ready), subscene_flags);
		//subscene root to subscene root (connected within sub scene)
		sub_scene_root->connect("started", callable_mp(sub_scene_root, &Entity::is_ready), subscene_flags);

		// Pack the scene.
		Ref<Prefab> prefab;
		prefab.instantiate();
		const Error err = prefab->pack(main_scene_root);
		CHECK(err == OK);

		// Make sure the right connections are in the prefab.
		Ref<SceneState> state = prefab->get_state();
		CHECK_EQ(state->get_connection_count(), 0);
	}
	*/

	memdelete(main_scene_root);
}

TEST_CASE("[Prefab] Clear Prefab") {
	// Create a scene to pack.
	Entity *scene = memnew(Entity);
	scene->set_name("TestScene");

	// Pack the scene.
	Prefab prefab;
	prefab.pack(scene);

	// Clear the prefab.
	prefab.clear();

	// Check if it has been cleared.
	Ref<SceneState> state = prefab.get_state();
	CHECK_FALSE(state->get_entity_count() == 1);

	memdelete(scene);
}

TEST_CASE("[Prefab] Can Instantiate Prefab") {
	// Create a scene to pack.
	Entity *scene = memnew(Entity);
	scene->set_name("TestScene");

	// Pack the scene.
	Prefab prefab;
	prefab.pack(scene);

	// Check if the prefab can be instantiated.
	const bool can_instantiate = prefab.can_instantiate();
	CHECK(can_instantiate == true);

	memdelete(scene);
}

TEST_CASE("[Prefab] Instantiate Prefab") {
	// Create a scene to pack.
	Entity *scene = memnew(Entity);
	scene->set_name("TestScene");

	// Pack the scene.
	Prefab prefab;
	prefab.pack(scene);

	// Instantiate the prefab.
	Entity *instance = prefab.instantiate();
	CHECK(instance != nullptr);
	CHECK(instance->get_name() == "TestScene");

	memdelete(scene);
	memdelete(instance);
}

TEST_CASE("[Prefab] Instantiate Prefab With Children") {
	// Create a scene to pack.
	Entity *scene = memnew(Entity);
	scene->set_name("TestScene");

	// Add persisting child nodes to the scene.
	Entity *child1 = memnew(Entity);
	child1->set_name("Child1");
	scene->add_child(child1);
	child1->set_owner(scene);

	Entity *child2 = memnew(Entity);
	child2->set_name("Child2");
	scene->add_child(child2);
	child2->set_owner(scene);

	// Add non persisting child node to the scene.
	Entity *child3 = memnew(Entity);
	child3->set_name("Child3");
	scene->add_child(child3);

	// Pack the scene.
	Prefab prefab;
	prefab.pack(scene);

	// Instantiate the prefab.
	Entity *instance = prefab.instantiate();
	CHECK(instance != nullptr);
	CHECK(instance->get_name() == "TestScene");

	// Validate the child nodes of the instantiated scene.
	CHECK(instance->get_child_count() == 2);
	CHECK(instance->get_child(0)->get_name() == "Child1");
	CHECK(instance->get_child(1)->get_name() == "Child2");
	CHECK(instance->get_child(0)->get_owner() == instance);
	CHECK(instance->get_child(1)->get_owner() == instance);

	memdelete(scene);
	memdelete(instance);
}

TEST_CASE("[Prefab] Set Path") {
	// Create a scene to pack.
	Entity *scene = memnew(Entity);
	scene->set_name("TestScene");

	// Pack the scene.
	Prefab prefab;
	prefab.pack(scene);

	// Set a new path for the prefab.
	const String new_path = "NewTestPath";
	prefab.set_path(new_path);

	// Check if the path has been set correctly.
	Ref<SceneState> state = prefab.get_state();
	CHECK(state.is_valid());
	CHECK(state->get_path() == new_path);

	memdelete(scene);
}

TEST_CASE("[Prefab] Replace State") {
	// Create a scene to pack.
	Entity *scene = memnew(Entity);
	scene->set_name("TestScene");

	// Pack the scene.
	Prefab prefab;
	prefab.pack(scene);

	// Create another scene state to replace with.
	Ref<SceneState> new_state = memnew(SceneState);
	new_state->set_path("NewPath");

	// Replace the state.
	prefab.replace_state(new_state);

	// Check if the state has been replaced.
	Ref<SceneState> state = prefab.get_state();
	CHECK(state.is_valid());
	CHECK(state == new_state);

	memdelete(scene);
}

TEST_CASE("[Prefab] Recreate State") {
	// Create a scene to pack.
	Entity *scene = memnew(Entity);
	scene->set_name("TestScene");

	// Pack the scene.
	Ref<Prefab> prefab;
	prefab.instantiate();
	prefab->pack(scene);

	// Recreate the state.
	prefab->recreate_state();

	// Check if the state has been recreated.
	Ref<SceneState> state = prefab->get_state();
	CHECK(state.is_valid());
	CHECK(state->get_entity_count() == 0); // Since the state was recreated, it should be empty.

	memdelete(scene);
}

TEST_CASE("[Prefab] A bundled sub prefab does not take the nodes of its file") {
	Entity *sub_root = memnew(Entity);
	sub_root->set_name("Sub");
	Ref<Prefab> sub_prefab;
	sub_prefab.instantiate();
	REQUIRE(sub_prefab->pack(sub_root) == OK);
	memdelete(sub_root);

	// Older files keep the nodes of an embedded prefab in `_bundled`.
	String bundled_text;
	REQUIRE(VariantWriter::write_to_string(sub_prefab->get("_bundled"), bundled_text) == OK);
	const String text = "[orb_scene format=3]\n\n[sub_resource type=\"Prefab\" id=\"Prefab_a\"]\n_bundled = " + bundled_text + "\n\n[node name=\"Root\" type=\"Entity\"]\nmetadata/embedded = SubResource(\"Prefab_a\")\n";

	const String path = TestUtils::get_temp_path("prefab_bundled_sub.oscn");
	Ref<FileAccess> file = FileAccess::open(path, FileAccess::WRITE);
	REQUIRE(file.is_valid());
	file->store_string(text);
	file.unref();

	Error err = OK;
	Ref<Prefab> loaded = ResourceLoader::load(path, "Prefab", ResourceFormatLoader::CACHE_MODE_IGNORE, &err);
	REQUIRE(err == OK);
	REQUIRE(loaded.is_valid());

	Ref<SceneState> state = loaded->get_state();
	REQUIRE(state->get_entity_count() == 1);
	CHECK(state->get_entity_name(0) == "Root");

	Entity *root = loaded->instantiate();
	REQUIRE(root != nullptr);
	Ref<Prefab> embedded = root->get_meta("embedded");
	REQUIRE(embedded.is_valid());
	CHECK(embedded->get_state()->get_entity_count() == 1);
	CHECK(embedded->get_state()->get_entity_name(0) == "Sub");
	memdelete(root);
}

} // namespace TestPrefab
