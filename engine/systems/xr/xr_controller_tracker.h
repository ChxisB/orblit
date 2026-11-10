/**************************************************************************/
/*  xr_controller_tracker.h                                               */

#pragma once

#include "core/os/thread_safe.h"
#include "systems/xr/xr_positional_tracker.h"

/**
	The controller tracker object as an object that represents the position and orientation of a controller.
*/

class XRControllerTracker : public XRPositionalTracker {
	ORBLIT_CLASS(XRControllerTracker, XRPositionalTracker);
	_THREAD_SAFE_CLASS_

public:
	XRControllerTracker() {
		type = XRSystem::TRACKER_CONTROLLER;
	}
};
