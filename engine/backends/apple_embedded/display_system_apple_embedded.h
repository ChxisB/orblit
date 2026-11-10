/**************************************************************************/
/*  display_system_apple_embedded.h                                       */

#pragma once

#include "core/os/thread_safe.h"
#include "systems/display/display_system.h"

#if defined(RD_ENABLED)
#include "systems/rendering/renderer_rd/renderer_compositor_rd.h"
#include "systems/rendering/rendering_device.h"

#if defined(VULKAN_ENABLED)
#import "backends/apple_embedded/rendering_context_driver_vulkan_apple_embedded.h"

#include <backends/vulkan/vulkan.h>
#endif // VULKAN_ENABLED

#if defined(METAL_ENABLED)
#import "backends/metal/rendering_context_driver_metal.h"
#endif // METAL_ENABLED
#endif // RD_ENABLED

#if defined(GLES3_ENABLED)
#include "backends/opengl/rasterizer_gles3.h"
#endif // GLES3_ENABLED

#import <Foundation/Foundation.h>
#import <QuartzCore/CAMetalLayer.h>

class InputEvent;
class NativeMenu;

/// "Embedded" as in "Embedded Device".
class DisplaySystemAppleEmbedded : public DisplaySystem {
	ORBLIT_SOFTCLASS(DisplaySystemAppleEmbedded, DisplaySystem);

	_THREAD_SAFE_CLASS_

#if defined(RD_ENABLED)
	RenderingContextDriver *rendering_context = nullptr;
	RenderingDevice *rendering_device = nullptr;
#endif
	NativeMenu *native_menu = nullptr;

	id tts = nullptr;

	DisplaySystemEnums::ScreenOrientation screen_orientation;

	ObjectID window_attached_instance_id;

	Callable window_event_callback;
	Callable window_resize_callback;
	Callable input_event_callback;
	Callable input_text_callback;

	Callable system_theme_changed;

	int virtual_keyboard_height = 0;

	void perform_event(const Ref<InputEvent> &p_event);

	void initialize_tts() const;

	bool edr_requested = false;
	void _update_hdr_output(bool edr_headroom_changed);
	float _calculate_current_reference_luminance() const;

protected:
	virtual bool _screen_hdr_is_supported() const { return false; }
	virtual float _screen_potential_edr_headroom() const { return 1.0f; }
	virtual float _screen_current_edr_headroom() const { return 1.0f; }
	float hardware_reference_luminance_nits = 100.0f;

	DisplaySystemAppleEmbedded(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);
	~DisplaySystemAppleEmbedded();

public:
	String rendering_driver;

	static DisplaySystemAppleEmbedded *get_singleton();

	static Vector<String> get_rendering_drivers_func();

	// MARK: - Events

	virtual void process_events() override;

	virtual void window_set_rect_changed_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_window_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_text_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_drop_files_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	static void _dispatch_input_events(const Ref<InputEvent> &p_event);
	void send_input_event(const Ref<InputEvent> &p_event) const;
	void send_input_text(const String &p_text) const;
	void send_window_event(DisplaySystemEnums::WindowEvent p_event) const;
	void _window_callback(const Callable &p_callable, const Variant &p_arg) const;

	void emit_system_theme_changed();

	// MARK: - Input

	// MARK: Touches and Apple Pencil

	void touch_press(int p_idx, int p_x, int p_y, bool p_pressed, bool p_double_click, bool p_long_press);
	void touch_drag(int p_idx, int p_prev_x, int p_prev_y, int p_x, int p_y, float p_pressure, Vector2 p_tilt);
	void touches_canceled(int p_idx);

	// MARK: Keyboard

	void key(Key p_key, char32_t p_char, Key p_unshifted, Key p_physical, NSInteger p_modifier, bool p_pressed, KeyLocation p_location);
	bool is_keyboard_active() const;

	// MARK: Motion

	void update_gravity(const Vector3 &p_gravity);
	void update_accelerometer(const Vector3 &p_accelerometer);
	void update_magnetometer(const Vector3 &p_magnetometer);
	void update_gyroscope(const Vector3 &p_gyroscope);
	void update_device_orientation(const Quaternion &p_orientation);

	// MARK: -

	virtual bool has_feature(DisplaySystemEnums::Feature p_feature) const override;
	virtual int accessibility_screen_reader_active() const override;

