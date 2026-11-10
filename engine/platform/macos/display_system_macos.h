/**************************************************************************/
/*  display_system_macos.h                                                */

#pragma once

#include "display_system_macos_base.h"

#include "core/os/process_id.h"

#define FontVariation __FontVariation
#define BitMap _QDBitMap // Suppress deprecated QuickDraw definition.

#import <AppKit/AppKit.h>
#import <AppKit/NSCursor.h>
#import <ApplicationServices/ApplicationServices.h>
#import <CoreVideo/CoreVideo.h>
#import <Foundation/Foundation.h>
#import <IOKit/pwr_mgt/IOPMLib.h>

#undef BitMap
#undef FontVariation

@class OrblitWindow;
@class OrblitContentView;
@class OrblitWindowDelegate;
@class OrblitButtonView;
@class OrblitProgressView;

class InputEvent;
class InputEventWithModifiers;
class NativeMenuMacOS;

#ifdef TOOLS_ENABLED
class EmbeddedProcessMacOS;
#endif

#ifdef GLES3_ENABLED
class GLManagerLegacy_MacOS;
#ifdef ANGLE_ENABLED
class GLManagerANGLE_MacOS;
#endif
#endif

class DisplaySystemMacOS : public DisplaySystemMacOSBase {
	ORBLIT_SOFTCLASS(DisplaySystemMacOS, DisplaySystemMacOSBase);

public:
	struct KeyEvent {
		DisplaySystemEnums::WindowID window_id = DisplaySystemEnums::INVALID_WINDOW_ID;
		unsigned int macos_state = false;
		bool pressed = false;
		bool echo = false;
		bool raw = false;
		bool ime = false;
		Key keycode = Key::NONE;
		Key physical_keycode = Key::NONE;
		Key key_label = Key::NONE;
		uint32_t unicode = 0;
		KeyLocation location = KeyLocation::UNSPECIFIED;
	};

	struct WindowData {
		OrblitWindowDelegate *window_delegate;
		OrblitWindow *window_object;
		OrblitContentView *window_view;
		OrblitButtonView *window_button_view;

		Vector<Vector2> mpath;

		CGDirectDisplayID display_id = -1;

		Point2i mouse_pos;
		DisplaySystemEnums::WindowResizeEdge edge = DisplaySystemEnums::WINDOW_EDGE_MAX;

		Size2i min_size;
		Size2i max_size;
		Size2i size;
		Vector2i wb_offset = Vector2i(14, 14);

		NSRect last_frame_rect = NSMakeRect(0, 0, 0, 0);
		NSRect pre_zoom_rect = NSMakeRect(0, 0, 0, 0);

		bool im_active = false;
		Size2i im_position;

		Callable rect_changed_callback;
		Callable event_callback;
		Callable input_event_callback;
		Callable input_text_callback;
		Callable drop_files_callback;

		ObjectID instance_id;
		bool fs_transition = false;
		bool initial_size = true;

		DisplaySystemEnums::WindowID transient_parent = DisplaySystemEnums::INVALID_WINDOW_ID;
		bool exclusive = false;
		HashSet<DisplaySystemEnums::WindowID> transient_children;

		bool layered_window = false;
		bool fullscreen = false;
		bool exclusive_fullscreen = false;
		bool on_top = false;
		bool borderless = false;
		bool resize_disabled = false;
		bool no_min_btn = false;
		bool no_max_btn = false;
		bool no_focus = false;
		bool is_popup = false;
		bool mpass = false;
		bool focused = false;
		bool is_visible = true;
		bool extend_to_title = false;
		bool hide_from_capture = false;

		HDROutput hdr_output;

		Rect2i parent_safe_rect;
	};

	List<DisplaySystemEnums::WindowID> popup_list;
	uint64_t time_since_popup = 0;

	OrblitProgressView *dock_progress = nullptr;

private:
	id screen_observer = nil;

#if defined(GLES3_ENABLED)
	GLManagerLegacy_MacOS *gl_manager_legacy = nullptr;
#if defined(ANGLE_ENABLED)
	GLManagerANGLE_MacOS *gl_manager_angle = nullptr;
#endif
#endif
	String rendering_driver;

	struct WarpEvent {
		NSTimeInterval timestamp;
		NSPoint delta;
	};
	List<WarpEvent> warp_events;
	NSTimeInterval last_warp = 0;
	bool ignore_warp = false;

	Vector<KeyEvent> key_event_buffer;
	int key_event_pos = 0;

