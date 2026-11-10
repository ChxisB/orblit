/**************************************************************************/
/*  mock_control.h                                                        */

#pragma once

#include "scene/ui/control.h"

// Mock Control used for testing size related functionality.
// The minimum and desired sizes can be set manually for testing various layouts.
class MockControl : public Control {
	ORBLIT_SOFTCLASS(MockControl, Control);

private:
	Size2 minimum_size;
	Size2 desired_size;

public:
	Size2 get_minimum_size() const override {
		return minimum_size;
	}

	Size2 get_desired_size() const override {
		return desired_size;
	}

	void set_minimum_size(Size2 p_size) {
		minimum_size = p_size;
		update_minimum_size();
	}

	void set_desired_size(Size2 p_size) {
		desired_size = p_size;
		update_desired_size();
	}
};
