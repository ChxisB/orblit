/**************************************************************************/
/*  test_parallax_planar.cpp                                                  */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_parallax_planar)

#include "scene/planar/parallax_planar.h"

// Test cases for the Parallax2D class to ensure its properties are set and retrieved correctly.

namespace TestParallax2D {

TEST_CASE("[Stage][Parallax2D] Scroll Scale") {
	// Test setting and getting the scroll scale.
	Parallax2D *parallax = memnew(Parallax2D);
	Size2 scale(2, 2);
	parallax->set_scroll_scale(scale);
	CHECK(parallax->get_scroll_scale() == scale);
	memdelete(parallax);
}

TEST_CASE("[Stage][Parallax2D] Repeat Size") {
	// Test setting and getting the repeat size.
	Parallax2D *parallax = memnew(Parallax2D);
	Size2 size(100, 100);
	parallax->set_repeat_size(size);
	CHECK(parallax->get_repeat_size() == size);
	memdelete(parallax);
}

TEST_CASE("[Stage][Parallax2D] Repeat Times") {
	// Test setting and getting the repeat times.
	Parallax2D *parallax = memnew(Parallax2D);
	int times = 5;
	parallax->set_repeat_times(times);
	CHECK(parallax->get_repeat_times() == times);
	memdelete(parallax);
}

TEST_CASE("[Stage][Parallax2D] Autoscroll") {
	// Test setting and getting the autoscroll values.
	Parallax2D *parallax = memnew(Parallax2D);
	Point2 autoscroll(1, 1);
	parallax->set_autoscroll(autoscroll);
	CHECK(parallax->get_autoscroll() == autoscroll);
	memdelete(parallax);
}

TEST_CASE("[Stage][Parallax2D] Scroll Offset") {
	// Test setting and getting the scroll offset.
	Parallax2D *parallax = memnew(Parallax2D);
	Point2 offset(10, 10);
	parallax->set_scroll_offset(offset);
	CHECK(parallax->get_scroll_offset() == offset);
	memdelete(parallax);
}

TEST_CASE("[Stage][Parallax2D] Screen Offset") {
	// Test setting and getting the screen offset.
	Parallax2D *parallax = memnew(Parallax2D);
	Point2 offset(20, 20);
	parallax->set_screen_offset(offset);
	CHECK(parallax->get_screen_offset() == offset);
	memdelete(parallax);
}

TEST_CASE("[Stage][Parallax2D] Limit Begin") {
	// Test setting and getting the limit begin values.
	Parallax2D *parallax = memnew(Parallax2D);
	Point2 limit_begin(-100, -100);
	parallax->set_limit_begin(limit_begin);
	CHECK(parallax->get_limit_begin() == limit_begin);
	memdelete(parallax);
}

TEST_CASE("[Stage][Parallax2D] Limit End") {
	// Test setting and getting the limit end values.
	Parallax2D *parallax = memnew(Parallax2D);
	Point2 limit_end(100, 100);
	parallax->set_limit_end(limit_end);
	CHECK(parallax->get_limit_end() == limit_end);
	memdelete(parallax);
}

TEST_CASE("[Stage][Parallax2D] Follow Viewport") {
	// Test setting and getting the follow viewport flag.
	Parallax2D *parallax = memnew(Parallax2D);
	parallax->set_follow_viewport(false);
	CHECK_FALSE(parallax->get_follow_viewport());
	memdelete(parallax);
}

TEST_CASE("[Stage][Parallax2D] Ignore Camera Scroll") {
	// Test setting and getting the ignore camera scroll flag.
	Parallax2D *parallax = memnew(Parallax2D);
	parallax->set_ignore_camera_scroll(true);
	CHECK(parallax->is_ignore_camera_scroll());
	memdelete(parallax);
}

} // namespace TestParallax2D
