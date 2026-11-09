/**************************************************************************/
/*  test_texture_progress_bar.cpp                                         */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_texture_progress_bar)

#include "scene/ui/texture_progress_bar.h"

namespace TestTextureProgressBar {

TEST_CASE("[Stage][TextureProgressBar]") {
	TextureProgressBar *texture_progress_bar = memnew(TextureProgressBar);

	SUBCASE("[TextureProgressBar] set_radial_initial_angle() should wrap angle between 0 and 360 degrees (inclusive).") {
		texture_progress_bar->set_radial_initial_angle(0.0);
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)0.0));

		texture_progress_bar->set_radial_initial_angle(360.0);
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)360.0));

		texture_progress_bar->set_radial_initial_angle(30.5);
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)30.5));

		texture_progress_bar->set_radial_initial_angle(-30.5);
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)(360 - 30.5)));

		texture_progress_bar->set_radial_initial_angle(36000 + 30.5);
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)30.5));

		texture_progress_bar->set_radial_initial_angle(-(36000 + 30.5));
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)(360 - 30.5)));
	}

	SUBCASE("[TextureProgressBar] set_radial_initial_angle() should not set non-finite values.") {
		texture_progress_bar->set_radial_initial_angle(30.5);

		ERR_PRINT_OFF;
		texture_progress_bar->set_radial_initial_angle(Math::INF);
		ERR_PRINT_ON;
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)30.5));

		ERR_PRINT_OFF;
		texture_progress_bar->set_radial_initial_angle(-Math::INF);
		ERR_PRINT_ON;
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)30.5));

		ERR_PRINT_OFF;
		texture_progress_bar->set_radial_initial_angle(Math::NaN);
		ERR_PRINT_ON;
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)30.5));

		ERR_PRINT_OFF;
		texture_progress_bar->set_radial_initial_angle(-Math::NaN);
		ERR_PRINT_ON;
		CHECK(Math::is_equal_approx(texture_progress_bar->get_radial_initial_angle(), (float)30.5));
	}

	memdelete(texture_progress_bar);
}

} // namespace TestTextureProgressBar
