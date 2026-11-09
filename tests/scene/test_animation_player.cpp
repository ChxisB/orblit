/**************************************************************************/
/*  test_animation_player.cpp                                             */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_animation_player)

#include "scene/animation/animation_player.h"
#include "scene/resources/animation.h"

namespace TestAnimationPlayer {

TEST_CASE("[AnimationPlayer] get & set default_blend_time") {
	AnimationPlayer *animation_player = memnew(AnimationPlayer);
	animation_player->set_default_blend_time(4.0);

	CHECK(animation_player->get_default_blend_time() == doctest::Approx(4.0f));
	memdelete(animation_player);
}

TEST_CASE("[AnimationPlayer] get & set blend_time") {
	String anim1 = "animation1";
	String anim2 = "animation2";
	const Ref<Animation> animation1 = memnew(Animation);
	const Ref<Animation> animation2 = memnew(Animation);
	const Ref<AnimationLibrary> animation_library = memnew(AnimationLibrary);
	animation_library->add_animation(anim1, animation1);
	animation_library->add_animation(anim2, animation2);

	AnimationPlayer *animation_player = memnew(AnimationPlayer);
	animation_player->add_animation_library("", animation_library);

	animation_player->set_blend_time(anim1, anim2, 4.0);
	CHECK(animation_player->get_blend_time(anim1, anim2) == doctest::Approx(4.0f));
	memdelete(animation_player);
}

} // namespace TestAnimationPlayer
