/**************************************************************************/
/*  test_input_event.cpp                                                  */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_input_event)

#include "core/input/input.h"
#include "core/input/input_event.h"
#include "core/input/input_map.h"
#include "core/math/rect2.h"
#include "core/variant/array.h"
#include "tests/signal_watcher.h"

namespace TestInputEvent {

TEST_CASE("[InputEvent] Signal is emitted when device is changed") {
	Ref<InputEventKey> input_event;
	input_event.instantiate();

	SIGNAL_WATCH(*input_event, CoreStringName(changed));

	Array empty_args = { {} };
	input_event->set_device(1);

	SIGNAL_CHECK("changed", empty_args);
	CHECK(input_event->get_device() == 1);

	SIGNAL_UNWATCH(*input_event, CoreStringName(changed));
}

TEST_CASE("[InputEvent] Test accumulate") {
	Ref<InputEventMouseMotion> iemm1, iemm2;
	Ref<InputEventKey> iek;

	iemm1.instantiate(), iemm2.instantiate();
	iek.instantiate();

	iemm1->set_button_mask(MouseButtonMask::LEFT);

	CHECK_FALSE(iemm1->accumulate(iemm2));

	iemm2->set_button_mask(MouseButtonMask::LEFT);

	CHECK(iemm1->accumulate(iemm2));

	CHECK_FALSE(iemm1->accumulate(iek));
	CHECK_FALSE(iemm2->accumulate(iek));
}

TEST_CASE("[InputEvent][Stage] Test methods that interact with the InputMap") {
	const String mock_action = "mock_action";
	Ref<InputEventJoypadMotion> iejm;
	iejm.instantiate();

	InputMap::get_singleton()->add_action(mock_action, 0.5);
	InputMap::get_singleton()->action_add_event(mock_action, iejm);

	CHECK(iejm->is_action_type());
	CHECK(iejm->is_action(mock_action));

	CHECK(iejm->is_action_released(mock_action));
	CHECK(Math::is_equal_approx(iejm->get_action_strength(mock_action), 0.0f));

	iejm->set_axis_value(0.8f);
	// Since deadzone is 0.5, action_strength grows linearly from 0.5 to 1.0.
	CHECK(Math::is_equal_approx(iejm->get_action_strength(mock_action), 0.6f));
	CHECK(Math::is_equal_approx(iejm->get_action_raw_strength(mock_action), 0.8f));
	CHECK(iejm->is_action_pressed(mock_action));

	InputMap::get_singleton()->erase_action(mock_action);
}

TEST_CASE("[InputEvent][Stage] A transformed mouse button still matches its action by event") {
	const StringName action = "mock_click_action";
	Ref<InputEventMouseButton> click;
	click.instantiate();
	click->set_button_index(MouseButton::LEFT);
	click->set_pressed(true);

	InputMap::get_singleton()->add_action(action);
	InputMap::get_singleton()->action_add_event(action, click);

	Ref<InputEventMouseButton> sent = click->duplicate();
	Input::get_singleton()->parse_input_event(sent);
	Input::get_singleton()->flush_buffered_events();

	Transform2D shift;
	shift = shift.translated(Vector2(2.0f, 3.0f));
	Ref<InputEvent> copy = sent->xformed_by(shift);

	CHECK(copy != sent);
	CHECK(Input::get_singleton()->is_action_just_pressed_by_event(action, sent));
	CHECK(Input::get_singleton()->is_action_just_pressed_by_event(action, copy));

	Ref<InputEventMouseButton> other = sent->duplicate();
	CHECK_FALSE(Input::get_singleton()->is_action_just_pressed_by_event(action, other));

	Input::get_singleton()->action_release(action);
	InputMap::get_singleton()->erase_action(action);
}

TEST_CASE("[InputEvent] Test xformed_by") {
	Ref<InputEventMouseMotion> iemm1;
	iemm1.instantiate();

	iemm1->set_position(Vector2(0.0f, 0.0f));
	Transform2D transform;
	transform = transform.translated(Vector2(2.0f, 3.0f));

	Ref<InputEventMouseMotion> iemm2 = iemm1->xformed_by(transform);

	CHECK(iemm2->get_position().is_equal_approx(Vector2(2.0f, 3.0f)));
}

} // namespace TestInputEvent
