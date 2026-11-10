/**************************************************************************/
/*  display_system_wayland.h                                              */

#pragma once

#ifdef WAYLAND_ENABLED

#include "wayland/wayland_thread.h"

#include "core/os/process_id.h"
#include "systems/display/display_system.h"

class InputEvent;
class NativeMenu;

#ifdef RD_ENABLED
class RenderingDevice;
class RenderingContextDriver;
#endif

#ifdef GLES3_ENABLED
class EGLManager;
#endif

#ifdef DBUS_ENABLED
class FreeDesktopPortalDesktop;
class FreeDesktopAtSPIMonitor;
class FreeDesktopScreenSaver;
#endif

#ifdef SPEECHD_ENABLED
class TTS_Linux;
#endif

class DisplaySystemWayland : public DisplaySystem {
	ORBLIT_SOFTCLASS(DisplaySystemWayland, DisplaySystem);

	struct WindowData {
		DisplaySystemEnums::WindowID id = DisplaySystemEnums::INVALID_WINDOW_ID;

		DisplaySystemEnums::WindowID parent_id = DisplaySystemEnums::INVALID_WINDOW_ID;

		// For popups.
		DisplaySystemEnums::WindowID root_id = DisplaySystemEnums::INVALID_WINDOW_ID;

		// For toplevels.
		List<DisplaySystemEnums::WindowID> popup_stack;

		Rect2i rect;
		Size2i max_size;
		Size2i min_size;

		Rect2i safe_rect;

		bool emulate_vsync = false;

		Vector<Vector2> mpass_region;

#ifdef GLES3_ENABLED
		struct wl_egl_window *wl_egl_window = nullptr;
#endif

		// Whether a `WaylandThread` equivalent exists or not.
		bool created = false;

		// Flags whether we have allocated a buffer through the video drivers.
		bool visible = false;

		DisplaySystemEnums::VSyncMode vsync_mode = DisplaySystemEnums::VSYNC_ENABLED;

		uint32_t flags = 0;

		DisplaySystemEnums::WindowMode mode = DisplaySystemEnums::WINDOW_MODE_WINDOWED;

		bool hdr_requested = false;
		WaylandThread::ColorProfile color_profile;

		Callable rect_changed_callback;
		Callable window_event_callback;
		Callable input_event_callback;
		Callable drop_files_callback;
		Callable input_text_callback;

		String title;
		ObjectID instance_id;
	};

	struct CustomCursor {
		Ref<Resource> resource;
		Point2i hotspot;
	};

	enum class SuspendState {
		NONE, // Unsuspended.
		TIMEOUT, // Legacy fallback.
		CAPABILITY, // New "suspended" wm_capability flag.
	};

	Point2 mouse_pos;

	DisplaySystemEnums::CursorShape cursor_shape = DisplaySystemEnums::CURSOR_ARROW;
	DisplaySystemEnums::MouseMode mouse_mode = DisplaySystemEnums::MOUSE_MODE_VISIBLE;
	DisplaySystemEnums::MouseMode mouse_mode_base = DisplaySystemEnums::MOUSE_MODE_VISIBLE;
	DisplaySystemEnums::MouseMode mouse_mode_override = DisplaySystemEnums::MOUSE_MODE_VISIBLE;
	bool mouse_mode_override_enabled = false;
	void _mouse_update_mode();

	DisplaySystemEnums::WindowID hovered_window_id = DisplaySystemEnums::INVALID_WINDOW_ID;

	HashMap<DisplaySystemEnums::CursorShape, CustomCursor> custom_cursors;

	HashMap<DisplaySystemEnums::WindowID, WindowData> windows;
	DisplaySystemEnums::WindowID window_id_counter = DisplaySystemEnums::MAIN_WINDOW_ID;

	WaylandThread wayland_thread;

	DisplaySystemEnums::Context context;
	bool swap_cancel_ok = false;

	// NOTE: These are the based on DisplaySystemEnums::WINDOW_FLAG_POPUP, which does NOT imply what it
	// seems. It's particularly confusing for our usecase, but just know that these
	// are the "take all input thx" windows while the `popup_stack` variable keeps
	// track of all the generic floating window concept.
	List<DisplaySystemEnums::WindowID> popup_menu_list;
	BitField<MouseButtonMask> last_mouse_monitor_mask = MouseButtonMask::NONE;
	bool last_touch_monitor_pressed = false;

	String ime_text;
	Vector2i ime_selection;

	SuspendState suspend_state = SuspendState::NONE;

	String rendering_driver;

#ifdef RD_ENABLED
	RenderingContextDriver *rendering_context = nullptr;
	RenderingDevice *rendering_device = nullptr;
#endif

#ifdef GLES3_ENABLED
	EGLManager *egl_manager = nullptr;
#endif

#ifdef SPEECHD_ENABLED
	TTS_Linux *tts = nullptr;
#endif
	NativeMenu *native_menu = nullptr;

#if DBUS_ENABLED
	FreeDesktopPortalDesktop *portal_desktop = nullptr;
	FreeDesktopAtSPIMonitor *atspi_monitor = nullptr;

