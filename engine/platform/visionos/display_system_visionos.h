/**************************************************************************/
/*  display_system_visionos.h                                             */

#pragma once

#include "backends/apple_embedded/display_system_apple_embedded.h"

class GCKeyboardHandler;

class DisplaySystemVisionOS : public DisplaySystemAppleEmbedded {
	ORBLIT_SOFTCLASS(DisplaySystemVisionOS, DisplaySystemAppleEmbedded);

	_THREAD_SAFE_CLASS_

	GCKeyboardHandler *gc_keyboard = nullptr;

	DisplaySystemVisionOS(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);
	~DisplaySystemVisionOS();

public:
	static DisplaySystemVisionOS *get_singleton();

	static void register_visionos_driver();
	static DisplaySystem *create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);

	virtual String get_name() const override;

	virtual int screen_get_dpi(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual float screen_get_scale(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual float screen_get_refresh_rate(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;

protected:
	virtual bool _screen_hdr_is_supported() const override;
	virtual float _screen_potential_edr_headroom() const override;
	virtual float _screen_current_edr_headroom() const override;
};
