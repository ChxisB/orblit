/**************************************************************************/
/*  test_entity.cpp                                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_entity)

#include "core/io/file_access.h"
#include "core/io/resource_saver.h"
#include "core/object/class_registry.h"
#include "scene/stage/entity.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/resources/prefab.h"
#include "tests/test_utils.h"

namespace TestNode {

class TestNode : public Entity {
	ORBLIT_CLASS(TestNode, Entity);

protected:
	void _notification(int p_what) {
		switch (p_what) {
			case NOTIFICATION_INTERNAL_FRAME: {
				internal_process_counter++;
				push_self();
			} break;
			case NOTIFICATION_INTERNAL_STEP: {
				internal_physics_process_counter++;
				push_self();
			} break;
			case NOTIFICATION_FRAME: {
				process_counter++;
				push_self();
			} break;
			case NOTIFICATION_STEP: {
				physics_process_counter++;
				push_self();
			} break;
		}
	}

	static void register_members() {
		ClassRegistry::bind_method(D_METHOD("set_exported_node", "node"), &TestNode::set_exported_node);
		ClassRegistry::bind_method(D_METHOD("get_exported_node"), &TestNode::get_exported_node);
		ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "exported_node", PROPERTY_HINT_ENTITY_TYPE, "Entity"), "set_exported_node", "get_exported_node");

		ClassRegistry::bind_method(D_METHOD("set_exported_nodes", "node"), &TestNode::set_exported_nodes);
		ClassRegistry::bind_method(D_METHOD("get_exported_nodes"), &TestNode::get_exported_nodes);
		ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "exported_nodes", PROPERTY_HINT_TYPE_STRING, vformat("%d/%d:Entity", Variant::OBJECT, PROPERTY_HINT_ENTITY_TYPE)), "set_exported_nodes", "get_exported_nodes");
	}

private:
	void push_self() {
		if (callback_list) {
			callback_list->push_back(this);
		}
	}

public:
	int internal_process_counter = 0;
	int internal_physics_process_counter = 0;
	int process_counter = 0;
	int physics_process_counter = 0;

	Entity *exported_node = nullptr;
	Array exported_nodes;

	List<Entity *> *callback_list = nullptr;

	void set_exported_node(Entity *p_node) { exported_node = p_node; }
	Entity *get_exported_node() const { return exported_node; }

	void set_exported_nodes(const Array &p_nodes) { exported_nodes = p_nodes; }
	Array get_exported_nodes() const { return exported_nodes; }

	TestNode() {
		Entity *internal = memnew(Entity);
		add_child(internal, false, INTERNAL_MODE_FRONT);
		internal = memnew(Entity);
		add_child(internal, false, INTERNAL_MODE_BACK);
	}
};

TEST_CASE("[Stage][Entity] Testing node operations with a very simple scene tree") {
	Entity *node = memnew(Entity);

	// Check initial scene tree setup.
	CHECK_EQ(Stage::get_singleton()->get_root()->get_child_count(), 0);
	CHECK_EQ(Stage::get_singleton()->get_entity_count(), 1);

	// Check initial node setup.
	CHECK(node->get_name() == StringName());
	CHECK_FALSE(node->is_inside_stage());
	CHECK_EQ(node->get_parent(), nullptr);
	ERR_PRINT_OFF;
	CHECK(node->get_path().is_empty());
	ERR_PRINT_ON;
	CHECK_EQ(node->get_child_count(), 0);

	Stage::get_singleton()->get_root()->add_child(node);

	CHECK_EQ(Stage::get_singleton()->get_root()->get_child_count(), 1);
	CHECK_EQ(Stage::get_singleton()->get_entity_count(), 2);

	CHECK(node->get_name() != StringName());
	CHECK(node->is_inside_stage());
	CHECK_EQ(Stage::get_singleton()->get_root(), node->get_parent());
	CHECK_FALSE(node->get_path().is_empty());
	CHECK_EQ(node->get_child_count(), 0);

	SUBCASE("Entity should be accessible as first child") {
		Entity *child = Stage::get_singleton()->get_root()->get_child(0);
		CHECK_EQ(child, node);
	}

	SUBCASE("Entity should be accessible via the node path") {
		Entity *child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(node->get_path());
		CHECK_EQ(child_by_path, node);

		child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(EntityPath("Entity"));
		CHECK_EQ(child_by_path, nullptr);

		child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(EntityPath("/root/Entity"));
		CHECK_EQ(child_by_path, nullptr);

		node->set_name("Entity");

		child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(node->get_path());
		CHECK_EQ(child_by_path, node);

		child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(EntityPath("Entity"));
		CHECK_EQ(child_by_path, node);

		child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(EntityPath("/root/Entity"));
		CHECK_EQ(child_by_path, node);
	}

	SUBCASE("Entity should be accessible via group") {
		Vector<Entity *> nodes = Stage::get_singleton()->get_entities_in_group("nodes");
		CHECK(nodes.is_empty());

		node->add_to_group("nodes");

		nodes = Stage::get_singleton()->get_entities_in_group("nodes");
		CHECK_EQ(nodes.size(), 1);
		Entity *E = nodes.get(0);
		CHECK_EQ(E, node);
	}

	SUBCASE("Entity should be possible to find") {
		Entity *child = Stage::get_singleton()->get_root()->find_child("Entity", true, false);
		CHECK_EQ(child, nullptr);

		node->set_name("Entity");

		child = Stage::get_singleton()->get_root()->find_child("Entity", true, false);
		CHECK_EQ(child, node);
	}

	SUBCASE("Entity should be possible to remove") {
		Stage::get_singleton()->get_root()->remove_child(node);

		CHECK_EQ(Stage::get_singleton()->get_root()->get_child_count(), 0);
		CHECK_EQ(Stage::get_singleton()->get_entity_count(), 1);

		CHECK_FALSE(node->is_inside_stage());
		CHECK_EQ(node->get_parent(), nullptr);
		ERR_PRINT_OFF;
		CHECK(node->get_path().is_empty());
		ERR_PRINT_ON;
	}

	SUBCASE("Entity should be possible to move") {
		Stage::get_singleton()->get_root()->move_child(node, 0);

		Entity *child = Stage::get_singleton()->get_root()->get_child(0);
		CHECK_EQ(child, node);
		CHECK(node->is_inside_stage());
	}

	SUBCASE("Entity should be possible to reparent") {
		node->reparent(Stage::get_singleton()->get_root());

		Entity *child = Stage::get_singleton()->get_root()->get_child(0);
		CHECK_EQ(child, node);
		CHECK(node->is_inside_stage());
	}

	SUBCASE("Entity should be possible to duplicate") {
		node->set_name("MyName");

		Entity *duplicate = node->duplicate();

		CHECK_FALSE(node == duplicate);
		CHECK_FALSE(duplicate->is_inside_stage());
		CHECK_EQ(duplicate->get_name(), node->get_name());

		memdelete(duplicate);
	}

	memdelete(node);
}

TEST_CASE("[Stage][Entity] Testing node operations with a more complex simple scene tree") {
	Entity *node1 = memnew(Entity);
	Entity *node2 = memnew(Entity);
	Entity *node1_1 = memnew(Entity);
	Entity *root = Stage::get_singleton()->get_root();

	root->add_child(node1);
	root->add_child(node2);

	node1->add_child(node1_1);

	CHECK(node1_1->is_inside_stage());
	CHECK_EQ(node1_1->get_parent(), node1);
	CHECK_EQ(node1->get_child_count(), 1);

	CHECK_EQ(root->is_ancestor_of(node1), true);
	CHECK_EQ(root->is_ancestor_of(node2), true);
	CHECK_EQ(root->is_ancestor_of(node1_1), true);
	CHECK_EQ(node1->is_ancestor_of(node1), false);
	CHECK_EQ(node1->is_ancestor_of(node1_1), true);
	CHECK_EQ(node2->is_ancestor_of(node1_1), false);
	CHECK_EQ(node2->is_ancestor_of(node1), false);

	CHECK_EQ(Stage::get_singleton()->get_root()->get_child_count(), 2);
	CHECK_EQ(Stage::get_singleton()->get_entity_count(), 4);

	SUBCASE("Nodes should be accessible via get_child(..)") {
		Entity *child1 = Stage::get_singleton()->get_root()->get_child(0);
		CHECK_EQ(child1, node1);

		Entity *child2 = Stage::get_singleton()->get_root()->get_child(1);
		CHECK_EQ(child2, node2);

		Entity *child1_1 = node1->get_child(0);
		CHECK_EQ(child1_1, node1_1);
	}

	SUBCASE("Removed nodes should also remove their children from the scene tree") {
		// Should also remove node1_1 from the scene tree.
		Stage::get_singleton()->get_root()->remove_child(node1);

		CHECK_EQ(node1->get_child_count(), 1);

		CHECK_EQ(Stage::get_singleton()->get_root()->get_child_count(), 1);
		CHECK_EQ(Stage::get_singleton()->get_entity_count(), 2);

		// First child should now be the second node.
		Entity *child1 = Stage::get_singleton()->get_root()->get_child(0);
		CHECK_EQ(child1, node2);
	}

	SUBCASE("Removed children nodes should not affect their parent in the scene tree") {
		node1->remove_child(node1_1);

		CHECK_EQ(node1_1->get_parent(), nullptr);
		CHECK_EQ(node1->get_child_count(), 0);

		CHECK_EQ(Stage::get_singleton()->get_entity_count(), 3);
	}

	SUBCASE("Nodes should be in the expected order when a node is moved to the back") {
		Stage::get_singleton()->get_root()->move_child(node1, 1);

		Entity *child1 = Stage::get_singleton()->get_root()->get_child(0);
		CHECK_EQ(child1, node2);

		Entity *child2 = Stage::get_singleton()->get_root()->get_child(1);
		CHECK_EQ(child2, node1);
	}

	SUBCASE("Nodes should be in the expected order when a node is moved to the front") {
		Stage::get_singleton()->get_root()->move_child(node2, 0);

		Entity *child1 = Stage::get_singleton()->get_root()->get_child(0);
		CHECK_EQ(child1, node2);

		Entity *child2 = Stage::get_singleton()->get_root()->get_child(1);
		CHECK_EQ(child2, node1);
	}

	SUBCASE("Nodes should be in the expected order when reparented (remove/add)") {
		CHECK_EQ(node2->get_child_count(), 0);

		node1->remove_child(node1_1);
		CHECK_EQ(node1->get_child_count(), 0);
		CHECK_EQ(node1_1->get_parent(), nullptr);

		node2->add_child(node1_1);
		CHECK_EQ(node2->get_child_count(), 1);
		CHECK_EQ(node1_1->get_parent(), node2);

		Entity *child = node2->get_child(0);
		CHECK_EQ(child, node1_1);

		CHECK_EQ(Stage::get_singleton()->get_root()->get_child_count(), 2);
		CHECK_EQ(Stage::get_singleton()->get_entity_count(), 4);
	}

	SUBCASE("Nodes should be in the expected order when reparented") {
		CHECK_EQ(node2->get_child_count(), 0);

		node1_1->reparent(node2);

		CHECK_EQ(node1->get_child_count(), 0);
		CHECK_EQ(node2->get_child_count(), 1);
		CHECK_EQ(node1_1->get_parent(), node2);

		Entity *child = node2->get_child(0);
		CHECK_EQ(child, node1_1);

		CHECK_EQ(Stage::get_singleton()->get_root()->get_child_count(), 2);
		CHECK_EQ(Stage::get_singleton()->get_entity_count(), 4);
	}

	SUBCASE("Nodes should be possible to find") {
		Entity *child = Stage::get_singleton()->get_root()->find_child("NestedNode", true, false);
		CHECK_EQ(child, nullptr);

		TypedArray<Entity> children = Stage::get_singleton()->get_root()->find_children("NestedNode", "", true, false);
		CHECK_EQ(children.size(), 0);

		node1->set_name("Node1");
		node2->set_name("Node2");
		node1_1->set_name("NestedNode");

		child = Stage::get_singleton()->get_root()->find_child("NestedNode", true, false);
		CHECK_EQ(child, node1_1);

		children = Stage::get_singleton()->get_root()->find_children("NestedNode", "", true, false);
		CHECK_EQ(children.size(), 1);
		CHECK_EQ(Object::cast_to<Entity>(children[0]), node1_1);

		// First node that matches with the name is node1.
		child = Stage::get_singleton()->get_root()->find_child("Node?", true, false);
		CHECK_EQ(child, node1);

		children = Stage::get_singleton()->get_root()->find_children("Node?", "", true, false);
		CHECK_EQ(children.size(), 2);
		CHECK_EQ(Object::cast_to<Entity>(children[0]), node1);
		CHECK_EQ(Object::cast_to<Entity>(children[1]), node2);

		Stage::get_singleton()->get_root()->move_child(node2, 0);

		// It should be node2, as it is now the first one in the tree.
		child = Stage::get_singleton()->get_root()->find_child("Node?", true, false);
		CHECK_EQ(child, node2);

		children = Stage::get_singleton()->get_root()->find_children("Node?", "", true, false);
		CHECK_EQ(children.size(), 2);
		CHECK_EQ(Object::cast_to<Entity>(children[0]), node2);
		CHECK_EQ(Object::cast_to<Entity>(children[1]), node1);
	}

	SUBCASE("Nodes should be accessible via their node path") {
		Entity *child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(node1->get_path());
		CHECK_EQ(child_by_path, node1);

		child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(node2->get_path());
		CHECK_EQ(child_by_path, node2);

		child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(node1_1->get_path());
		CHECK_EQ(child_by_path, node1_1);

		node1->set_name("Node1");
		node1_1->set_name("NestedNode");

		child_by_path = node1->get_entity_or_null(EntityPath("NestedNode"));
		CHECK_EQ(child_by_path, node1_1);

		child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(EntityPath("/root/Node1/NestedNode"));
		CHECK_EQ(child_by_path, node1_1);

		child_by_path = Stage::get_singleton()->get_root()->get_entity_or_null(EntityPath("Node1/NestedNode"));
		CHECK_EQ(child_by_path, node1_1);
	}

	SUBCASE("Nodes should be accessible via their groups") {
		Vector<Entity *> nodes = Stage::get_singleton()->get_entities_in_group("nodes");
		CHECK(nodes.is_empty());

		nodes = Stage::get_singleton()->get_entities_in_group("other_nodes");
		CHECK(nodes.is_empty());

		node1->add_to_group("nodes");
		node2->add_to_group("other_nodes");
		node1_1->add_to_group("nodes");
		node1_1->add_to_group("other_nodes");

		nodes = Stage::get_singleton()->get_entities_in_group("nodes");
		CHECK_EQ(nodes.size(), 2);

		Entity *E = nodes.get(0);
		CHECK_EQ(E, node1);
		E = nodes.get(1);
		CHECK_EQ(E, node1_1);

		// Clear and try again with the other group.
		nodes.clear();

		nodes = Stage::get_singleton()->get_entities_in_group("other_nodes");
		CHECK_EQ(nodes.size(), 2);

		E = nodes.get(0);
		CHECK_EQ(E, node1_1);
		E = nodes.get(1);
		CHECK_EQ(E, node2);

		// Clear and try again with the other group and one node removed.
		nodes.clear();

		node1->remove_from_group("nodes");
		nodes = Stage::get_singleton()->get_entities_in_group("nodes");
		CHECK_EQ(nodes.size(), 1);

		E = nodes.get(0);
		CHECK_EQ(E, node1_1);
	}

	SUBCASE("Nodes added as siblings of another node should be right next to it") {
		node1->remove_child(node1_1);

		node1->add_sibling(node1_1);

		CHECK_EQ(Stage::get_singleton()->get_root()->get_child_count(), 3);
		CHECK_EQ(Stage::get_singleton()->get_entity_count(), 4);

		CHECK_EQ(Stage::get_singleton()->get_root()->get_child(0), node1);
		CHECK_EQ(Stage::get_singleton()->get_root()->get_child(1), node1_1);
		CHECK_EQ(Stage::get_singleton()->get_root()->get_child(2), node2);
	}

	SUBCASE("Replaced nodes should be be removed and the replacing node added") {
		Stage::get_singleton()->get_root()->remove_child(node2);

		node1->replace_by(node2);

		CHECK_EQ(Stage::get_singleton()->get_root()->get_child_count(), 1);
		CHECK_EQ(Stage::get_singleton()->get_entity_count(), 3);

		CHECK_FALSE(node1->is_inside_stage());
		CHECK(node2->is_inside_stage());

		CHECK_EQ(node1->get_parent(), nullptr);
		CHECK_EQ(node2->get_parent(), Stage::get_singleton()->get_root());
		CHECK_EQ(node2->get_child_count(), 1);
		CHECK_EQ(node2->get_child(0), node1_1);
	}

	SUBCASE("Replacing nodes should keep the groups of the replaced nodes") {
		Stage::get_singleton()->get_root()->remove_child(node2);

		node1->add_to_group("nodes");
		node1->replace_by(node2, true);

		Vector<Entity *> nodes = Stage::get_singleton()->get_entities_in_group("nodes");
		CHECK_EQ(nodes.size(), 1);

		Entity *E = nodes.get(0);
		CHECK_EQ(E, node2);
	}

	SUBCASE("Duplicating a node should also duplicate the children") {
		node1->set_name("MyName1");
		node1_1->set_name("MyName1_1");
		Entity *duplicate1 = node1->duplicate();

		CHECK_EQ(duplicate1->get_child_count(), node1->get_child_count());
		Entity *duplicate1_1 = duplicate1->get_child(0);

		CHECK_EQ(duplicate1_1->get_child_count(), node1_1->get_child_count());

		CHECK_EQ(duplicate1->get_name(), node1->get_name());
		CHECK_EQ(duplicate1_1->get_name(), node1_1->get_name());

		CHECK_FALSE(duplicate1->is_inside_stage());
		CHECK_FALSE(duplicate1_1->is_inside_stage());

		memdelete(duplicate1_1);
		memdelete(duplicate1);
	}

	memdelete(node1_1);
	memdelete(node1);
	memdelete(node2);
}

TEST_CASE("[Stage][Entity] Duplicating node with internal children") {
	ORBLIT_REGISTER_CLASS(TestNode);

	TestNode *node = memnew(TestNode);
	Entity *child = memnew(Entity);
	child->set_name("Child");
	node->add_child(child);

	int child_count = node->get_child_count();

	Entity *dup = node->duplicate();
	CHECK(dup->get_child_count() == child_count);
	CHECK(dup->has_entity(String("Child")));

	memdelete(node);
	memdelete(dup);
}

TEST_CASE("[Stage][Entity]Exported node checks") {
	TestNode *node = memnew(TestNode);
	Stage::get_singleton()->get_root()->add_child(node);

	Entity *child = memnew(Entity);
	child->set_name("Child");
	node->add_child(child);
	child->set_owner(node);

	Entity *child2 = memnew(Entity);
	child2->set_name("Child2");
	node->add_child(child2);
	child2->set_owner(node);

	Array children;
	children.append(child);

	node->set("exported_node", child);
	node->set("exported_nodes", children);

	SUBCASE("Property of duplicated node should point to duplicated child") {
		ORBLIT_REGISTER_CLASS(TestNode);

		TestNode *dup = Object::cast_to<TestNode>(node->duplicate());
		Entity *new_exported = Object::cast_to<Entity>(dup->get("exported_node"));
		CHECK(new_exported == dup->get_child(0, false));

		memdelete(dup);
	}

#ifdef TOOLS_ENABLED
	SUBCASE("Saving instance with exported nodes should not store the unchanged property") {
		Ref<Prefab> ps;
		ps.instantiate();
		ps->pack(node);

		String scene_path = TestUtils::get_temp_path("test_scene.oscn");
		ps->set_path(scene_path);

		Entity *root = memnew(Entity);

		Entity *sub_child = ps->instantiate(Prefab::GEN_EDIT_STATE_MAIN);
		root->add_child(sub_child);
		sub_child->set_owner(root);

		Ref<Prefab> ps2;
		ps2.instantiate();
		ps2->pack(root);

		scene_path = TestUtils::get_temp_path("new_test_scene.oscn");
		ResourceSaver::save(ps2, scene_path);
		memdelete(root);

		bool is_wrong = false;
		Ref<FileAccess> fa = FileAccess::open(scene_path, FileAccess::READ);
		while (!fa->eof_reached()) {
			const String line = fa->get_line();
			if (line.begins_with("exported_node")) {
				// The property was saved, while it shouldn't.
				is_wrong = true;
				break;
			}
		}
		CHECK_FALSE(is_wrong);
	}

	SUBCASE("Saving instance with exported nodes should store property if changed") {
		Ref<Prefab> ps;
		ps.instantiate();
		ps->pack(node);

		String scene_path = TestUtils::get_temp_path("test_scene.oscn");
		ps->set_path(scene_path);

		Entity *root = memnew(Entity);

		Entity *sub_child = ps->instantiate(Prefab::GEN_EDIT_STATE_MAIN);
		root->add_child(sub_child);
		sub_child->set_owner(root);

		sub_child->set("exported_node", sub_child->get_child(1, false));

		children = Array();
		children.append(sub_child->get_child(1, false));
		sub_child->set("exported_nodes", children);

		Ref<Prefab> ps2;
		ps2.instantiate();
		ps2->pack(root);

		scene_path = TestUtils::get_temp_path("new_test_scene2.oscn");
		ResourceSaver::save(ps2, scene_path);
		memdelete(root);

		int stored_properties = 0;
		Ref<FileAccess> fa = FileAccess::open(scene_path, FileAccess::READ);
		while (!fa->eof_reached()) {
			const String line = fa->get_line();
			if (line.begins_with("exported_node")) {
				stored_properties++;
			}
		}
		CHECK_EQ(stored_properties, 2);
	}
#endif // TOOLS_ENABLED

	memdelete(node);
}

TEST_CASE("[Entity] Processing checks") {
	Entity *node = memnew(Entity);

	SUBCASE("Processing") {
		CHECK_FALSE(node->is_frame_enabled());

		node->set_frame_enabled(true);

		CHECK(node->is_frame_enabled());

		node->set_frame_enabled(false);

		CHECK_FALSE(node->is_frame_enabled());
	}

	SUBCASE("Physics processing") {
		CHECK_FALSE(node->is_step_enabled());

		node->set_step_enabled(true);

		CHECK(node->is_step_enabled());

		node->set_step_enabled(false);

		CHECK_FALSE(node->is_step_enabled());
	}

	SUBCASE("Unhandled input processing") {
		CHECK_FALSE(node->is_unhandled_input_enabled());

		node->set_unhandled_input_enabled(true);

		CHECK(node->is_unhandled_input_enabled());

		node->set_unhandled_input_enabled(false);

		CHECK_FALSE(node->is_unhandled_input_enabled());
	}

	SUBCASE("Input processing") {
		CHECK_FALSE(node->is_input_enabled());

		node->set_input_enabled(true);

		CHECK(node->is_input_enabled());

		node->set_input_enabled(false);

		CHECK_FALSE(node->is_input_enabled());
	}

	SUBCASE("Unhandled key input processing") {
		CHECK_FALSE(node->is_unhandled_key_input_enabled());

		node->set_unhandled_key_input_enabled(true);

		CHECK(node->is_unhandled_key_input_enabled());

		node->set_unhandled_key_input_enabled(false);

		CHECK_FALSE(node->is_unhandled_key_input_enabled());
	}

	SUBCASE("Shortcut input processing") {
		CHECK_FALSE(node->is_shortcut_input_enabled());

		node->set_shortcut_input_enabled(true);

		CHECK(node->is_shortcut_input_enabled());

		node->set_shortcut_input_enabled(false);

		CHECK_FALSE(node->is_shortcut_input_enabled());
	}

	SUBCASE("Internal processing") {
		CHECK_FALSE(node->is_internal_frame_enabled());

		node->set_internal_frame_enabled(true);

		CHECK(node->is_internal_frame_enabled());

		node->set_internal_frame_enabled(false);

		CHECK_FALSE(node->is_internal_frame_enabled());
	}

	SUBCASE("Process priority") {
		CHECK_EQ(0, node->get_run_priority());

		node->set_run_priority(1);

		CHECK_EQ(1, node->get_run_priority());
	}

	SUBCASE("Physics process priority") {
		CHECK_EQ(0, node->get_run_physics_priority());

		node->set_run_physics_priority(1);

		CHECK_EQ(1, node->get_run_physics_priority());
	}

	memdelete(node);
}

TEST_CASE("[Stage][Entity] Test the processing") {
	TestNode *node = memnew(TestNode);
	Stage::get_singleton()->get_root()->add_child(node);

	SUBCASE("No process") {
		CHECK_EQ(0, node->process_counter);
		CHECK_EQ(0, node->physics_process_counter);
	}

	SUBCASE("Process") {
		node->set_frame_enabled(true);
		Stage::get_singleton()->process(0);

		CHECK_EQ(1, node->process_counter);
		CHECK_EQ(0, node->physics_process_counter);
		CHECK_EQ(0, node->internal_process_counter);
		CHECK_EQ(0, node->internal_physics_process_counter);
	}

	SUBCASE("Physics process") {
		node->set_step_enabled(true);
		Stage::get_singleton()->physics_process(0);

		CHECK_EQ(0, node->process_counter);
		CHECK_EQ(1, node->physics_process_counter);
		CHECK_EQ(0, node->internal_process_counter);
		CHECK_EQ(0, node->internal_physics_process_counter);
	}

	SUBCASE("Normal and physics process") {
		node->set_frame_enabled(true);
		node->set_step_enabled(true);
		Stage::get_singleton()->process(0);
		Stage::get_singleton()->physics_process(0);

		CHECK_EQ(1, node->process_counter);
		CHECK_EQ(1, node->physics_process_counter);
		CHECK_EQ(0, node->internal_process_counter);
		CHECK_EQ(0, node->internal_physics_process_counter);
	}

	SUBCASE("Internal, normal and physics process") {
		node->set_internal_frame_enabled(true);
		node->set_internal_step_enabled(true);
		Stage::get_singleton()->process(0);
		Stage::get_singleton()->physics_process(0);

		CHECK_EQ(0, node->process_counter);
		CHECK_EQ(0, node->physics_process_counter);
		CHECK_EQ(1, node->internal_process_counter);
		CHECK_EQ(1, node->internal_physics_process_counter);
	}

	SUBCASE("All processing") {
		node->set_frame_enabled(true);
		node->set_step_enabled(true);
		node->set_internal_frame_enabled(true);
		node->set_internal_step_enabled(true);
		Stage::get_singleton()->process(0);
		Stage::get_singleton()->physics_process(0);

		CHECK_EQ(1, node->process_counter);
		CHECK_EQ(1, node->physics_process_counter);
		CHECK_EQ(1, node->internal_process_counter);
		CHECK_EQ(1, node->internal_physics_process_counter);
	}

	SUBCASE("All processing twice") {
		node->set_frame_enabled(true);
		node->set_step_enabled(true);
		node->set_internal_frame_enabled(true);
		node->set_internal_step_enabled(true);
		Stage::get_singleton()->process(0);
		Stage::get_singleton()->physics_process(0);
		Stage::get_singleton()->process(0);
		Stage::get_singleton()->physics_process(0);

		CHECK_EQ(2, node->process_counter);
		CHECK_EQ(2, node->physics_process_counter);
		CHECK_EQ(2, node->internal_process_counter);
		CHECK_EQ(2, node->internal_physics_process_counter);
	}

	SUBCASE("Enable and disable processing") {
		node->set_frame_enabled(true);
		node->set_step_enabled(true);
		node->set_internal_frame_enabled(true);
		node->set_internal_step_enabled(true);
		Stage::get_singleton()->process(0);
		Stage::get_singleton()->physics_process(0);

		node->set_frame_enabled(false);
		node->set_step_enabled(false);
		node->set_internal_frame_enabled(false);
		node->set_internal_step_enabled(false);
		Stage::get_singleton()->process(0);
		Stage::get_singleton()->physics_process(0);

		CHECK_EQ(1, node->process_counter);
		CHECK_EQ(1, node->physics_process_counter);
		CHECK_EQ(1, node->internal_process_counter);
		CHECK_EQ(1, node->internal_physics_process_counter);
	}

	memdelete(node);
}

TEST_CASE("[Stage][Entity] Test the process priority") {
	List<Entity *> process_order;

	TestNode *node = memnew(TestNode);
	node->callback_list = &process_order;
	Stage::get_singleton()->get_root()->add_child(node);

	TestNode *node2 = memnew(TestNode);
	node2->callback_list = &process_order;
	Stage::get_singleton()->get_root()->add_child(node2);

	TestNode *node3 = memnew(TestNode);
	node3->callback_list = &process_order;
	Stage::get_singleton()->get_root()->add_child(node3);

	TestNode *node4 = memnew(TestNode);
	node4->callback_list = &process_order;
	Stage::get_singleton()->get_root()->add_child(node4);

	SUBCASE("Process priority") {
		node->set_frame_enabled(true);
		node->set_run_priority(20);
		node2->set_frame_enabled(true);
		node2->set_run_priority(10);
		node3->set_frame_enabled(true);
		node3->set_run_priority(40);
		node4->set_frame_enabled(true);
		node4->set_run_priority(30);

		Stage::get_singleton()->process(0);

		CHECK_EQ(4, process_order.size());
		List<Entity *>::Element *E = process_order.front();
		CHECK_EQ(E->get(), node2);
		E = E->next();
		CHECK_EQ(E->get(), node);
		E = E->next();
		CHECK_EQ(E->get(), node4);
		E = E->next();
		CHECK_EQ(E->get(), node3);
	}

	SUBCASE("Physics process priority") {
		node->set_step_enabled(true);
		node->set_run_physics_priority(20);
		node2->set_step_enabled(true);
		node2->set_run_physics_priority(10);
		node3->set_step_enabled(true);
		node3->set_run_physics_priority(40);
		node4->set_step_enabled(true);
		node4->set_run_physics_priority(30);

		Stage::get_singleton()->physics_process(0);

		CHECK_EQ(4, process_order.size());
		List<Entity *>::Element *E = process_order.front();
		CHECK_EQ(E->get(), node2);
		E = E->next();
		CHECK_EQ(E->get(), node);
		E = E->next();
		CHECK_EQ(E->get(), node4);
		E = E->next();
		CHECK_EQ(E->get(), node3);
	}

	memdelete(node);
	memdelete(node2);
	memdelete(node3);
	memdelete(node4);
}

} // namespace TestNode
