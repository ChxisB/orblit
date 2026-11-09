/**************************************************************************/
/*  display_system_android.h                                              */

#pragma once

#include "systems/display/display_system.h"

class InputEvent;
class NativeMenu;

#if defined(RD_ENABLED)
class RenderingContextDriver;
class RenderingDevice;
#endif

class DisplaySystemAndroid : public DisplaySystem {
	ORBLIT_SOFTCLASS(DisplaySystemAndroid, DisplaySystem);

	String rendering_driver;

	// https://developer.android.com/reference/android/view/PointerIcon
	// mapping between Orblit's cursor shape to Android's'
	int android_cursors[DisplaySystemEnums::CURSOR_MAX] = {
		1000, //DisplaySystemEnums::CURSOR_ARROW
		1008, //DisplaySystemEnums::CURSOR_IBEAM
		1002, //DisplaySystemEnums::CURSOR_POINTIN
		1007, //DisplaySystemEnums::CURSOR_CROSS
		1004, //DisplaySystemEnums::CURSOR_WAIT
		1004, //DisplaySystemEnums::CURSOR_BUSY
		1021, //DisplaySystemEnums::CURSOR_DRAG
		1021, //DisplaySystemEnums::CURSOR_CAN_DRO
		1000, //DisplaySystemEnums::CURSOR_FORBIDD (no corresponding icon in Android's icon so fallback to default)
		1015, //DisplaySystemEnums::CURSOR_VSIZE
		1014, //DisplaySystemEnums::CURSOR_HSIZE
		1017, //DisplaySystemEnums::CURSOR_BDIAGSI
		1016, //DisplaySystemEnums::CURSOR_FDIAGSI
		1020, //DisplaySystemEnums::CURSOR_MOVE
		1015, //DisplaySystemEnums::CURSOR_VSPLIT
		1014, //DisplaySystemEnums::CURSOR_HSPLIT
		1003, //DisplaySystemEnums::CURSOR_HELP
	};
	const int CURSOR_TYPE_NULL = 0;
	DisplaySystemEnums::MouseMode mouse_mode = DisplaySystemEnums::MouseMode::MOUSE_MODE_VISIBLE;
	DisplaySystemEnums::MouseMode mouse_mode_base = DisplaySystemEnums::MouseMode::MOUSE_MODE_VISIBLE;
	DisplaySystemEnums::MouseMode mouse_mode_override = DisplaySystemEnums::MouseMode::MOUSE_MODE_VISIBLE;
	bool mouse_mode_override_enabled = false;
	void _mouse_update_mode();

	bool keep_screen_on;
	bool swap_buffers_flag;

	DisplaySystemEnums::CursorShape cursor_shape = DisplaySystemEnums::CursorShape::CURSOR_ARROW;

#if defined(RD_ENABLED)
	RenderingDevice *rendering_device = nullptr;
#endif
	NativeMenu *native_menu = nullptr;

	ObjectID window_attached_instance_id;

	Callable window_event_callback;
	Callable input_event_callback;
	Callable input_text_callback;
	Callable rect_changed_callback;

	Callable system_theme_changed;
	Callable hardware_keyboard_connection_changed;

	Callable dialog_callback;
	Callable input_dialog_callback;

	Callable file_picker_callback;

	template <typename... Args>
	void _window_callback(const Callable &p_callable, bool p_deferred, const Args &...p_rest_args) const;

	static void _dispatch_input_events(const Ref<InputEvent> &p_event);

public:
	static DisplaySystemAndroid *get_singleton();

	virtual bool has_feature(DisplaySystemEnums::Feature p_feature) const override;
	virtual String get_name() const override;

	virtual int accessibility_should_increase_contrast() const override;
	virtual int accessibility_screen_reader_active() const override;
	virtual int accessibility_should_reduce_animation() const override;

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
	void emit_system_theme_changed();

	virtual void clipboard_set(const String &p_text) override;
	virtual String clipboard_get() const override;
	virtual bool clipboard_has() const override;

	virtual Error dialog_show(String p_title, String p_description, Vector<String> p_buttons, const Callable &p_callback) override;
	void emit_dialog_callback(int p_button_index);

	virtual Error dialog_input_text(String p_title, String p_description, String p_partial, const Callable &p_callback) override;
	void emit_input_dialog_callback(String p_text);

	virtual Error file_dialog_show(const String &p_title, const String &p_current_directory, const String &p_filename, bool p_show_hidden, const DisplaySystemEnums::FileDialogMode p_mode, const Vector<String> &p_filters, const Callable &p_callback, DisplaySystemEnums::WindowID p_window_id) override;
	void emit_file_picker_callback(bool p_ok, const Vector<String> &p_selected_paths);

