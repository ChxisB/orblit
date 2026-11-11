/**************************************************************************/
/*  test_range.cpp                                                        */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_range)

#include "scene/ui/progress_bar.h"
#include "scene/ui/slider.h"

namespace TestRange {

TEST_CASE("[Stage][Range] exponential ratio with a zero minimum") {
	HSlider *slider = memnew(HSlider);
	slider->set_exp_ratio(true);
	slider->set_step(0);
	slider->set_min(0);
	slider->set_max(100);

	SUBCASE("The ends of the ratio are the ends of the range") {
		slider->set_as_ratio(0);
		CHECK(slider->get_value() == doctest::Approx(0.0));
		slider->set_as_ratio(1);
		CHECK(slider->get_value() == doctest::Approx(100.0));
	}

	SUBCASE("The ratio and the value map back to each other") {
		slider->set_as_ratio(0.5);
		CHECK(slider->get_value() > 0.0);
		CHECK(slider->get_value() < 50.0);
		CHECK(slider->get_as_ratio() == doctest::Approx(0.5));
		slider->set_value(0);
		CHECK(slider->get_as_ratio() == doctest::Approx(0.0));
	}

	SUBCASE("A maximum below one stays usable") {
		slider->set_max(0.5);
		slider->set_as_ratio(1);
		CHECK(slider->get_value() == doctest::Approx(0.5));
		slider->set_as_ratio(0.5);
		CHECK(slider->get_value() > 0.0);
		CHECK(slider->get_value() < 0.5);
		CHECK(slider->get_as_ratio() == doctest::Approx(0.5));
	}

	SUBCASE("A positive minimum keeps the plain exponential curve") {
		slider->set_min(1);
		slider->set_as_ratio(0.5);
		CHECK(slider->get_value() == doctest::Approx(10.0));
	}

	memdelete(slider);
}

} // namespace TestRange
