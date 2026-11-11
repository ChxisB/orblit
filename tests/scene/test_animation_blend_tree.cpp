/**************************************************************************/
/*  test_animation_blend_tree.cpp                                         */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_animation_blend_tree)

#include "scene/animation/animation_blend_tree.h"
#include "scene/animation/animation_tree.h"
#include "scene/resources/animation.h"
#include "scene/resources/animation_library.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestAnimationBlendTree {

TEST_CASE("[Stage][AnimationBlendTree] Create AnimationBlendTree and add AnimationNode") {
	Ref<AnimationNodeBlendTree> blend_tree;
	blend_tree.instantiate();

	// Test initial state.
	CHECK(blend_tree->has_node("output"));
	CHECK_EQ(blend_tree->get_graph_offset(), Vector2(0, 0));
	CHECK_EQ(blend_tree->get_node_list().size(), 1);

	// Test adding animation node.
	Ref<AnimationNodeAnimation> anim_node;
	anim_node.instantiate();
	anim_node->set_animation(StringName("test_animation"));
	Vector2 position(100, 100);
	blend_tree->add_node("test_node", anim_node, position);

	// Test node existence.
	CHECK(blend_tree->has_node("test_node"));
	CHECK_EQ(blend_tree->get_node("test_node"), anim_node);
	CHECK_EQ(blend_tree->get_node_position("test_node"), position);

	// Test node connection on port 0.
	CHECK_EQ(blend_tree->can_connect_node("output", 0, "test_node"), AnimationNodeBlendTree::CONNECTION_OK);
	blend_tree->connect_node("output", 0, "test_node");

	const LocalVector<StringName> *connections = blend_tree->get_node_connection_array("output");
	CHECK_EQ(connections->size(), 1);
	CHECK_EQ(connections->operator[](0), StringName("test_node"));

	// Test node rename.
	blend_tree->rename_node("test_node", "renamed_node");
	CHECK_FALSE(blend_tree->has_node("test_node"));
	CHECK(blend_tree->has_node("renamed_node"));

	connections = blend_tree->get_node_connection_array("output");
	CHECK_EQ(connections->operator[](0), StringName("renamed_node"));

	// Test node removal.
	blend_tree->remove_node("renamed_node");
	CHECK_FALSE(blend_tree->has_node("renamed_node"));

	connections = blend_tree->get_node_connection_array("output");
	CHECK_EQ(connections->operator[](0), StringName());
}

TEST_CASE("[Stage][AnimationNodeAnimation] The delta stops at the end of a clip that does not loop") {
	Ref<Animation> animation;
	animation.instantiate();
	animation->set_length(1.0);
	Ref<AnimationLibrary> library;
	library.instantiate();
	library->add_animation("clip", animation);

	Ref<AnimationNodeAnimation> anim_node;
	anim_node.instantiate();
	anim_node->set_animation("clip");
	Ref<AnimationNodeBlendTree> blend_tree;
	blend_tree.instantiate();
	blend_tree->add_node("clip_node", anim_node);
	blend_tree->connect_node("output", 0, "clip_node");

	AnimationTree *tree = memnew(AnimationTree);
	Stage::get_singleton()->get_root()->add_child(tree);
	tree->add_animation_library("", library);
	tree->set_root_animation_node(blend_tree);
	tree->set_active(true);

	// The first advance only seeks to the start.
	tree->advance(0.0);
	tree->advance(0.8);
	tree->advance(0.8);

	// The clip had 0.2 s left, so that is the time it moved.
	const double delta = tree->get("parameters/clip_node/current_delta");
	CHECK(delta == doctest::Approx(0.2));

	memdelete(tree);
}

} // namespace TestAnimationBlendTree