	FreeDesktopScreenSaver *screensaver = nullptr;
	bool screensaver_inhibited = false;
#endif
	static String _get_app_id_from_context(DisplaySystemEnums::Context p_context);

	void _send_window_event(DisplaySystemEnums::WindowEvent p_event, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID);

	static void dispatch_input_events(const Ref<InputEvent> &p_event);
	void _dispatch_input_event(const Ref<InputEvent> &p_event);

	void _delete_window(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID);
	void _update_window_rect(const Rect2i &p_rect, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID);
	void _hover_window(DisplaySystemEnums::WindowID p_window_id);

	void _window_update_hdr_state(WindowData &p_window);

	void try_suspend();

	void initialize_tts() const;

public:
	virtual bool has_feature(DisplaySystemEnums::Feature p_feature) const override;

	virtual String get_name() const override;

#ifdef SPEECHD_ENABLED
	virtual bool tts_is_speaking() const override;
	virtual bool tts_is_paused() const override;
	virtual TypedArray<Dictionary> tts_get_voices() const override;

	virtual void tts_speak(const String &p_text, const String &p_voice, int p_volume = 50, float p_pitch = 1.f, float p_rate = 1.f, int64_t p_utterance_id = 0, bool p_interrupt = false) override;
	virtual void tts_pause() override;
	virtual void tts_resume() override;
	virtual void tts_stop() override;
#endif

#ifdef DBUS_ENABLED
	virtual bool is_dark_mode_supported() const override;
	virtual bool is_dark_mode() const override;
	virtual Color get_accent_color() const override;
	virtual void set_system_theme_change_callback(const Callable &p_callable) override;

	virtual Error file_dialog_show(const String &p_title, const String &p_current_directory, const String &p_filename, bool p_show_hidden, DisplaySystemEnums::FileDialogMode p_mode, const Vector<String> &p_filters, const Callable &p_callback, DisplaySystemEnums::WindowID p_window_id) override;
	virtual Error file_dialog_with_options_show(const String &p_title, const String &p_current_directory, const String &p_root, const String &p_filename, bool p_show_hidden, DisplaySystemEnums::FileDialogMode p_mode, const Vector<String> &p_filters, const TypedArray<Dictionary> &p_options, const Callable &p_callback, DisplaySystemEnums::WindowID p_window_id) override;
#endif

	virtual void beep() const override;

	virtual void mouse_set_mode(DisplaySystemEnums::MouseMode p_mode) override;
	virtual DisplaySystemEnums::MouseMode mouse_get_mode() const override;
	virtual void mouse_set_mode_override(DisplaySystemEnums::MouseMode p_mode) override;
	virtual DisplaySystemEnums::MouseMode mouse_get_mode_override() const override;
	virtual void mouse_set_mode_override_enabled(bool p_override_enabled) override;
	virtual bool mouse_is_mode_override_enabled() const override;

	virtual void warp_mouse(const Point2i &p_to) override;
	virtual Point2i mouse_get_position() const override;
	virtual BitField<MouseButtonMask> mouse_get_button_state() const override;

	virtual void clipboard_set(const String &p_text) override;
	virtual String clipboard_get() const override;
	virtual Ref<Image> clipboard_get_image() const override;
	virtual bool clipboard_has_image() const override;
	virtual void clipboard_set_primary(const String &p_text) override;
	virtual String clipboard_get_primary() const override;

