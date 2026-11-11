/**************************************************************************/
/*  test_animation_player.cpp                                             */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_animation_player)

#include "scene/animation/animation_player.h"
#include "scene/resources/animation.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

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

TEST_CASE("[Stage][AnimationPlayer] The current length follows an animation in a named library") {
	Ref<Animation> animation;
	animation.instantiate();
	animation->set_length(1.0);
	Ref<AnimationLibrary> library;
	library.instantiate();
	library->add_animation("clip", animation);

	AnimationPlayer *animation_player = memnew(AnimationPlayer);
	Stage::get_singleton()->get_root()->add_child(animation_player);
	animation_player->add_animation_library("named", library);
	animation_player->play("named/clip");
	animation_player->pause();

	animation->set_length(10.0);

	CHECK(animation_player->get_current_animation_length() == doctest::Approx(10.0));

	memdelete(animation_player);
}

TEST_CASE("[Stage][AnimationPlayer] A capture track with a missing target is skipped") {
	Ref<Animation> animation;
	animation.instantiate();
	animation->set_length(1.0);
	const int track = animation->add_track(Animation::TYPE_VALUE);
	animation->track_set_path(track, EntityPath("Missing:position"));
	animation->value_track_set_update_mode(track, Animation::UPDATE_CAPTURE);
	animation->track_insert_key(track, 0.0, Vector2());
	Ref<AnimationLibrary> library;
	library.instantiate();
	library->add_animation("clip", animation);

	AnimationPlayer *animation_player = memnew(AnimationPlayer);
	Stage::get_singleton()->get_root()->add_child(animation_player);
	animation_player->add_animation_library("", library);

	ERR_PRINT_OFF
	animation_player->capture("clip", 1.0);
	ERR_PRINT_ON

	memdelete(animation_player);
}

} // namespace TestAnimationPlayer
