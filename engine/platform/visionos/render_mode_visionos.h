/**************************************************************************/
/*  render_mode_visionos.h                                                */

#pragma once

class RenderModeVisionOS {
public:
	enum Mode {
		WINDOWED = 0,
		COMPOSITOR_SERVICES = 1
	};

	static Mode get_mode();
	static void *get_compositor_services_device();

private:
	RenderModeVisionOS() = delete;
};
