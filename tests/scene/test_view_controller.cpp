/**************************************************************************/
/*  test_view_controller.cpp                                              */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_view_controller)

#if defined(DEBUG_ENABLED) && !defined(SPATIAL_DISABLED)

#include "scene/debugger/view_spatial_controller.h"

namespace TestViewController {

TEST_CASE("[ViewController] A new cursor starts at the origin") {
	// The storage is filled first, because a stack slot often holds zero by chance.
	alignas(View3DController::Cursor) uint8_t storage[sizeof(View3DController::Cursor)];
	memset(storage, 0xFF, sizeof(storage));
	const View3DController::Cursor *cursor = new (storage) View3DController::Cursor();

	CHECK(cursor->pos_x == 0.0);
	CHECK(cursor->pos_y == 0.0);
	CHECK(cursor->pos_z == 0.0);
	CHECK(cursor->eye_pos_x == 0.0);
	CHECK(cursor->eye_pos_y == 0.0);
	CHECK(cursor->eye_pos_z == 0.0);
}

} // namespace TestViewController

#endif // DEBUG_ENABLED && !SPATIAL_DISABLED
