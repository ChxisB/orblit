/**************************************************************************/
/*  test_range.cpp                                                        */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_range)

#ifndef ADVANCED_GUI_DISABLED

#include "scene/ui/range.h"

namespace TestRange {

TEST_CASE("[Stage][Range] A value on the step grid keeps its exact double") {
	Range *range = memnew(Range);
	range->set_max(1.0);

	range->set_step(1e-7);
	range->set_value(1e-7);
	CHECK(range->get_value() == 1e-7);

	range->set_step(0.1);
	range->set_value(0.3);
	CHECK(range->get_value() == 0.3);

	range->set_step(1e-3);
	range->set_value(0.007);
	CHECK(range->get_value() == 0.007);

	memdelete(range);
}

TEST_CASE("[Stage][Range] A value off the step grid snaps to the nearest step") {
	Range *range = memnew(Range);
	range->set_max(1.0);
	range->set_step(1e-7);

	range->set_value(1.01e-7);
	CHECK(range->get_value() == 1e-7);

	range->set_step(0.25);
	range->set_value(0.4);
	CHECK(range->get_value() == 0.5);

	memdelete(range);
}

} // namespace TestRange

#endif // ADVANCED_GUI_DISABLED