	id menu_delegate = nullptr;
	NativeMenuMacOS *native_menu = nullptr;

	void _mouse_apply_mode(DisplaySystemEnums::MouseMode p_prev_mode, DisplaySystemEnums::MouseMode p_new_mode) override;

	HDROutput &_get_hdr_output(DisplaySystemEnums::WindowID p_window) override;
	const HDROutput &_get_hdr_output(DisplaySystemEnums::WindowID p_window) const override;

	bool drop_events = false;
	bool in_dispatch_input_event = false;

	DisplaySystemEnums::WindowID window_mouseover_id = DisplaySystemEnums::INVALID_WINDOW_ID;
	DisplaySystemEnums::WindowID last_focused_window = DisplaySystemEnums::INVALID_WINDOW_ID;
	DisplaySystemEnums::WindowID window_id_counter = DisplaySystemEnums::MAIN_WINDOW_ID;
	float display_max_scale = 1.f;
	mutable Point2i origin;
	mutable bool displays_arrangement_dirty = true;
	bool is_resizing = false;

	NSCursor *cursors[DisplaySystemEnums::CURSOR_MAX];
	HashMap<DisplaySystemEnums::CursorShape, Vector<Variant>> cursors_cache;

	HashMap<DisplaySystemEnums::WindowID, WindowData> windows;

	struct IndicatorData {
		id delegate;
		id item;
	};

	DisplaySystemEnums::IndicatorID indicator_id_counter = 0;
	HashMap<DisplaySystemEnums::IndicatorID, IndicatorData> indicators;

	Callable help_search_callback;
	Callable help_action_callback;

	struct MenuCall {
		Variant tag;
		Callable callback;
	};
	List<MenuCall> deferred_menu_calls;

	DisplaySystemEnums::WindowID _create_window(DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, const Rect2i &p_rect);
	void _update_window_style(WindowData p_wd, DisplaySystemEnums::WindowID p_window);

	void _update_displays_arrangement() const;
	Point2i _get_native_screen_position(int p_screen) const;
	static void _displays_arrangement_changed(CGDirectDisplayID display_id, CGDisplayChangeSummaryFlags flags, void *user_info);

	static void _dispatch_input_events(const Ref<InputEvent> &p_event);
	void _dispatch_input_event(const Ref<InputEvent> &p_event);
	void _push_input(const Ref<InputEvent> &p_event);
	void _process_key_events();

	static NSCursor *_cursor_from_selector(SEL p_selector, SEL p_fallback = nil);

	Error _file_dialog_with_options_show(const String &p_title, const String &p_current_directory, const String &p_root, const String &p_filename, bool p_show_hidden, DisplaySystemEnums::FileDialogMode p_mode, const Vector<String> &p_filters, const TypedArray<Dictionary> &p_options, const Callable &p_callback, bool p_options_in_cb, DisplaySystemEnums::WindowID p_window_id);

#ifdef TOOLS_ENABLED
	struct EmbeddedProcessData {
		EmbeddedProcessMacOS *process = nullptr;
		WindowData *wd = nullptr;
		CALayer *layer_host = nil;
	};
	HashMap<ProcessID, EmbeddedProcessData> embedded_processes;
#endif
	void _window_update_display_id(WindowData *p_wd);

public:
	void menu_callback(id p_sender);

	virtual bool has_window(DisplaySystemEnums::WindowID p_window) const override;
	WindowData &get_window(DisplaySystemEnums::WindowID p_window);

	NSImage *_convert_to_nsimg(Ref<Image> &p_image) const;
	Point2i _get_screens_origin() const;

	void set_menu_delegate(NSMenu *p_menu);

