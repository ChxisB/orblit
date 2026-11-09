/**************************************************************************/
/*  display_system_macos_base.h                                           */

#pragma once

#include "core/os/thread_safe.h"
#include "core/typedefs.h" // IWYU pragma: keep. Prevent macOS `MAX`/`MIN` from being defined.
#include "systems/display/display_system.h"

#define FontVariation __FontVariation

#import <AppKit/AppKit.h>
#import <IOKit/pwr_mgt/IOPMLib.h>

#undef FontVariation

class RenderingContextDriver;
class RenderingDevice;

class DisplaySystemMacOSBase : public DisplaySystem {
	ORBLIT_SOFTCLASS(DisplaySystemMacOSBase, DisplaySystem)

	id tts = nullptr;
	IOPMAssertionID screen_keep_on_assertion = kIOPMNullAssertionID;

	struct LayoutInfo {
		String name;
		String code;
	};
	mutable Vector<LayoutInfo> kbd_layouts;
	mutable int current_layout = 0;
	mutable bool keyboard_layout_dirty = true;

	Callable system_theme_changed;

	constexpr static CGFloat HARDWARE_REFERENCE_LUMINANCE_NITS = 100.0f;

public:
	struct HDROutput {
		static constexpr float AUTO_MAX_LUMINANCE = -1.0f;

		bool requested = false;
		float max_luminance = AUTO_MAX_LUMINANCE;

		bool is_auto_max_luminance() const { return max_luminance < 0.0f; }
	};

protected:
	_THREAD_SAFE_CLASS_

	DisplaySystemEnums::MouseMode mouse_mode = DisplaySystemEnums::MOUSE_MODE_VISIBLE;
	DisplaySystemEnums::MouseMode mouse_mode_base = DisplaySystemEnums::MOUSE_MODE_VISIBLE;
	DisplaySystemEnums::MouseMode mouse_mode_override = DisplaySystemEnums::MOUSE_MODE_VISIBLE;
	bool mouse_mode_override_enabled = false;

	void _mouse_update_mode();
	virtual void _mouse_apply_mode(DisplaySystemEnums::MouseMode p_prev_mode, DisplaySystemEnums::MouseMode p_new_mode) = 0;

	String im_text;
	Point2i im_selection;

	DisplaySystemEnums::CursorShape cursor_shape = DisplaySystemEnums::CURSOR_ARROW;

	void initialize_tts() const;

	static CGDirectDisplayID _get_display_id_for_screen(NSScreen *p_screen);

	void _update_keyboard_layouts() const;
	static void _keyboard_layout_changed(CFNotificationCenterRef center, void *observer, CFStringRef name, const void *object, CFDictionaryRef user_info);

#if defined(RD_ENABLED)
	RenderingContextDriver *rendering_context = nullptr;
	RenderingDevice *rendering_device = nullptr;
#endif

	virtual HDROutput &_get_hdr_output(DisplaySystemEnums::WindowID p_window) = 0;
	virtual const HDROutput &_get_hdr_output(DisplaySystemEnums::WindowID p_window) const = 0;

	constexpr float _calculate_current_reference_luminance(CGFloat p_max_potential_edr_value, CGFloat p_max_edr_value) const;
	void _update_hdr_output(DisplaySystemEnums::WindowID p_window, const HDROutput &p_hdr);

public:
	virtual bool has_window(DisplaySystemEnums::WindowID p_window) const { return p_window == DisplaySystemEnums::MAIN_WINDOW_ID; }

	virtual bool tts_is_speaking() const override;
	virtual bool tts_is_paused() const override;
	virtual TypedArray<Dictionary> tts_get_voices() const override;

	virtual void tts_speak(const String &p_text, const String &p_voice, int p_volume = 50, float p_pitch = 1.f, float p_rate = 1.f, int64_t p_utterance_id = 0, bool p_interrupt = false) override;
	virtual void tts_pause() override;
	virtual void tts_resume() override;
	virtual void tts_stop() override;

	void emit_system_theme_changed();
	virtual bool is_dark_mode_supported() const override;
	virtual bool is_dark_mode() const override;
	virtual Color get_accent_color() const override;
	virtual Color get_base_color() const override;
	virtual void set_system_theme_change_callback(const Callable &p_callable) override;

	virtual void send_window_event_by_id(DisplaySystemEnums::WindowEvent p_event, DisplaySystemEnums::WindowID p_id = DisplaySystemEnums::MAIN_WINDOW_ID) const = 0;

	virtual void mouse_set_mode(DisplaySystemEnums::MouseMode p_mode) override;
	virtual DisplaySystemEnums::MouseMode mouse_get_mode() const override;
	virtual void mouse_set_mode_override(DisplaySystemEnums::MouseMode p_mode) override;
	virtual DisplaySystemEnums::MouseMode mouse_get_mode_override() const override;
	virtual void mouse_set_mode_override_enabled(bool p_override_enabled) override;
	virtual bool mouse_is_mode_override_enabled() const override;

	virtual void clipboard_set(const String &p_text) override;
	virtual String clipboard_get() const override;
	virtual Ref<Image> clipboard_get_image() const override;
	virtual bool clipboard_has() const override;
	virtual bool clipboard_has_image() const override;

	virtual int get_primary_screen() const override;
	virtual float screen_get_refresh_rate(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual void screen_set_keep_on(bool p_enable) override;
	virtual bool screen_is_kept_on() const override;

	virtual int accessibility_should_increase_contrast() const override;
	virtual int accessibility_should_reduce_animation() const override;
	virtual int accessibility_should_reduce_transparency() const override;
	virtual int accessibility_screen_reader_active() const override;

	void update_im_text(const Point2i &p_selection, const String &p_text);
	virtual Point2i ime_get_selection() const override;
	virtual String ime_get_text() const override;

	virtual DisplaySystemEnums::CursorShape cursor_get_shape() const override;

	virtual void beep() const override;

	virtual int keyboard_get_layout_count() const override;
	virtual int keyboard_get_current_layout() const override;
	virtual void keyboard_set_current_layout(int p_index) override;
	virtual String keyboard_get_layout_language(int p_index) const override;
	virtual String keyboard_get_layout_name(int p_index) const override;
	virtual Key keyboard_get_keycode_from_physical(Key p_keycode) const override;
	virtual Key keyboard_get_label_from_physical(Key p_keycode) const override;
	virtual void show_emoji_and_symbol_picker() const override;

	virtual void window_get_edr_values(DisplaySystemEnums::WindowID p_window, CGFloat *r_max_potential_edr_value = nullptr, CGFloat *r_max_edr_value = nullptr) const = 0;
	virtual bool window_is_hdr_output_supported(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void window_request_hdr_output(const bool p_enable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual bool window_is_hdr_output_requested(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual bool window_is_hdr_output_enabled(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_hdr_output_reference_luminance(const float p_reference_luminance, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual float window_get_hdr_output_reference_luminance(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual float window_get_hdr_output_current_reference_luminance(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_hdr_output_max_luminance(const float p_max_luminance, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual float window_get_hdr_output_max_luminance(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual float window_get_hdr_output_current_max_luminance(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual float window_get_output_max_linear_value(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	DisplaySystemMacOSBase();
	~DisplaySystemMacOSBase();
};
