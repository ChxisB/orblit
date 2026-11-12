/**************************************************************************/
/*  test_animation_state_machine.cpp                                      */

#include "tests/test_macros.h"
#include "tests/test_tools.h"

TEST_FORCE_LINK(test_animation_state_machine)

#include "scene/animation/animation_blend_tree.h"
#include "scene/animation/animation_node_state_machine.h"
#include "scene/animation/animation_tree.h"
#include "scene/resources/animation.h"
#include "scene/resources/animation_library.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestAnimationStateMachine {

static Ref<AnimationNodeStateMachineTransition> make_auto_transition() {
	Ref<AnimationNodeStateMachineTransition> transition;
	transition.instantiate();
	transition->set_advance_mode(AnimationNodeStateMachineTransition::ADVANCE_MODE_AUTO);
	return transition;
}

static Ref<AnimationNodeAnimation> make_animation_node(const StringName &p_animation) {
	Ref<AnimationNodeAnimation> node;
	node.instantiate();
	node->set_animation(p_animation);
	return node;
}

TEST_CASE("[Stage][AnimationNodeStateMachine] A grouped machine hands over to its parent without a condition") {
	Ref<AnimationLibrary> library;
	library.instantiate();
	for (const StringName &name : { StringName("inner"), StringName("after") }) {
		Ref<Animation> animation;
		animation.instantiate();
		animation->set_length(1.0);
		library->add_animation(name, animation);
	}

	Ref<AnimationNodeStateMachine> group;
	group.instantiate();
	group->set_state_machine_type(AnimationNodeStateMachine::STATE_MACHINE_TYPE_GROUPED);
	group->add_node("Inner", make_animation_node("inner"));
	group->add_transition("Start", "Inner", make_auto_transition());
	group->add_transition("Inner", "End", make_auto_transition());

	Ref<AnimationNodeStateMachine> root;
	root.instantiate();
	root->add_node("Group", group);
	root->add_node("After", make_animation_node("after"));
	root->add_transition("Start", "Group", make_auto_transition());
	root->add_transition("Group", "After", make_auto_transition());

	AnimationTree *tree = memnew(AnimationTree);
	Stage::get_singleton()->get_root()->add_child(tree);
	tree->add_animation_library("", library);
	tree->set_root_animation_node(root);
	tree->set_active(true);

	Ref<AnimationNodeStateMachinePlayback> playback = tree->get("parameters/playback");
	REQUIRE(playback.is_valid());

	ErrorDetector errors;
	for (int i = 0; i < 20; i++) {
		tree->advance(0.1);
	}

	CHECK_FALSE(errors.has_error);
	CHECK(playback->get_current_node() == StringName("After"));

	memdelete(tree);
}

} // namespace TestAnimationStateMachine
