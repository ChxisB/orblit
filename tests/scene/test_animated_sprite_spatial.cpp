/**************************************************************************/
/*  test_animated_sprite_spatial.cpp                                          */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_animated_sprite_spatial)

#ifndef SPATIAL_DISABLED

#include "scene/resources/sprite_frames.h"
#include "scene/spatial/sprite_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestAnimatedSprite3D {

TEST_CASE("[Stage][AnimatedSprite3D] A long frame after a short one is not skipped") {
	Ref<SpriteFrames> frames = memnew(SpriteFrames);
	frames->set_animation_speed("default", 1.0);
	frames->add_frame("default", Ref<Texture2D>(), 0.01);
	frames->add_frame("default", Ref<Texture2D>(), 99.0);
	frames->add_frame("default", Ref<Texture2D>(), 1.0);

	AnimatedSprite3D *sprite = memnew(AnimatedSprite3D);
	sprite->set_sprite_frames(frames);
	Stage::get_singleton()->get_root()->add_child(sprite);
	sprite->play("default");

	// The first frame ends after 0.01 s. The rest of the step belongs to the long frame.
	Stage::get_singleton()->process(0.5);

	CHECK(sprite->get_frame() == 1);
	CHECK(sprite->get_frame_progress() == doctest::Approx(0.49 / 99.0));

	memdelete(sprite);
}

} // namespace TestAnimatedSprite3D

#endif // SPATIAL_DISABLED