	void send_event(NSEvent *p_event);
	void send_window_event(const WindowData &p_wd, DisplaySystemEnums::WindowEvent p_event) const;
	virtual void send_window_event_by_id(DisplaySystemEnums::WindowEvent p_event, DisplaySystemEnums::WindowID p_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	void release_pressed_events();
	void sync_mouse_state();
	void get_key_modifier_state(unsigned int p_macos_state, Ref<InputEventWithModifiers> r_state) const;
	void update_mouse_pos(WindowData &p_wd, NSPoint p_location_in_window);
	void push_to_key_event_buffer(const KeyEvent &p_event);
	void pop_last_key_event();
	void set_last_focused_window(DisplaySystemEnums::WindowID p_window);
	bool mouse_process_popups(bool p_close = false);
	void popup_open(DisplaySystemEnums::WindowID p_window);
	void popup_close(DisplaySystemEnums::WindowID p_window);
	void set_is_resizing(bool p_is_resizing);
	bool get_is_resizing() const;
	void reparent_check(DisplaySystemEnums::WindowID p_window);
	DisplaySystemEnums::WindowID _get_focused_window_or_popup() const;
	void mouse_enter_window(DisplaySystemEnums::WindowID p_window);
	void mouse_exit_window(DisplaySystemEnums::WindowID p_window);
	DisplaySystemEnums::WindowID get_mouseover_window() const { return window_mouseover_id; }
	void update_presentation_mode();

	bool is_always_on_top_recursive(DisplaySystemEnums::WindowID p_window) const;

	/**
	 * Get the display ID of a window.
	 */
	uint32_t window_get_display_id(DisplaySystemEnums::WindowID p_window) const;
	void window_destroy(DisplaySystemEnums::WindowID p_window);
	void window_resize(DisplaySystemEnums::WindowID p_window, int p_width, int p_height);
	void window_set_custom_window_buttons(WindowData &p_wd, bool p_enabled);
	void set_window_per_pixel_transparency_enabled(bool p_enabled, DisplaySystemEnums::WindowID p_window);

	virtual bool has_feature(DisplaySystemEnums::Feature p_feature) const override;
	virtual String get_name() const override;

	virtual void help_set_search_callbacks(const Callable &p_search_callback = Callable(), const Callable &p_action_callback = Callable()) override;
	Callable _help_get_search_callback() const;
	Callable _help_get_action_callback() const;

	virtual Error dialog_show(String p_title, String p_description, Vector<String> p_buttons, const Callable &p_callback) override;
	virtual Error dialog_input_text(String p_title, String p_description, String p_partial, const Callable &p_callback) override;

	virtual Error file_dialog_show(const String &p_title, const String &p_current_directory, const String &p_filename, bool p_show_hidden, DisplaySystemEnums::FileDialogMode p_mode, const Vector<String> &p_filters, const Callable &p_callback, DisplaySystemEnums::WindowID p_window_id) override;
	virtual Error file_dialog_with_options_show(const String &p_title, const String &p_current_directory, const String &p_root, const String &p_filename, bool p_show_hidden, DisplaySystemEnums::FileDialogMode p_mode, const Vector<String> &p_filters, const TypedArray<Dictionary> &p_options, const Callable &p_callback, DisplaySystemEnums::WindowID p_window_id) override;

	bool update_mouse_wrap(WindowData &p_wd, NSPoint &r_delta, NSPoint &r_mpos, NSTimeInterval p_timestamp);
	virtual void warp_mouse(const Point2i &p_position) override;
	virtual Point2i mouse_get_position() const override;
	virtual BitField<MouseButtonMask> mouse_get_button_state() const override;

	virtual TypedArray<Rect2> get_display_cutouts(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Rect2i get_display_safe_area(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;

	virtual int get_screen_count() const override;
	virtual int get_keyboard_focus_screen() const override;
	virtual Point2i screen_get_position(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Size2i screen_get_size(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual int screen_get_dpi(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual float screen_get_scale(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual float screen_get_max_scale() const override;
	virtual Rect2i screen_get_usable_rect(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Color screen_get_pixel(const Point2i &p_position) const override;
	virtual Ref<Image> screen_get_image(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Ref<Image> screen_get_image_rect(const Rect2i &p_rect) const override;
	virtual Vector<int> get_window_list() const override;

	virtual DisplaySystemEnums::WindowID create_sub_window(DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Rect2i &p_rect = Rect2i(), bool p_exclusive = false, DisplaySystemEnums::WindowID p_transient_parent = DisplaySystemEnums::INVALID_WINDOW_ID) override;
	virtual void show_window(DisplaySystemEnums::WindowID p_id) override;
	virtual void delete_sub_window(DisplaySystemEnums::WindowID p_id) override;

	virtual DisplaySystemEnums::WindowID window_get_active_popup() const override;
	virtual void window_set_popup_safe_rect(DisplaySystemEnums::WindowID p_window, const Rect2i &p_rect) override;
	virtual Rect2i window_get_popup_safe_rect(DisplaySystemEnums::WindowID p_window) const override;

	virtual void window_set_rect_changed_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_window_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_text_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_drop_files_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual void window_set_title(const String &p_title, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual Size2i window_get_title_size(const String &p_title, DisplaySystemEnums::WindowID p_window) const override;
	virtual void window_set_mouse_passthrough(const Vector<Vector2> &p_region, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual int window_get_current_screen(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void window_set_current_screen(int p_screen, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual Point2i window_get_position(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual Point2i window_get_position_with_decorations(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void window_set_position(const Point2i &p_position, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual void window_set_transient(DisplaySystemEnums::WindowID p_window, DisplaySystemEnums::WindowID p_parent) override;
	virtual void window_set_exclusive(DisplaySystemEnums::WindowID p_window, bool p_exclusive) override;

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
	virtual void window_set_taskbar_progress_value(float p_value, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_taskbar_progress_state(DisplaySystemEnums::ProgressState p_state, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_move_to_foreground(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual bool window_is_focused(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual DisplaySystemEnums::WindowID get_focused_window() const override;

	virtual bool window_can_draw(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual bool can_any_window_draw() const override;

	virtual void window_set_ime_active(const bool p_active, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_ime_position(const Point2i &p_pos, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual DisplaySystemEnums::WindowID get_window_at_screen_position(const Point2i &p_position) const override;

	virtual int64_t window_get_native_handle(DisplaySystemEnums::HandleType p_handle_type, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual void window_attach_instance_id(ObjectID p_instance, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual ObjectID window_get_attached_instance_id(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	virtual void gl_window_make_current(DisplaySystemEnums::WindowID p_window_id) override;

	virtual void window_set_vsync_mode(DisplaySystemEnums::VSyncMode p_vsync_mode, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual DisplaySystemEnums::VSyncMode window_get_vsync_mode(DisplaySystemEnums::WindowID p_vsync_mode) const override;

public:
	void update_screen_parameters();

	virtual bool window_maximize_on_title_dbl_click() const override;
	virtual bool window_minimize_on_title_dbl_click() const override;

	virtual void window_start_drag(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_start_resize(DisplaySystemEnums::WindowResizeEdge p_edge, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual void window_set_window_buttons_offset(const Vector2i &p_offset, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual Vector3i window_get_safe_title_margins(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	void cursor_update_shape();
	virtual void cursor_set_shape(DisplaySystemEnums::CursorShape p_shape) override;
	virtual void cursor_set_custom_image(const Ref<Resource> &p_cursor, DisplaySystemEnums::CursorShape p_shape = DisplaySystemEnums::CURSOR_ARROW, const Vector2 &p_hotspot = Vector2()) override;

	virtual bool get_swap_cancel_ok() override;

	virtual void enable_for_stealing_focus(ProcessID pid) override;
#ifdef TOOLS_ENABLED
	Error embed_process_update(DisplaySystemEnums::WindowID p_window, EmbeddedProcessMacOS *p_process);
	virtual Error request_close_embedded_process(ProcessID p_pid) override;
	virtual Error remove_embedded_process(ProcessID p_pid) override;
#endif

	void _process_events(bool p_pump);
	virtual void process_events() override;
	virtual void force_process_and_drop_events() override;

	virtual void release_rendering_thread() override;
	virtual void swap_buffers() override;

	virtual void set_native_icon(const String &p_filename) override;
	virtual void set_icon(const Ref<Image> &p_icon) override;

	virtual DisplaySystemEnums::IndicatorID create_status_indicator(const Ref<Texture2D> &p_icon, const String &p_tooltip, const Callable &p_callback) override;
	virtual void status_indicator_set_icon(DisplaySystemEnums::IndicatorID p_id, const Ref<Texture2D> &p_icon) override;
	virtual void status_indicator_set_tooltip(DisplaySystemEnums::IndicatorID p_id, const String &p_tooltip) override;
	virtual void status_indicator_set_menu(DisplaySystemEnums::IndicatorID p_id, const RID &p_menu_rid) override;
	virtual void status_indicator_set_callback(DisplaySystemEnums::IndicatorID p_id, const Callable &p_callback) override;
	virtual Rect2 status_indicator_get_rect(DisplaySystemEnums::IndicatorID p_id) const override;
	virtual void delete_status_indicator(DisplaySystemEnums::IndicatorID p_id) override;

	virtual bool is_window_transparency_available() const override;

	void window_get_edr_values(DisplaySystemEnums::WindowID p_window, CGFloat *r_max_potential_edr_value, CGFloat *r_max_edr_value) const override;

	static DisplaySystem *create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);
	static Vector<String> get_rendering_drivers_func();

	static void register_macos_driver();

	DisplaySystemMacOS(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);
	~DisplaySystemMacOS();
};
