/**************************************************************************/
/*  display_system_macos_embedded.h                                       */

#pragma once

#include "display_system_macos_base.h"

@class CAContext;
@class CALayer;
class InputEvent;
class NativeMenu;

#ifdef GLES3_ENABLED
class GLManagerEmbedded;
#endif

struct DisplaySystemMacOSEmbeddedState {
	/*! Default to a scale of 2.0, which is the most common. */
	float screen_max_scale = 2.0f;
	float screen_dpi = 96.0f;
	/*! Scale for window displaying embedded content */
	float screen_window_scale = 2.0f;
	/*! The display ID of the window which is displaying the embedded process content. */
	uint32_t display_id = -1;

	/*! The current maximum EDR value for the display. */
	double screen_max_edr = 1.0;
	/*! The maximum possible EDR value for the display. */
	double screen_max_potential_edr = 1.0;

	void serialize(PackedByteArray &r_data);
	Error deserialize(const PackedByteArray &p_data);

	_FORCE_INLINE_ bool operator==(const DisplaySystemMacOSEmbeddedState &p_other) const {
		return screen_max_scale == p_other.screen_max_scale && screen_dpi == p_other.screen_dpi && screen_window_scale == p_other.screen_window_scale && display_id == p_other.display_id && screen_max_edr == p_other.screen_max_edr && screen_max_potential_edr == p_other.screen_max_potential_edr;
	}
};

/// "Embedded" as in "Embedded in the Orblit editor window".
class DisplaySystemMacOSEmbedded : public DisplaySystemMacOSBase {
	ORBLIT_SOFTCLASS(DisplaySystemMacOSEmbedded, DisplaySystemMacOSBase)

	DisplaySystemMacOSEmbeddedState state;

	NativeMenu *native_menu = nullptr;

	HashMap<DisplaySystemEnums::WindowID, ObjectID> window_attached_instance_id;

	HashMap<DisplaySystemEnums::WindowID, Callable> window_event_callbacks;
	HashMap<DisplaySystemEnums::WindowID, Callable> window_resize_callbacks;
	HashMap<DisplaySystemEnums::WindowID, Callable> input_event_callbacks;
	HashMap<DisplaySystemEnums::WindowID, Callable> input_text_callbacks;

	DisplaySystemEnums::WindowID window_id_counter = DisplaySystemEnums::MAIN_WINDOW_ID;

	bool transparent = false;

	CAContext *ca_context = nullptr;
	// Either be a CAMetalLayer or a CALayer depending on the rendering driver.
	CALayer *layer = nullptr;
#ifdef GLES3_ENABLED
	GLManagerEmbedded *gl_manager = nullptr;
#endif

	String rendering_driver;

	HDROutput hdr_output;

	HDROutput &_get_hdr_output(DisplaySystemEnums::WindowID p_window) override;
	const HDROutput &_get_hdr_output(DisplaySystemEnums::WindowID p_window) const override;

	Point2i ime_last_position;

	// Convert source pixel size to render pixel size, accounting for the
	// difference between display scale and render scale when hiDPI is disabled.
	Size2i _source_to_render_size(const Size2i &p_source_size) const;

	void _mouse_apply_mode(DisplaySystemEnums::MouseMode p_prev_mode, DisplaySystemEnums::MouseMode p_new_mode) override;

	struct Joy {
		String name;
		uint64_t timestamp = 0;

		Joy() = default;
		Joy(const String &p_name) :
				name(p_name) {}
	};
	HashMap<int, Joy> joysticks;

public:
	static void register_embedded_driver();
	static DisplaySystem *create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error);
	static Vector<String> get_rendering_drivers_func();

	void _window_set_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID);

	// MARK: - Events

	virtual void process_events() override;

	virtual void window_set_rect_changed_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_window_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_input_text_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_drop_files_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	static void _dispatch_input_events(const Ref<InputEvent> &p_event);
	void send_input_event(const Ref<InputEvent> &p_event, DisplaySystemEnums::WindowID p_id = DisplaySystemEnums::MAIN_WINDOW_ID) const;
	void send_input_text(const String &p_text, DisplaySystemEnums::WindowID p_id = DisplaySystemEnums::MAIN_WINDOW_ID) const;
	virtual void send_window_event_by_id(DisplaySystemEnums::WindowEvent p_event, DisplaySystemEnums::WindowID p_id = DisplaySystemEnums::MAIN_WINDOW_ID) const override;
	void _window_callback(const Callable &p_callable, const Variant &p_arg) const;

	// MARK: - Mouse
	virtual void warp_mouse(const Point2i &p_position) override;
	virtual Point2i mouse_get_position() const override;
	virtual BitField<MouseButtonMask> mouse_get_button_state() const override;

	// MARK: - Window

	virtual bool has_feature(DisplaySystemEnums::Feature p_feature) const override;
	virtual String get_name() const override;

	virtual int get_screen_count() const override;
	virtual Point2i screen_get_position(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Size2i screen_get_size(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Rect2i screen_get_usable_rect(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual int screen_get_dpi(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual float screen_get_scale(int p_screen = DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW) const override;
	virtual Vector<DisplaySystemEnums::WindowID> get_window_list() const override;

	virtual DisplaySystemEnums::WindowID get_window_at_screen_position(const Point2i &p_position) const override;

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
	virtual void window_set_taskbar_progress_value(float p_value, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_taskbar_progress_state(DisplaySystemEnums::ProgressState p_state, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_move_to_foreground(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual bool window_is_focused(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual float screen_get_max_scale() const override;

	virtual bool window_can_draw(DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) const override;

	virtual bool can_any_window_draw() const override;

	virtual void window_set_ime_active(const bool p_active, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual void window_set_ime_position(const Point2i &p_pos, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;

	virtual void window_set_vsync_mode(DisplaySystemEnums::VSyncMode p_vsync_mode, DisplaySystemEnums::WindowID p_window = DisplaySystemEnums::MAIN_WINDOW_ID) override;
	virtual DisplaySystemEnums::VSyncMode window_get_vsync_mode(DisplaySystemEnums::WindowID p_vsync_mode) const override;

	virtual void cursor_set_shape(DisplaySystemEnums::CursorShape p_shape) override;
	virtual void cursor_set_custom_image(const Ref<Resource> &p_cursor, DisplaySystemEnums::CursorShape p_shape = DisplaySystemEnums::CURSOR_ARROW, const Vector2 &p_hotspot = Vector2()) override;

	void window_get_edr_values(DisplaySystemEnums::WindowID p_window, CGFloat *r_max_potential_edr_value, CGFloat *r_max_edr_value) const override;
	void update_screen_parameters();

	void set_state(const DisplaySystemMacOSEmbeddedState &p_state);
	virtual void swap_buffers() override;

	DisplaySystemMacOSEmbedded(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, Error &r_error);
	~DisplaySystemMacOSEmbedded();
};