	virtual int get_screen_count() const override;
	virtual int get_primary_screen() const override;
	virtual Point2i screen_get_position(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Size2i screen_get_size(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Rect2i screen_get_usable_rect(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual int screen_get_dpi(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual float screen_get_scale(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual float screen_get_refresh_rate(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;

	virtual bool is_touchscreen_available() const override;

	virtual void screen_set_keep_on(bool p_enable) override;
	virtual bool screen_is_kept_on() const override;

	virtual Vector<DisplaySystemEnums::WindowID> get_window_list() const override;

	virtual DisplaySystemEnums::WindowID create_sub_window(DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Rect2i &p_rect = Rect2i(), bool p_exclusive = false, DisplaySystemEnums::WindowID p_transient_parent = DisplaySystemEnums::INVALID_WINDOW_ID) override;
	virtual void show_window(DisplaySystemEnums::WindowID p_id) override;
	virtual void delete_sub_window(DisplaySystemEnums::WindowID p_id) override;

	virtual DisplaySystemEnums::WindowID window_get_active_popup() const override;
	virtual void window_set_popup_safe_rect(DisplaySystemEnums::WindowID p_window, const Rect2i &p_rect) override;
	virtual Rect2i window_get_popup_safe_rect(DisplaySystemEnums::WindowID p_window) const override;

	virtual int64_t window_get_native_handle(DisplaySystemEnums::HandleType p_handle_type, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual DisplaySystemEnums::WindowID get_window_at_screen_position(const Point2i &p_position) const override;

	virtual void window_attach_instance_id(ObjectID p_instance, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual ObjectID window_get_attached_instance_id(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_title(const String &p_title, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_mouse_passthrough(const Vector<Vector2> &p_region, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual void window_set_rect_changed_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_window_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_text_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_drop_files_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual int window_get_current_screen(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void window_set_current_screen(int p_screen, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual Point2i window_get_position(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual Point2i window_get_position_with_decorations(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void window_set_position(const Point2i &p_position, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual void window_set_max_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual Size2i window_get_max_size(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void gl_window_make_current(DisplaySystemEnums::WindowID p_window_id) override;

	virtual void window_set_transient(DisplaySystemEnums::WindowID p_window_id, DisplaySystemEnums::WindowID p_parent) override;

	virtual void window_set_min_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual Size2i window_get_min_size(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual Size2i window_get_size(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual Size2i window_get_size_with_decorations(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual float window_get_scale(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_mode(DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual DisplaySystemEnums::WindowMode window_get_mode(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_icon(const Ref<Image> &p_icon, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual bool window_is_maximize_allowed(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_flag(DisplaySystemEnums::WindowFlags p_flag, bool p_enabled, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual bool window_get_flag(DisplaySystemEnums::WindowFlags p_flag, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_request_attention(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual void window_move_to_foreground(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual bool window_is_focused(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual bool window_can_draw(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual bool can_any_window_draw() const override;

	virtual void window_set_ime_active(const bool p_active, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_ime_position(const Point2i &p_pos, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual int accessibility_should_increase_contrast() const override;
	virtual int accessibility_screen_reader_active() const override;

	virtual Point2i ime_get_selection() const override;
	virtual String ime_get_text() const override;

	virtual void window_set_vsync_mode(DisplaySystemEnums::VSyncMode p_vsync_mode, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual DisplaySystemEnums::VSyncMode window_get_vsync_mode(DisplaySystemEnums::WindowID p_window_id) const override;

	virtual void window_start_drag(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_start_resize(DisplaySystemEnums::WindowResizeEdge p_edge, DisplaySystemEnums::WindowID p_window) override;

	virtual bool window_is_hdr_output_supported(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_request_hdr_output(const bool p_enabled, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual bool window_is_hdr_output_requested(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual bool window_is_hdr_output_enabled(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_hdr_output_reference_luminance(const float p_reference_luminance, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual float window_get_hdr_output_reference_luminance(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual float window_get_hdr_output_current_reference_luminance(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_set_hdr_output_max_luminance(const float p_max_luminance, DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual float window_get_hdr_output_max_luminance(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual float window_get_hdr_output_current_max_luminance(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual float window_get_output_max_linear_value(DisplaySystemEnums::WindowID p_window_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void cursor_set_shape(DisplaySystemEnums::CursorShape p_shape) override;
	virtual DisplaySystemEnums::CursorShape cursor_get_shape() const override;
	virtual void cursor_set_custom_image(const Ref<Resource> &p_cursor, DisplaySystemEnums::CursorShape p_shape, const Vector2 &p_hotspot) override;

	virtual bool get_swap_cancel_ok() override;

	virtual Error embed_process(DisplaySystemEnums::WindowID p_window, ProcessID p_pid, const Rect2i &p_rect, bool p_visible, bool p_grab_focus) override;
	virtual Error request_close_embedded_process(ProcessID p_pid) override;
	virtual Error remove_embedded_process(ProcessID p_pid) override;
	virtual ProcessID get_focused_process_id() override;

	virtual int keyboard_get_layout_count() const override;
	virtual int keyboard_get_current_layout() const override;
	virtual void keyboard_set_current_layout(int p_index) override;
	virtual String keyboard_get_layout_language(int p_index) const override;
	virtual String keyboard_get_layout_name(int p_index) const override;
	virtual Key keyboard_get_keycode_from_physical(Key p_keycode) const override;
	virtual Key keyboard_get_label_from_physical(Key p_keycode) const override;

	virtual bool color_picker(const Callable &p_callback) override;

	virtual void process_events() override;

	virtual void release_rendering_thread() override;
	virtual void swap_buffers() override;

	virtual void set_icon(const Ref<Image> &p_icon) override;

	virtual void set_context(DisplaySystemEnums::Context p_context) override;

	virtual bool is_window_transparency_available() const override;

	static DisplaySystem *create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Point2i *p_position, const Size2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);
	static Vector<String> get_rendering_drivers_func();

	static void register_wayland_driver();

	DisplaySystemWayland(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i &p_resolution, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);
	~DisplaySystemWayland();
};

#endif // WAYLAND_ENABLED
