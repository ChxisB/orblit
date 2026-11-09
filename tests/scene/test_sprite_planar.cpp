/**************************************************************************/
/*  test_sprite_planar.cpp                                                    */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_sprite_planar)

#include "scene/planar/sprite_planar.h"

namespace TestSprite2D {

TEST_CASE("[Stage][Sprite2D] Constructor") {
	Sprite2D *sprite_2d = memnew(Sprite2D);

	CHECK(sprite_2d->get_texture().is_null());
	CHECK_EQ(sprite_2d->get_offset(), Point2(0, 0));
	CHECK(sprite_2d->is_centered());
	CHECK_FALSE(sprite_2d->is_flipped_h());
	CHECK_FALSE(sprite_2d->is_flipped_v());
	CHECK_EQ(sprite_2d->get_hframes(), 1);
	CHECK_EQ(sprite_2d->get_vframes(), 1);
	CHECK_EQ(sprite_2d->get_frame(), 0);
	CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 0));
	CHECK_FALSE(sprite_2d->is_region_enabled());

	memdelete(sprite_2d);
}

TEST_CASE("[Stage][Sprite2D] Frames") {
	Sprite2D *sprite_2d = memnew(Sprite2D);

	SUBCASE("Invalid range") {
		ERR_PRINT_OFF;
		sprite_2d->set_frame(30);
		sprite_2d->set_frame(-1);
		ERR_PRINT_ON;
		CHECK(sprite_2d->get_frame() == 0);
	}

	SUBCASE("Base value") {
		sprite_2d->set_hframes(1);
		sprite_2d->set_vframes(1);
		CHECK(sprite_2d->get_frame() == 0);
	}

	SUBCASE("2x2 frames") {
		sprite_2d->set_hframes(2);
		sprite_2d->set_vframes(2);

		CHECK(sprite_2d->get_hframes() == 2);
		CHECK(sprite_2d->get_vframes() == 2);

		sprite_2d->set_frame(0);
		CHECK(sprite_2d->get_frame() == 0);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 0));

		sprite_2d->set_frame(1);
		CHECK(sprite_2d->get_frame() == 1);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(1, 0));

		sprite_2d->set_frame(2);
		CHECK(sprite_2d->get_frame() == 2);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 1));

		sprite_2d->set_frame(3);
		CHECK(sprite_2d->get_frame() == 3);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(1, 1));
	}

	SUBCASE("4x4 frames") {
		sprite_2d->set_hframes(4);
		sprite_2d->set_vframes(4);

		CHECK(sprite_2d->get_hframes() == 4);
		CHECK(sprite_2d->get_vframes() == 4);

		sprite_2d->set_frame(0);
		CHECK(sprite_2d->get_frame() == 0);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 0));

		sprite_2d->set_frame(2);
		CHECK(sprite_2d->get_frame() == 2);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(2, 0));

		sprite_2d->set_frame(4);
		CHECK(sprite_2d->get_frame() == 4);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 1));

		sprite_2d->set_frame(6);
		CHECK(sprite_2d->get_frame() == 6);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(2, 1));

		sprite_2d->set_frame(8);
		CHECK(sprite_2d->get_frame() == 8);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 2));

		sprite_2d->set_frame(10);
		CHECK(sprite_2d->get_frame() == 10);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(2, 2));

		sprite_2d->set_frame(12);
		CHECK(sprite_2d->get_frame() == 12);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 3));

		sprite_2d->set_frame(14);
		CHECK(sprite_2d->get_frame() == 14);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(2, 3));
	}

	SUBCASE("8x4 frames") {
		sprite_2d->set_hframes(8);
		sprite_2d->set_vframes(4);

		CHECK(sprite_2d->get_hframes() == 8);
		CHECK(sprite_2d->get_vframes() == 4);

		sprite_2d->set_frame(0);
		CHECK(sprite_2d->get_frame() == 0);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 0));

		sprite_2d->set_frame(4);
		CHECK(sprite_2d->get_frame() == 4);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(4, 0));

		sprite_2d->set_frame(8);
		CHECK(sprite_2d->get_frame() == 8);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 1));

		sprite_2d->set_frame(16);
		CHECK(sprite_2d->get_frame() == 16);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 2));

		sprite_2d->set_frame(31);
		CHECK(sprite_2d->get_frame() == 31);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(7, 3));
	}

	SUBCASE("100x100 frames") {
		sprite_2d->set_hframes(100);
		sprite_2d->set_vframes(100);

		CHECK(sprite_2d->get_hframes() == 100);
		CHECK(sprite_2d->get_vframes() == 100);

		sprite_2d->set_frame(0);
		CHECK(sprite_2d->get_frame() == 0);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(0, 0));

		sprite_2d->set_frame(60);
		CHECK(sprite_2d->get_frame() == 60);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(60, 0));

		sprite_2d->set_frame(120);
		CHECK(sprite_2d->get_frame() == 120);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(20, 1));

		sprite_2d->set_frame(240);
		CHECK(sprite_2d->get_frame() == 240);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(40, 2));

		sprite_2d->set_frame(360);
		CHECK(sprite_2d->get_frame() == 360);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(60, 3));

		sprite_2d->set_frame(2048);
		CHECK(sprite_2d->get_frame() == 2048);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(48, 20));

		sprite_2d->set_frame(8192);
		CHECK(sprite_2d->get_frame() == 8192);
		CHECK_EQ(sprite_2d->get_frame_coords(), Vector2i(92, 81));
	}

	memdelete(sprite_2d);
}

