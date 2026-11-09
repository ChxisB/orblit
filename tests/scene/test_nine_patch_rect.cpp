/**************************************************************************/
/*  test_nine_patch_rect.cpp                                              */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_nine_patch_rect)

#include "scene/resources/texture.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/nine_patch_rect.h"
#include "tests/signal_watcher.h"

namespace TestNinePatchRect {

TEST_CASE("[Stage][NinePatchRect] Default properties") {
	NinePatchRect *nine_patch = memnew(NinePatchRect);

	CHECK(nine_patch->get_texture().is_null());

	CHECK(nine_patch->get_patch_margin(SIDE_LEFT) == 0);
	CHECK(nine_patch->get_patch_margin(SIDE_TOP) == 0);
	CHECK(nine_patch->get_patch_margin(SIDE_RIGHT) == 0);
	CHECK(nine_patch->get_patch_margin(SIDE_BOTTOM) == 0);

	CHECK(nine_patch->get_region_rect() == Rect2(0, 0, 0, 0));
	CHECK(nine_patch->is_draw_center_enabled());

	CHECK(nine_patch->get_h_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_STRETCH);
	CHECK(nine_patch->get_v_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_STRETCH);

	// Set in the NinePatchRect constructor, unlike the Control default.
	CHECK(nine_patch->get_mouse_filter() == Control::MOUSE_FILTER_IGNORE);

	CHECK(nine_patch->get_combined_minimum_size() == Size2(0, 0));

	memdelete(nine_patch);
}

TEST_CASE("[Stage][NinePatchRect] Set/get texture") {
	NinePatchRect *nine_patch = memnew(NinePatchRect);

	Ref<Texture2D> texture;
	texture.instantiate();

	nine_patch->set_texture(texture);
	CHECK(nine_patch->get_texture() == texture);

	nine_patch->set_texture(Ref<Texture2D>());
	CHECK(nine_patch->get_texture().is_null());

	memdelete(nine_patch);
}

TEST_CASE("[Stage][NinePatchRect] texture_changed signal") {
	NinePatchRect *nine_patch = memnew(NinePatchRect);

	Ref<Texture2D> texture_a;
	texture_a.instantiate();
	Ref<Texture2D> texture_b;
	texture_b.instantiate();

	SIGNAL_WATCH(nine_patch, "texture_changed");
	Array empty_signal_args = { {} };

	SUBCASE("Setting a new texture emits the signal") {
		nine_patch->set_texture(texture_a);
		SIGNAL_CHECK("texture_changed", empty_signal_args);
	}

	SUBCASE("Setting the same texture does not re-emit") {
		nine_patch->set_texture(texture_a);
		SIGNAL_DISCARD("texture_changed");

		nine_patch->set_texture(texture_a);
		SIGNAL_CHECK_FALSE("texture_changed");
	}

	SUBCASE("Replacing one texture with another emits the signal") {
		nine_patch->set_texture(texture_a);
		SIGNAL_DISCARD("texture_changed");

		nine_patch->set_texture(texture_b);
		SIGNAL_CHECK("texture_changed", empty_signal_args);
	}

	SUBCASE("Clearing the texture emits the signal") {
		nine_patch->set_texture(texture_a);
		SIGNAL_DISCARD("texture_changed");

		nine_patch->set_texture(Ref<Texture2D>());
		SIGNAL_CHECK("texture_changed", empty_signal_args);
	}

	SIGNAL_UNWATCH(nine_patch, "texture_changed");
	memdelete(nine_patch);
}

TEST_CASE("[Stage][NinePatchRect] Patch margins") {
	NinePatchRect *nine_patch = memnew(NinePatchRect);

	SUBCASE("Each side can be set and read independently") {
		nine_patch->set_patch_margin(SIDE_LEFT, 5);
		nine_patch->set_patch_margin(SIDE_TOP, 10);
		nine_patch->set_patch_margin(SIDE_RIGHT, 15);
		nine_patch->set_patch_margin(SIDE_BOTTOM, 20);

		CHECK(nine_patch->get_patch_margin(SIDE_LEFT) == 5);
		CHECK(nine_patch->get_patch_margin(SIDE_TOP) == 10);
		CHECK(nine_patch->get_patch_margin(SIDE_RIGHT) == 15);
		CHECK(nine_patch->get_patch_margin(SIDE_BOTTOM) == 20);
	}

	SUBCASE("Updating one side does not affect the others") {
		nine_patch->set_patch_margin(SIDE_LEFT, 7);
		nine_patch->set_patch_margin(SIDE_RIGHT, 9);

		nine_patch->set_patch_margin(SIDE_LEFT, 42);

		CHECK(nine_patch->get_patch_margin(SIDE_LEFT) == 42);
		CHECK(nine_patch->get_patch_margin(SIDE_TOP) == 0);
		CHECK(nine_patch->get_patch_margin(SIDE_RIGHT) == 9);
		CHECK(nine_patch->get_patch_margin(SIDE_BOTTOM) == 0);
	}

	memdelete(nine_patch);
}

TEST_CASE("[Stage][NinePatchRect] Patch margins drive minimum size") {
	NinePatchRect *nine_patch = memnew(NinePatchRect);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(nine_patch);

	nine_patch->set_patch_margin(SIDE_LEFT, 10);
	nine_patch->set_patch_margin(SIDE_TOP, 20);
	nine_patch->set_patch_margin(SIDE_RIGHT, 30);
	nine_patch->set_patch_margin(SIDE_BOTTOM, 40);
	Stage::get_singleton()->process(0);

	// Minimum width is left + right; minimum height is top + bottom.
	CHECK(nine_patch->get_combined_minimum_size() == Size2(40, 60));

	nine_patch->set_patch_margin(SIDE_RIGHT, 0);
	Stage::get_singleton()->process(0);
	CHECK(nine_patch->get_combined_minimum_size() == Size2(10, 60));

	memdelete(nine_patch);
}

TEST_CASE("[Stage][NinePatchRect] Patch margin out-of-range index is guarded") {
	NinePatchRect *nine_patch = memnew(NinePatchRect);

	nine_patch->set_patch_margin(SIDE_LEFT, 11);
	nine_patch->set_patch_margin(SIDE_TOP, 22);
	nine_patch->set_patch_margin(SIDE_RIGHT, 33);
	nine_patch->set_patch_margin(SIDE_BOTTOM, 44);

	ERR_PRINT_OFF;
	nine_patch->set_patch_margin((Side)4, 999);
	const int out_of_range_value = nine_patch->get_patch_margin((Side)4);
	ERR_PRINT_ON;

	CHECK(out_of_range_value == 0);
	CHECK(nine_patch->get_patch_margin(SIDE_LEFT) == 11);
	CHECK(nine_patch->get_patch_margin(SIDE_TOP) == 22);
	CHECK(nine_patch->get_patch_margin(SIDE_RIGHT) == 33);
	CHECK(nine_patch->get_patch_margin(SIDE_BOTTOM) == 44);

	memdelete(nine_patch);
}

TEST_CASE("[Stage][NinePatchRect] Region rect and draw center") {
	NinePatchRect *nine_patch = memnew(NinePatchRect);

	SUBCASE("Region rect is set and retrieved") {
		const Rect2 region(8, 16, 32, 64);
		nine_patch->set_region_rect(region);
		CHECK(nine_patch->get_region_rect() == region);
	}

	SUBCASE("Draw center can be toggled") {
		nine_patch->set_draw_center(false);
		CHECK_FALSE(nine_patch->is_draw_center_enabled());

		nine_patch->set_draw_center(true);
		CHECK(nine_patch->is_draw_center_enabled());
	}

	memdelete(nine_patch);
}

TEST_CASE("[Stage][NinePatchRect] Axis stretch modes") {
	NinePatchRect *nine_patch = memnew(NinePatchRect);

	SUBCASE("Horizontal stretch mode accepts each value") {
		nine_patch->set_h_axis_stretch_mode(NinePatchRect::AXIS_STRETCH_MODE_TILE);
		CHECK(nine_patch->get_h_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_TILE);

		nine_patch->set_h_axis_stretch_mode(NinePatchRect::AXIS_STRETCH_MODE_TILE_FIT);
		CHECK(nine_patch->get_h_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_TILE_FIT);

		nine_patch->set_h_axis_stretch_mode(NinePatchRect::AXIS_STRETCH_MODE_STRETCH);
		CHECK(nine_patch->get_h_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_STRETCH);
	}

	SUBCASE("Vertical stretch mode accepts each value") {
		nine_patch->set_v_axis_stretch_mode(NinePatchRect::AXIS_STRETCH_MODE_TILE);
		CHECK(nine_patch->get_v_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_TILE);

		nine_patch->set_v_axis_stretch_mode(NinePatchRect::AXIS_STRETCH_MODE_TILE_FIT);
		CHECK(nine_patch->get_v_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_TILE_FIT);

		nine_patch->set_v_axis_stretch_mode(NinePatchRect::AXIS_STRETCH_MODE_STRETCH);
		CHECK(nine_patch->get_v_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_STRETCH);
	}

	SUBCASE("Horizontal and vertical stretch modes are independent") {
		nine_patch->set_h_axis_stretch_mode(NinePatchRect::AXIS_STRETCH_MODE_TILE);
		nine_patch->set_v_axis_stretch_mode(NinePatchRect::AXIS_STRETCH_MODE_TILE_FIT);

		CHECK(nine_patch->get_h_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_TILE);
		CHECK(nine_patch->get_v_axis_stretch_mode() == NinePatchRect::AXIS_STRETCH_MODE_TILE_FIT);
	}

	memdelete(nine_patch);
}

} // namespace TestNinePatchRect
