/**************************************************************************/
/*  test_scenes.cpp                                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_scenes)

#include "core/io/file_access.h"
#include "core/io/resource.h"
#include "core/io/resource_loader.h"
#include "core/io/resource_saver.h"
#include "core/object/class_registry.h"
#include "core/object/object.h"
#include "core/object/property_info.h"
#include "core/os/memory.h"
#include "core/string/string_name.h"
#include "core/variant/variant.h"
#include "scene/resources/prefab.h"
#include "scene/stage/entity.h"
#include "tests/test_utils.h"

namespace TestScenes {

class _TestNodeForTestingNestedScenes : public Entity {
	ORBLIT_CLASS(_TestNodeForTestingNestedScenes, Entity);

	Ref<Prefab> _inner_scene;

public:
	void set_inner_scene(const Ref<Prefab> p_scene) {
		_inner_scene = p_scene;
	}

	Ref<Prefab> get_inner_scene() const {
		return _inner_scene;
	}

	static void register_members() {
		ClassRegistry::bind_method(D_METHOD("set_inner_scene", "p_scene"), &_TestNodeForTestingNestedScenes::set_inner_scene);
		ClassRegistry::bind_method(D_METHOD("get_inner_scene"), &_TestNodeForTestingNestedScenes::get_inner_scene);

		ADD_PROPERTY(
				PropertyInfo(Variant::OBJECT, "inner_scene", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_STORAGE),
				"set_inner_scene", "get_inner_scene");
	}
};

void prepare_scene(Ref<Prefab> scene) {
	Ref<Resource> child_resource = memnew(Resource);
	child_resource->set_name("I'm a child resource");
	scene->set_meta("other_resource", child_resource);

	scene->set_name("Hello world");
	scene->set_meta("ExampleMetadata", Vector2i(40, 80));
	scene->set_meta("string", "The\nstring\nwith\nunnecessary\nline\n\t\\\nbreaks");
}

void validate_scene(Ref<Prefab> loaded_scene) {
	CHECK_MESSAGE(
			loaded_scene->get_name() == "Hello world",
			"The loaded resource name should be equal to the expected value.");
	CHECK_MESSAGE(
			loaded_scene->get_meta("ExampleMetadata") == Vector2i(40, 80),
			"The loaded resource metadata should be equal to the expected value.");
	CHECK_MESSAGE(
			loaded_scene->get_meta("string") == "The\nstring\nwith\nunnecessary\nline\n\t\\\nbreaks",
			"The loaded resource metadata should be equal to the expected value.");

	const Ref<Resource> &meta_resource = loaded_scene->get_meta("other_resource");
	CHECK_MESSAGE(
			meta_resource->get_name() == "I'm a child resource",
			"The loaded child resource name should be equal to the expected value.");
}

void validate_and_instantiate_scene(Ref<Prefab> loaded_scene) {
	bool is_null = loaded_scene.is_null();
	CHECK_MESSAGE(!is_null, "Could not open scene file");

	if (is_null) {
		return;
	}

	validate_scene(loaded_scene);

	Entity *new_node = static_cast<Entity *>(loaded_scene->instantiate());
	CHECK_MESSAGE(
			new_node->get_name() == "named_node",
			"The instantiated node has invalid name.");

	const Ref<Prefab> &inner_loaded_scene = loaded_scene->get_meta("inner_scene");
	validate_scene(inner_loaded_scene);

	Entity *inner_new_node = static_cast<Entity *>(inner_loaded_scene->instantiate());
	CHECK_MESSAGE(
			inner_new_node->get_name() == "inner_named_node",
			"The instantiated node has invalid name.");

	memdelete(new_node);
	memdelete(inner_new_node);
}

TEST_CASE("[Scenes] Scene Saving and loading") {
	Entity *inner_node = memnew(Entity);
	inner_node->set_name("inner_named_node");

	Ref<Prefab> inner_scene = memnew(Prefab);
	inner_scene->pack(inner_node);
	prepare_scene(inner_scene);

	Entity *node = memnew(Entity);
	node->set_name("named_node");

	Ref<Prefab> scene = memnew(Prefab);
	scene->pack(node);
	prepare_scene(scene);
	scene->set_meta("inner_scene", inner_scene);

	Ref<Resource> child_resource = memnew(Resource);
	child_resource->set_name("I'm a child resource");
	scene->set_meta("other_resource", child_resource);

	const String save_path_binary = TestUtils::get_temp_path("scene.oresb");
	const String save_path_text = TestUtils::get_temp_path("scene.oscn");

	ResourceSaver::save(scene, save_path_binary);
	ResourceSaver::save(scene, save_path_text);

	const Ref<Prefab> &loaded_resource_binary = ResourceLoader::load(save_path_binary);
	validate_and_instantiate_scene(loaded_resource_binary);

	const Ref<Prefab> &loaded_resource_text = ResourceLoader::load(save_path_text);
	validate_and_instantiate_scene(loaded_resource_text);

	memdelete(node);
	memdelete(inner_node);
}

void validate_nested_scene(Ref<Prefab> loaded_scene) {
	bool is_null = loaded_scene.is_null();
	CHECK_MESSAGE(!is_null, "Could not open scene file");

	if (is_null) {
		return;
	}

	_TestNodeForTestingNestedScenes *node_for_testing = static_cast<_TestNodeForTestingNestedScenes *>(loaded_scene->instantiate());
	Ref<Prefab> inner_scene = node_for_testing->get_inner_scene();
	Entity *inner_node = inner_scene->instantiate();

	CHECK_MESSAGE(
			inner_node->get_name() == "named_node",
			"The instantiated node has invalid name.");

	memdelete(node_for_testing);
	memdelete(inner_node);
}

// This is to test a specific bug that can happen.
TEST_CASE("[Scenes] Scene Saving and loading as property") {
	ClassRegistry::register_class<_TestNodeForTestingNestedScenes>();

	Entity *inner_node = memnew(Entity);
	const Ref<Prefab> inner_scene = memnew(Prefab);
	inner_node->set_name("named_node");
	inner_scene->pack(inner_node);

	_TestNodeForTestingNestedScenes *nested_scene_node = memnew(_TestNodeForTestingNestedScenes);
	nested_scene_node->set_inner_scene(inner_scene);

	Ref<Prefab> scene = memnew(Prefab);
	scene->pack(nested_scene_node);

	const String save_path_binary = TestUtils::get_temp_path("nested_scene_test.oresb");
	const String save_path_text = TestUtils::get_temp_path("nested_scene_test.oscn");

	ResourceSaver::save(scene, save_path_binary);
	ResourceSaver::save(scene, save_path_text);

	memdelete(inner_node);
	memdelete(nested_scene_node);

	const Ref<Prefab> &loaded_resource_binary = ResourceLoader::load(save_path_binary);
	validate_nested_scene(loaded_resource_binary);

	const Ref<Prefab> &loaded_resource_text = ResourceLoader::load(save_path_text);
	validate_nested_scene(loaded_resource_text);
}

void validate_simple_scene(Ref<Prefab> loaded_scene) {
	bool is_null = loaded_scene.is_null();
	CHECK(!is_null);

	if (is_null) {
		return;
	}

	Entity *instanced_node = static_cast<Entity *>(loaded_scene->instantiate());
	CHECK_MESSAGE(
			instanced_node->get_name() == "SimpleNode",
			"The instantiated node has invalid name..");

	memdelete(instanced_node);
}

TEST_CASE("[Scenes] Simple scene saving and loading") {
	Entity *node = memnew(Entity);
	node->set_name("SimpleNode");

	Ref<Prefab> scene = memnew(Prefab);
	scene->pack(node);

	const String save_path_binary = TestUtils::get_temp_path("simple_scene_test.oresb");
	const String save_path_text = TestUtils::get_temp_path("simple_scene_test.oscn");

	ResourceSaver::save(scene, save_path_binary);
	ResourceSaver::save(scene, save_path_text);

	memdelete(node);

	const Ref<Prefab> &loaded_resource_binary = ResourceLoader::load(save_path_binary);
	validate_simple_scene(loaded_resource_binary);

	const Ref<Prefab> &loaded_resource_text = ResourceLoader::load(save_path_text);
	validate_simple_scene(loaded_resource_text);

	// Simple scenes should not have a [resource] tag, so let's validate it.
	Ref<FileAccess> f = FileAccess::open(save_path_text, FileAccess::READ);
	bool is_null = f.is_null();
	CHECK(!is_null);

	if (is_null) {
		return;
	}

	while (!f->eof_reached()) {
		String line;
		line = f->get_line();
		CHECK_MESSAGE(
				line != "[resource]",
				"The scene file cannot have the [resource] tag");
	}
	f->close();
}
} // namespace TestScenes
