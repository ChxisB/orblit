/**************************************************************************/
/*  camera_win.h                                                          */

#pragma once

#include "systems/camera/camera_system.h"

class CameraWindows : public CameraSystem {
private:
	void add_active_cameras();

public:
	CameraWindows();
	~CameraWindows() {}
};