	virtual bool tts_is_speaking() const override;
	virtual bool tts_is_paused() const override;
	virtual TypedArray<Dictionary> tts_get_voices() const override;

	virtual void tts_speak(const String &p_text, const String &p_voice, int p_volume = 50, float p_pitch = 1.f, float p_rate = 1.f, int64_t p_utterance_id = 0, bool p_interrupt = false) override;
	virtual void tts_pause() override;
	virtual void tts_resume() override;
	virtual void tts_stop() override;

	virtual bool is_dark_mode_supported() const override;
	virtual bool is_dark_mode() const override;
	virtual void set_system_theme_change_callback(const Callable &p_callable) override;

	virtual Rect2i get_display_safe_area(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;

	virtual int get_screen_count() const override;
	virtual int get_primary_screen() const override;
	virtual Point2i screen_get_position(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Size2i screen_get_size(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Rect2i screen_get_usable_rect(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;

	virtual Vector<DisplaySystemEnums::WindowID> get_window_list() const override;

	virtual DisplaySystemEnums::WindowID get_window_at_screen_position(const Point2i &p_position) const override;

	virtual int64_t window_get_native_handle(DisplaySystemEnums::HandleType p_handle_type, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_attach_instance_id(ObjectID p_instance, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual ObjectID window_get_attached_instance_id(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_title(const String &p_title, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual int window_get_current_screen(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void window_set_current_screen(int p_screen, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual Point2i window_get_position(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual Point2i window_get_position_with_decorations(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void window_set_position(const Point2i &p_position, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual void window_set_transient(DisplaySystemEnums::WindowID p_window, DisplaySystemEnums::WindowID p_parent) override;

	virtual void window_set_max_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual Size2i window_get_max_size(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_min_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual Size2i window_get_min_size(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual Size2i window_get_size(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual Size2i window_get_size_with_decorations(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_mode(DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual DisplaySystemEnums::WindowMode window_get_mode(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual bool window_is_maximize_allowed(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_flag(DisplaySystemEnums::WindowFlags p_flag, bool p_enabled, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual bool window_get_flag(DisplaySystemEnums::WindowFlags p_flag, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_request_attention(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_move_to_foreground(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual bool window_is_focused(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual float screen_get_max_scale() const override;

	virtual void screen_set_orientation(DisplaySystemEnums::ScreenOrientation p_orientation, int p_screen) override;
	virtual DisplaySystemEnums::ScreenOrientation screen_get_orientation(int p_screen) const override;

	virtual bool window_can_draw(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual bool can_any_window_draw() const override;

	virtual void window_set_vsync_mode(DisplaySystemEnums::VSyncMode p_vsync_mode, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual DisplaySystemEnums::VSyncMode window_get_vsync_mode(DisplaySystemEnums::WindowID p_vsync_mode) const override;

	virtual bool is_touchscreen_available() const override;

	virtual void virtual_keyboard_show(const String &p_existing_text, const Rect2 &p_screen_rect, DisplaySystemEnums::VirtualKeyboardType p_type, int p_max_length, int p_cursor_start, int p_cursor_end) override;
	virtual void virtual_keyboard_hide() override;

	void virtual_keyboard_set_height(int height);
	virtual int virtual_keyboard_get_height() const override;
	virtual bool has_hardware_keyboard() const override;

	virtual void clipboard_set(const String &p_text) override;
	virtual String clipboard_get() const override;

	virtual void screen_set_keep_on(bool p_enable) override;
	virtual bool screen_is_kept_on() const override;

	// MARK: - HDR / EDR

	void current_edr_headroom_changed();
	virtual bool window_is_hdr_output_supported(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void window_request_hdr_output(const bool p_enabled, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual bool window_is_hdr_output_requested(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual bool window_is_hdr_output_enabled(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_hdr_output_reference_luminance(const float p_reference_luminance, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual float window_get_hdr_output_reference_luminance(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual float window_get_hdr_output_current_reference_luminance(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_hdr_output_max_luminance(const float p_max_luminance, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual float window_get_hdr_output_max_luminance(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual float window_get_hdr_output_current_max_luminance(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual float window_get_output_max_linear_value(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	void resize_window(CGSize size);
	virtual void swap_buffers() override {}

	virtual void set_native_icon(const String &p_filename) override;
	virtual void set_icon(const Ref<Image> &p_icon) override;
};