	virtual Color get_accent_color() const override;
	virtual Color get_base_color() const override;

	virtual TypedArray<Rect2> get_display_cutouts(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Rect2i get_display_safe_area(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;

	virtual void screen_set_keep_on(bool p_enable) override;
	virtual bool screen_is_kept_on() const override;

	virtual void screen_set_orientation(DisplaySystemEnums::ScreenOrientation p_orientation, int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) override;
	virtual DisplaySystemEnums::ScreenOrientation screen_get_orientation(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	int get_display_rotation() const;

	virtual int get_screen_count() const override;
	virtual int get_primary_screen() const override;
	virtual Point2i screen_get_position(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Size2i screen_get_size(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Rect2i screen_get_usable_rect(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual int screen_get_dpi(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual float screen_get_scale(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual float screen_get_refresh_rate(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual bool is_touchscreen_available() const override;

	virtual void virtual_keyboard_show(const String &p_existing_text, const Rect2 &p_screen_rect = Rect2(), DisplaySystemEnums::VirtualKeyboardType p_type = DisplaySystemEnums::KEYBOARD_TYPE_DEFAULT, int p_max_length = -1, int p_cursor_start = -1, int p_cursor_end = -1) override;
	virtual void virtual_keyboard_hide() override;
	virtual int virtual_keyboard_get_height() const override;
	virtual bool has_hardware_keyboard() const override;
	virtual void set_hardware_keyboard_connection_change_callback(const Callable &p_callable) override;
	void emit_hardware_keyboard_connection_changed(bool p_connected);

	virtual void window_set_window_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_text_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_rect_changed_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_drop_files_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	void send_window_event(DisplaySystemEnums::WindowEvent p_event, bool p_deferred = false) const;
	void send_input_event(const Ref<InputEvent> &p_event) const;
	void send_input_text(const String &p_text) const;

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

	virtual bool window_can_draw(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual bool can_any_window_draw() const override;

	virtual void window_set_vsync_mode(DisplaySystemEnums::VSyncMode p_vsync_mode, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual DisplaySystemEnums::VSyncMode window_get_vsync_mode(DisplaySystemEnums::WindowID p_vsync_mode) const override;

	virtual void window_set_color(const Color &p_color) override;

	virtual void process_events() override;

	void process_accelerometer(const Vector3 &p_accelerometer);
	void process_gravity(const Vector3 &p_gravity);
	void process_magnetometer(const Vector3 &p_magnetometer);
	void process_gyroscope(const Vector3 &p_gyroscope);
	void process_device_orientation(const Quaternion &p_orientation);

	void _cursor_set_shape_helper(DisplaySystemEnums::CursorShape p_shape, bool force = false);
	virtual void cursor_set_shape(DisplaySystemEnums::CursorShape p_shape) override;
	virtual DisplaySystemEnums::CursorShape cursor_get_shape() const override;
	virtual void cursor_set_custom_image(const Ref<Resource> &p_cursor, DisplaySystemEnums::CursorShape p_shape = DisplaySystemEnums::CURSOR_ARROW, const Vector2 &p_hotspot = Vector2()) override;

	virtual void mouse_set_mode(DisplaySystemEnums::MouseMode p_mode) override;
	virtual DisplaySystemEnums::MouseMode mouse_get_mode() const override;
	virtual void mouse_set_mode_override(DisplaySystemEnums::MouseMode p_mode) override;
	virtual DisplaySystemEnums::MouseMode mouse_get_mode_override() const override;
	virtual void mouse_set_mode_override_enabled(bool p_override_enabled) override;
	virtual bool mouse_is_mode_override_enabled() const override;

	static DisplaySystem *create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);
	static Vector<String> get_rendering_drivers_func();
	static void register_android_driver();

#ifdef VULKAN_ENABLED
	static bool check_vulkan_global_context(bool p_vulkan_requirements_met);
	static void free_vulkan_global_context();
#endif

	void reset_window();
	void notify_surface_changed(int p_width, int p_height);
	void notify_application_paused();

	virtual Point2i mouse_get_position() const override;
	virtual BitField<MouseButtonMask> mouse_get_button_state() const override;

	void reset_swap_buffers_flag();
	bool should_swap_buffers() const;
	virtual void swap_buffers() override;

	virtual void set_native_icon(const String &p_filename) override;
	virtual void set_icon(const Ref<Image> &p_icon) override;

	virtual bool is_window_transparency_available() const override;

	virtual bool is_in_pip_mode(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void pip_mode_enter(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void pip_mode_set_aspect_ratio(int p_numerator, int p_denominator, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void pip_mode_set_auto_enter_on_background(bool p_auto_enter_on_background, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	DisplaySystemAndroid(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);
	~DisplaySystemAndroid();
};
