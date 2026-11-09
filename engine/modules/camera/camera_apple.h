/**************************************************************************/
/*  camera_apple.h                                                        */

#pragma once

///@TODO this is a near duplicate of CameraIOS, we should find a way to combine those to minimize code duplication!!!!
// If you fix something here, make sure you fix it there as well!

#include "systems/camera/camera_system.h"

class CameraApple : public CameraSystem {
	ORBLIT_SOFTCLASS(CameraApple, CameraSystem);

public:
	CameraApple() = default;

	void update_feeds();
	void set_monitoring_feeds(bool p_monitoring_feeds) override;
	void handle_display_rotation_change(int p_orientation) override;
	void handle_application_pause() override;
	void handle_application_resume() override;
};