TEST_CASE("[Stage][Sprite2D] Flipping") {
	Sprite2D *sprite_2d = memnew(Sprite2D);

	SUBCASE("Both False") {
		CHECK_FALSE(sprite_2d->is_flipped_h());
		CHECK_FALSE(sprite_2d->is_flipped_v());
	}

	SUBCASE("Both True") {
		sprite_2d->set_flip_h(true);
		sprite_2d->set_flip_v(true);

		CHECK(sprite_2d->is_flipped_h());
		CHECK(sprite_2d->is_flipped_v());
	}

	SUBCASE("True False") {
		sprite_2d->set_flip_h(true);
		sprite_2d->set_flip_v(false);

		CHECK(sprite_2d->is_flipped_h());
		CHECK_FALSE(sprite_2d->is_flipped_v());
	}

	SUBCASE("False True") {
		sprite_2d->set_flip_h(false);
		sprite_2d->set_flip_v(true);

		CHECK_FALSE(sprite_2d->is_flipped_h());
		CHECK(sprite_2d->is_flipped_v());
	}

	memdelete(sprite_2d);
}

TEST_CASE("[Stage][Sprite2D] Offset") {
	Sprite2D *sprite_2d = memnew(Sprite2D);

	sprite_2d->set_offset(Point2(0.0, 0.0));
	CHECK(sprite_2d->get_offset() == Point2(0.0, 0.0));

	sprite_2d->set_offset(Point2(8.0, 8.0));
	CHECK(sprite_2d->get_offset() == Point2(8.0, 8.0));

	sprite_2d->set_offset(Point2(25.0, 50.0));
	CHECK(sprite_2d->get_offset() == Point2(25.0, 50.0));

	sprite_2d->set_offset(Point2(500.0, 250.0));
	CHECK(sprite_2d->get_offset() == Point2(500.0, 250.0));

	sprite_2d->set_offset(Point2(-8.0, -8.0));
	CHECK(sprite_2d->get_offset() == Point2(-8.0, -8.0));

	sprite_2d->set_offset(Point2(-25.0, -50.0));
	CHECK(sprite_2d->get_offset() == Point2(-25.0, -50.0));

	sprite_2d->set_offset(Point2(-500.0, -250.0));
	CHECK(sprite_2d->get_offset() == Point2(-500.0, -250.0));

	memdelete(sprite_2d);
}

} // namespace TestSprite2D
