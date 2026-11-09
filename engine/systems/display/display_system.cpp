/**************************************************************************/
/*  display_system.cpp                                                    */

#include "display_system.h"

STATIC_ASSERT_INCOMPLETE_TYPE(class, Input);
STATIC_ASSERT_INCOMPLETE_TYPE(class, NativeMenu);
STATIC_ASSERT_INCOMPLETE_TYPE(class, Texture2D);
STATIC_ASSERT_INCOMPLETE_TYPE(class, RenderingSystem);

#include "core/input/input.h"
#include "core/object/class_registry.h"
#include "core/os/os.h"
#include "scene/resources/texture.h"
#include "systems/display/accessibility_system.h"
#include "systems/display/display_system_headless.h"
#include "systems/display/native_menu.h"
#include "systems/rendering/rendering_system.h"

#if defined(RD_ENABLED)
#include "systems/rendering/rendering_device.h"
#endif

#if defined(VULKAN_ENABLED)
#include "backends/vulkan/rendering_context_driver_vulkan.h"
#endif
#if defined(D3D12_ENABLED)
#include "backends/d3d12/rendering_context_driver_d3d12.h"
#endif
#if defined(METAL_ENABLED)
#include "backends/metal/rendering_context_driver_metal.h"
#endif

DisplaySystem *DisplaySystem::singleton = nullptr;

bool DisplaySystem::window_early_clear_override_enabled = false;
Color DisplaySystem::window_early_clear_override_color = Color(0, 0, 0, 0);

DisplaySystem::DisplaySystemCreate DisplaySystem::system_create_functions[DisplaySystem::MAX_SYSTEMS] = {
	{ "headless", &DisplaySystemHeadless::create_func, &DisplaySystemHeadless::get_rendering_drivers_func }
};

int DisplaySystem::system_create_count = 1;

void DisplaySystem::help_set_search_callbacks(const Callable &p_search_callback, const Callable &p_action_callback) {
	WARN_PRINT("Native help is not supported by this display server.");
}

bool DisplaySystem::tts_is_speaking() const {
	WARN_PRINT("TTS is not supported by this display server.");
	return false;
}

bool DisplaySystem::tts_is_paused() const {
	WARN_PRINT("TTS is not supported by this display server.");
	return false;
}

void DisplaySystem::tts_pause() {
	WARN_PRINT("TTS is not supported by this display server.");
}

void DisplaySystem::tts_resume() {
	WARN_PRINT("TTS is not supported by this display server.");
}

TypedArray<Dictionary> DisplaySystem::tts_get_voices() const {
	WARN_PRINT("TTS is not supported by this display server.");
	return TypedArray<Dictionary>();
}

PackedStringArray DisplaySystem::tts_get_voices_for_language(const String &p_language) const {
	PackedStringArray ret;
	TypedArray<Dictionary> voices = tts_get_voices();
	for (int i = 0; i < voices.size(); i++) {
		const Dictionary &voice = voices[i];
		if (voice.has("id") && voice.has("language") && voice["language"].operator String().begins_with(p_language)) {
			ret.push_back(voice["id"]);
		}
	}
	return ret;
}

void DisplaySystem::tts_speak(const String &p_text, const String &p_voice, int p_volume, float p_pitch, float p_rate, int64_t p_utterance_id, bool p_interrupt) {
	WARN_PRINT("TTS is not supported by this display server.");
}

void DisplaySystem::tts_stop() {
	WARN_PRINT("TTS is not supported by this display server.");
}

void DisplaySystem::tts_set_utterance_callback(DisplaySystemEnums::TTSUtteranceEvent p_event, const Callable &p_callable) {
	ERR_FAIL_INDEX(p_event, DisplaySystemEnums::TTS_UTTERANCE_MAX);
	utterance_callback[p_event] = p_callable;
}

void DisplaySystem::tts_post_utterance_event(DisplaySystemEnums::TTSUtteranceEvent p_event, int64_t p_id, int p_pos) {
	ERR_FAIL_INDEX(p_event, DisplaySystemEnums::TTS_UTTERANCE_MAX);
	switch (p_event) {
		case DisplaySystemEnums::TTS_UTTERANCE_STARTED:
		case DisplaySystemEnums::TTS_UTTERANCE_ENDED:
		case DisplaySystemEnums::TTS_UTTERANCE_CANCELED: {
			if (utterance_callback[p_event].is_valid()) {
				utterance_callback[p_event].call_deferred(p_id); // Should be deferred, on some platforms utterance events can be called from different threads in a rapid succession.
			}
		} break;
		case DisplaySystemEnums::TTS_UTTERANCE_BOUNDARY: {
			if (utterance_callback[p_event].is_valid()) {
				utterance_callback[p_event].call_deferred(p_pos, p_id); // Should be deferred, on some platforms utterance events can be called from different threads in a rapid succession.
			}
		} break;
		default:
			break;
	}
}

bool DisplaySystem::_get_window_early_clear_override(Color &r_color) {
	if (window_early_clear_override_enabled) {
		r_color = window_early_clear_override_color;
		return true;
	} else if (RenderingSystem::get_singleton()) {
		r_color = RenderingSystem::get_singleton()->get_default_clear_color();
		return true;
	} else {
		return false;
	}
}

void DisplaySystem::set_early_window_clear_color_override(bool p_enabled, Color p_color) {
	window_early_clear_override_enabled = p_enabled;
	window_early_clear_override_color = p_color;
}

void DisplaySystem::mouse_set_mode(DisplaySystemEnums::MouseMode p_mode) {
	WARN_PRINT("Mouse is not supported by this display server.");
}

DisplaySystemEnums::MouseMode DisplaySystem::mouse_get_mode() const {
	return DisplaySystemEnums::MOUSE_MODE_VISIBLE;
}

void DisplaySystem::mouse_set_mode_override(DisplaySystemEnums::MouseMode p_mode) {
	WARN_PRINT("Mouse is not supported by this display server.");
}

DisplaySystemEnums::MouseMode DisplaySystem::mouse_get_mode_override() const {
	return DisplaySystemEnums::MOUSE_MODE_VISIBLE;
}

void DisplaySystem::mouse_set_mode_override_enabled(bool p_override_enabled) {
	WARN_PRINT("Mouse is not supported by this display server.");
}

bool DisplaySystem::mouse_is_mode_override_enabled() const {
	return false;
}

void DisplaySystem::warp_mouse(const Point2i &p_position) {
}

Point2i DisplaySystem::mouse_get_position() const {
	ERR_FAIL_V_MSG(Point2i(), "Mouse is not supported by this display server.");
}

BitField<MouseButtonMask> DisplaySystem::mouse_get_button_state() const {
	ERR_FAIL_V_MSG(MouseButtonMask::NONE, "Mouse is not supported by this display server.");
}

void DisplaySystem::clipboard_set(const String &p_text) {
	WARN_PRINT("Clipboard is not supported by this display server.");
}

String DisplaySystem::clipboard_get() const {
	ERR_FAIL_V_MSG(String(), "Clipboard is not supported by this display server.");
}

Ref<Image> DisplaySystem::clipboard_get_image() const {
	ERR_FAIL_V_MSG(Ref<Image>(), "Clipboard is not supported by this display server.");
}

bool DisplaySystem::clipboard_has() const {
	return !clipboard_get().is_empty();
}

bool DisplaySystem::clipboard_has_image() const {
	return clipboard_get_image().is_valid();
}

void DisplaySystem::clipboard_set_primary(const String &p_text) {
	WARN_PRINT("Primary clipboard is not supported by this display server.");
}

String DisplaySystem::clipboard_get_primary() const {
	ERR_FAIL_V_MSG(String(), "Primary clipboard is not supported by this display server.");
}

void DisplaySystem::screen_set_orientation(DisplaySystemEnums::ScreenOrientation p_orientation, int p_screen) {
	WARN_PRINT("Orientation not supported by this display server.");
}

DisplaySystemEnums::ScreenOrientation DisplaySystem::screen_get_orientation(int p_screen) const {
	return DisplaySystemEnums::SCREEN_LANDSCAPE;
}

float DisplaySystem::screen_get_scale(int p_screen) const {
	return 1.0f;
}

bool DisplaySystem::is_touchscreen_available() const {
	return Input::get_singleton() && Input::get_singleton()->is_emulating_touch_from_mouse();
}

void DisplaySystem::screen_set_keep_on(bool p_enable) {
	WARN_PRINT("Keeping screen on not supported by this display server.");
}

bool DisplaySystem::screen_is_kept_on() const {
	return false;
}

int DisplaySystem::get_screen_from_rect(const Rect2 &p_rect) const {
	int nearest_area = 0;
	int pos_screen = DisplaySystemEnums::INVALID_SCREEN;
	for (int i = 0; i < get_screen_count(); i++) {
		Rect2i r;
		r.position = screen_get_position(i);
		r.size = screen_get_size(i);
		Rect2 inters = r.intersection(p_rect);
		int area = inters.size.width * inters.size.height;
		if (area > nearest_area) {
			pos_screen = i;
			nearest_area = area;
		}
	}
	return pos_screen;
}

DisplaySystemEnums::WindowID DisplaySystem::create_sub_window(DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Rect2i &p_rect, bool p_exclusive, DisplaySystemEnums::WindowID p_transient_parent) {
	ERR_FAIL_V_MSG(DisplaySystemEnums::INVALID_WINDOW_ID, "Sub-windows not supported by this display server.");
}

void DisplaySystem::show_window(DisplaySystemEnums::WindowID p_id) {
	ERR_FAIL_MSG("Sub-windows not supported by this display server.");
}

void DisplaySystem::delete_sub_window(DisplaySystemEnums::WindowID p_id) {
	ERR_FAIL_MSG("Sub-windows not supported by this display server.");
}

void DisplaySystem::window_set_exclusive(DisplaySystemEnums::WindowID p_window, bool p_exclusive) {
	// Do nothing, if not supported.
}

void DisplaySystem::window_set_mouse_passthrough(const Vector<Vector2> &p_region, DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_MSG("Mouse passthrough not supported by this display server.");
}

void DisplaySystem::gl_window_make_current(DisplaySystemEnums::WindowID p_window_id) {
	// noop except in gles
}

void DisplaySystem::window_set_ime_active(const bool p_active, DisplaySystemEnums::WindowID p_window) {
	WARN_PRINT("IME not supported by this display server.");
}

void DisplaySystem::window_set_ime_position(const Point2i &p_pos, DisplaySystemEnums::WindowID p_window) {
	WARN_PRINT("IME not supported by this display server.");
}

Point2i DisplaySystem::ime_get_selection() const {
	ERR_FAIL_V_MSG(Point2i(), "IME or NOTIFICATION_WM_IME_UPDATE not supported by this display server.");
}

String DisplaySystem::ime_get_text() const {
	ERR_FAIL_V_MSG(String(), "IME or NOTIFICATION_WM_IME_UPDATE not supported by this display server.");
}

void DisplaySystem::virtual_keyboard_show(const String &p_existing_text, const Rect2 &p_screen_rect, DisplaySystemEnums::VirtualKeyboardType p_type, int p_max_length, int p_cursor_start, int p_cursor_end) {
	WARN_PRINT("Virtual keyboard not supported by this display server.");
}

void DisplaySystem::virtual_keyboard_hide() {
	WARN_PRINT("Virtual keyboard not supported by this display server.");
}

// returns height of the currently shown keyboard (0 if keyboard is hidden)
int DisplaySystem::virtual_keyboard_get_height() const {
	WARN_PRINT("Virtual keyboard not supported by this display server.");
	return 0;
}

bool DisplaySystem::has_hardware_keyboard() const {
	return true;
}

void DisplaySystem::cursor_set_shape(DisplaySystemEnums::CursorShape p_shape) {
	WARN_PRINT("Cursor shape not supported by this display server.");
}

DisplaySystemEnums::CursorShape DisplaySystem::cursor_get_shape() const {
	return DisplaySystemEnums::CURSOR_ARROW;
}

void DisplaySystem::cursor_set_custom_image(const Ref<Resource> &p_cursor, DisplaySystemEnums::CursorShape p_shape, const Vector2 &p_hotspot) {
	WARN_PRINT("Custom cursor shape not supported by this display server.");
}

DisplaySystemEnums::NotificationID DisplaySystem::send_toast_notification(const String &p_title, const String &p_text, const Ref<Texture2D> &p_image, const Callable &p_callback) {
	return DisplaySystemEnums::INVALID_NOTIFICATION_ID;
}

void DisplaySystem::hide_toast_notification(DisplaySystemEnums::NotificationID p_id) {}

bool DisplaySystem::get_swap_cancel_ok() {
	return false;
}

void DisplaySystem::enable_for_stealing_focus(ProcessID pid) {
}

Error DisplaySystem::embed_process(DisplaySystemEnums::WindowID p_window, ProcessID p_pid, const Rect2i &p_rect, bool p_visible, bool p_grab_focus) {
	WARN_PRINT("Embedded process not supported by this display server.");
	return ERR_UNAVAILABLE;
}

Error DisplaySystem::request_close_embedded_process(ProcessID p_pid) {
	WARN_PRINT("Embedded process not supported by this display server.");
	return ERR_UNAVAILABLE;
}

Error DisplaySystem::remove_embedded_process(ProcessID p_pid) {
	WARN_PRINT("Embedded process not supported by this display server.");
	return ERR_UNAVAILABLE;
}

ProcessID DisplaySystem::get_focused_process_id() {
	WARN_PRINT("Embedded process not supported by this display server.");
	return 0;
}

Error DisplaySystem::dialog_show(String p_title, String p_description, Vector<String> p_buttons, const Callable &p_callback) {
	WARN_PRINT("Native dialogs not supported by this display server.");
	return ERR_UNAVAILABLE;
}

Error DisplaySystem::dialog_input_text(String p_title, String p_description, String p_partial, const Callable &p_callback) {
	WARN_PRINT("Native dialogs not supported by this display server.");
	return ERR_UNAVAILABLE;
}

Error DisplaySystem::file_dialog_show(const String &p_title, const String &p_current_directory, const String &p_filename, bool p_show_hidden, DisplaySystemEnums::FileDialogMode p_mode, const Vector<String> &p_filters, const Callable &p_callback, DisplaySystemEnums::WindowID p_window_id) {
	WARN_PRINT("Native dialogs not supported by this display server.");
	return ERR_UNAVAILABLE;
}

Error DisplaySystem::file_dialog_with_options_show(const String &p_title, const String &p_current_directory, const String &p_root, const String &p_filename, bool p_show_hidden, DisplaySystemEnums::FileDialogMode p_mode, const Vector<String> &p_filters, const TypedArray<Dictionary> &p_options, const Callable &p_callback, DisplaySystemEnums::WindowID p_window_id) {
	WARN_PRINT("Native dialogs not supported by this display server.");
	return ERR_UNAVAILABLE;
}

void DisplaySystem::beep() const {
}

int DisplaySystem::keyboard_get_layout_count() const {
	return 0;
}

int DisplaySystem::keyboard_get_current_layout() const {
	return -1;
}

void DisplaySystem::keyboard_set_current_layout(int p_index) {
}

String DisplaySystem::keyboard_get_layout_language(int p_index) const {
	return "";
}

String DisplaySystem::keyboard_get_layout_name(int p_index) const {
	return "Not supported";
}

Key DisplaySystem::keyboard_get_keycode_from_physical(Key p_keycode) const {
	ERR_FAIL_V_MSG(p_keycode, "Not supported by this display server.");
}

Key DisplaySystem::keyboard_get_label_from_physical(Key p_keycode) const {
	ERR_FAIL_V_MSG(p_keycode, "Not supported by this display server.");
}

void DisplaySystem::show_emoji_and_symbol_picker() const {
}

bool DisplaySystem::color_picker(const Callable &p_callback) {
	return false;
}

void DisplaySystem::force_process_and_drop_events() {
}

void DisplaySystem::release_rendering_thread() {
	WARN_PRINT("Rendering thread not supported by this display server.");
}

void DisplaySystem::swap_buffers() {
	WARN_PRINT("Swap buffers not supported by this display server.");
}

void DisplaySystem::set_native_icon(const String &p_filename) {
	WARN_PRINT("Native icon not supported by this display server.");
}

void DisplaySystem::set_icon(const Ref<Image> &p_icon) {
	WARN_PRINT("Icon not supported by this display server.");
}

DisplaySystemEnums::IndicatorID DisplaySystem::create_status_indicator(const Ref<Texture2D> &p_icon, const String &p_tooltip, const Callable &p_callback) {
	WARN_PRINT("Status indicator not supported by this display server.");
	return DisplaySystemEnums::INVALID_INDICATOR_ID;
}

void DisplaySystem::status_indicator_set_icon(DisplaySystemEnums::IndicatorID p_id, const Ref<Texture2D> &p_icon) {
	WARN_PRINT("Status indicator not supported by this display server.");
}

void DisplaySystem::status_indicator_set_tooltip(DisplaySystemEnums::IndicatorID p_id, const String &p_tooltip) {
	WARN_PRINT("Status indicator not supported by this display server.");
}

void DisplaySystem::status_indicator_set_menu(DisplaySystemEnums::IndicatorID p_id, const RID &p_menu_rid) {
	WARN_PRINT("Status indicator not supported by this display server.");
}

void DisplaySystem::status_indicator_set_callback(DisplaySystemEnums::IndicatorID p_id, const Callable &p_callback) {
	WARN_PRINT("Status indicator not supported by this display server.");
}

Rect2 DisplaySystem::status_indicator_get_rect(DisplaySystemEnums::IndicatorID p_id) const {
	WARN_PRINT("Status indicator not supported by this display server.");
	return Rect2();
}

void DisplaySystem::delete_status_indicator(DisplaySystemEnums::IndicatorID p_id) {
	WARN_PRINT("Status indicator not supported by this display server.");
}

int64_t DisplaySystem::window_get_native_handle(DisplaySystemEnums::HandleType p_handle_type, DisplaySystemEnums::WindowID p_window) const {
	WARN_PRINT("Native handle not supported by this display server.");
	return 0;
}

void DisplaySystem::window_set_vsync_mode(DisplaySystemEnums::VSyncMode p_vsync_mode, DisplaySystemEnums::WindowID p_window) {
	WARN_PRINT("Changing the V-Sync mode is not supported by this display server.");
}

DisplaySystemEnums::VSyncMode DisplaySystem::window_get_vsync_mode(DisplaySystemEnums::WindowID p_window) const {
	WARN_PRINT("Changing the V-Sync mode is not supported by this display server.");
	return DisplaySystemEnums::VSyncMode::VSYNC_ENABLED;
}

bool DisplaySystem::window_is_hdr_output_supported(DisplaySystemEnums::WindowID p_window) const {
	return false;
}

void DisplaySystem::window_request_hdr_output(const bool p_enable, DisplaySystemEnums::WindowID p_window) {
	if (p_enable) {
		WARN_PRINT("HDR output requested, but it is not supported by this display server.");
	}
}

bool DisplaySystem::window_is_hdr_output_requested(DisplaySystemEnums::WindowID p_window) const {
	return false;
}

bool DisplaySystem::window_is_hdr_output_enabled(DisplaySystemEnums::WindowID p_window) const {
	return false;
}

void DisplaySystem::window_set_hdr_output_reference_luminance(const float p_reference_luminance, DisplaySystemEnums::WindowID p_window) {
	WARN_PRINT("Attempting to set reference luminance, but HDR output is not supported by this display server.");
}

float DisplaySystem::window_get_hdr_output_reference_luminance(DisplaySystemEnums::WindowID p_window) const {
	return -1.0f;
}

float DisplaySystem::window_get_hdr_output_current_reference_luminance(DisplaySystemEnums::WindowID p_window) const {
	return 0.0f;
}

void DisplaySystem::window_set_hdr_output_max_luminance(const float p_max_luminance, DisplaySystemEnums::WindowID p_window) {
	WARN_PRINT("Attempting to set max luminance, but HDR output is not supported by this display server.");
}

float DisplaySystem::window_get_hdr_output_max_luminance(DisplaySystemEnums::WindowID p_window) const {
	return -1.0f;
}

float DisplaySystem::window_get_hdr_output_current_max_luminance(DisplaySystemEnums::WindowID p_window) const {
	return 0.0f;
}

float DisplaySystem::window_get_output_max_linear_value(DisplaySystemEnums::WindowID p_window) const {
	return 1.0f;
}

DisplaySystemEnums::WindowID DisplaySystem::get_focused_window() const {
	return DisplaySystemEnums::MAIN_WINDOW_ID; // Proper value for single windows.
}

void DisplaySystem::set_context(DisplaySystemEnums::Context p_context) {
}

void DisplaySystem::register_additional_output(Object *p_object) {
	ObjectID id = p_object->get_instance_id();
	if (!additional_outputs.has(id)) {
		additional_outputs.push_back(id);
	}
}

void DisplaySystem::unregister_additional_output(Object *p_object) {
	additional_outputs.erase(p_object->get_instance_id());
}

void DisplaySystem::register_members() {
	ADD_SIGNAL(MethodInfo("orientation_changed", PropertyInfo(Variant::INT, "orientation", PROPERTY_HINT_NONE, String(), PROPERTY_USAGE_DEFAULT | PROPERTY_USAGE_CLASS_IS_ENUM, "DisplaySystem.SensorOrientation")));

	ClassRegistry::bind_method(D_METHOD("has_feature", "feature"), &DisplaySystem::has_feature);
	ClassRegistry::bind_method(D_METHOD("get_name"), &DisplaySystem::get_name);

	ClassRegistry::bind_method(D_METHOD("help_set_search_callbacks", "search_callback", "action_callback"), &DisplaySystem::help_set_search_callbacks);

	ClassRegistry::bind_method(D_METHOD("tts_is_speaking"), &DisplaySystem::tts_is_speaking);
	ClassRegistry::bind_method(D_METHOD("tts_is_paused"), &DisplaySystem::tts_is_paused);
	ClassRegistry::bind_method(D_METHOD("tts_get_voices"), &DisplaySystem::tts_get_voices);
	ClassRegistry::bind_method(D_METHOD("tts_get_voices_for_language", "language"), &DisplaySystem::tts_get_voices_for_language);

	ClassRegistry::bind_method(D_METHOD("tts_speak", "text", "voice", "volume", "pitch", "rate", "utterance_id", "interrupt"), &DisplaySystem::tts_speak, DEFVAL(50), DEFVAL(1.f), DEFVAL(1.f), DEFVAL(0), DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("tts_pause"), &DisplaySystem::tts_pause);
	ClassRegistry::bind_method(D_METHOD("tts_resume"), &DisplaySystem::tts_resume);
	ClassRegistry::bind_method(D_METHOD("tts_stop"), &DisplaySystem::tts_stop);

	ClassRegistry::bind_method(D_METHOD("tts_set_utterance_callback", "event", "callable"), &DisplaySystem::tts_set_utterance_callback);
	ClassRegistry::bind_method(D_METHOD("_tts_post_utterance_event", "event", "id", "char_pos"), &DisplaySystem::tts_post_utterance_event);

	ClassRegistry::bind_method(D_METHOD("is_dark_mode_supported"), &DisplaySystem::is_dark_mode_supported);
	ClassRegistry::bind_method(D_METHOD("is_dark_mode"), &DisplaySystem::is_dark_mode);
	ClassRegistry::bind_method(D_METHOD("get_accent_color"), &DisplaySystem::get_accent_color);
	ClassRegistry::bind_method(D_METHOD("get_base_color"), &DisplaySystem::get_base_color);
	ClassRegistry::bind_method(D_METHOD("set_system_theme_change_callback", "callable"), &DisplaySystem::set_system_theme_change_callback);

	ClassRegistry::bind_method(D_METHOD("mouse_set_mode", "mouse_mode"), &DisplaySystem::mouse_set_mode);
	ClassRegistry::bind_method(D_METHOD("mouse_get_mode"), &DisplaySystem::mouse_get_mode);

	ClassRegistry::bind_method(D_METHOD("warp_mouse", "position"), &DisplaySystem::warp_mouse);
	ClassRegistry::bind_method(D_METHOD("mouse_get_position"), &DisplaySystem::mouse_get_position);
	ClassRegistry::bind_method(D_METHOD("mouse_get_button_state"), &DisplaySystem::mouse_get_button_state);

	ClassRegistry::bind_method(D_METHOD("clipboard_set", "clipboard"), &DisplaySystem::clipboard_set);
	ClassRegistry::bind_method(D_METHOD("clipboard_get"), &DisplaySystem::clipboard_get);
	ClassRegistry::bind_method(D_METHOD("clipboard_get_image"), &DisplaySystem::clipboard_get_image);
	ClassRegistry::bind_method(D_METHOD("clipboard_has"), &DisplaySystem::clipboard_has);
	ClassRegistry::bind_method(D_METHOD("clipboard_has_image"), &DisplaySystem::clipboard_has_image);
	ClassRegistry::bind_method(D_METHOD("clipboard_set_primary", "clipboard_primary"), &DisplaySystem::clipboard_set_primary);
	ClassRegistry::bind_method(D_METHOD("clipboard_get_primary"), &DisplaySystem::clipboard_get_primary);

	ClassRegistry::bind_method(D_METHOD("get_display_cutouts", "screen"), &DisplaySystem::get_display_cutouts, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));
	ClassRegistry::bind_method(D_METHOD("get_display_safe_area", "screen"), &DisplaySystem::get_display_safe_area, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));

	ClassRegistry::bind_method(D_METHOD("get_screen_count"), &DisplaySystem::get_screen_count);
	ClassRegistry::bind_method(D_METHOD("get_primary_screen"), &DisplaySystem::get_primary_screen);
	ClassRegistry::bind_method(D_METHOD("get_keyboard_focus_screen"), &DisplaySystem::get_keyboard_focus_screen);
	ClassRegistry::bind_method(D_METHOD("get_screen_from_rect", "rect"), &DisplaySystem::get_screen_from_rect);
	ClassRegistry::bind_method(D_METHOD("screen_get_position", "screen"), &DisplaySystem::screen_get_position, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));
	ClassRegistry::bind_method(D_METHOD("screen_get_size", "screen"), &DisplaySystem::screen_get_size, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));
	ClassRegistry::bind_method(D_METHOD("screen_get_usable_rect", "screen"), &DisplaySystem::screen_get_usable_rect, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));
	ClassRegistry::bind_method(D_METHOD("screen_get_dpi", "screen"), &DisplaySystem::screen_get_dpi, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));
	ClassRegistry::bind_method(D_METHOD("screen_get_scale", "screen"), &DisplaySystem::screen_get_scale, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));
	ClassRegistry::bind_method(D_METHOD("is_touchscreen_available"), &DisplaySystem::is_touchscreen_available);
	ClassRegistry::bind_method(D_METHOD("screen_get_max_scale"), &DisplaySystem::screen_get_max_scale);
	ClassRegistry::bind_method(D_METHOD("screen_get_refresh_rate", "screen"), &DisplaySystem::screen_get_refresh_rate, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));
	ClassRegistry::bind_method(D_METHOD("screen_get_pixel", "position"), &DisplaySystem::screen_get_pixel);
	ClassRegistry::bind_method(D_METHOD("screen_get_image", "screen"), &DisplaySystem::screen_get_image, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));
	ClassRegistry::bind_method(D_METHOD("screen_get_image_rect", "rect"), &DisplaySystem::screen_get_image_rect);

	ClassRegistry::bind_method(D_METHOD("screen_set_orientation", "orientation", "screen"), &DisplaySystem::screen_set_orientation, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));
	ClassRegistry::bind_method(D_METHOD("screen_get_orientation", "screen"), &DisplaySystem::screen_get_orientation, DEFVAL(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW));

	ClassRegistry::bind_method(D_METHOD("screen_set_keep_on", "enable"), &DisplaySystem::screen_set_keep_on);
	ClassRegistry::bind_method(D_METHOD("screen_is_kept_on"), &DisplaySystem::screen_is_kept_on);

	ClassRegistry::bind_method(D_METHOD("get_window_list"), &DisplaySystem::get_window_list);
	ClassRegistry::bind_method(D_METHOD("get_window_at_screen_position", "position"), &DisplaySystem::get_window_at_screen_position);

	ClassRegistry::bind_method(D_METHOD("window_get_native_handle", "handle_type", "window_id"), &DisplaySystem::window_get_native_handle, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_active_popup"), &DisplaySystem::window_get_active_popup);
	ClassRegistry::bind_method(D_METHOD("window_set_popup_safe_rect", "window", "rect"), &DisplaySystem::window_set_popup_safe_rect);
	ClassRegistry::bind_method(D_METHOD("window_get_popup_safe_rect", "window"), &DisplaySystem::window_get_popup_safe_rect);

	ClassRegistry::bind_method(D_METHOD("window_set_title", "title", "window_id"), &DisplaySystem::window_set_title, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_title_size", "title", "window_id"), &DisplaySystem::window_get_title_size, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_mouse_passthrough", "region", "window_id"), &DisplaySystem::window_set_mouse_passthrough, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_get_current_screen", "window_id"), &DisplaySystem::window_get_current_screen, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_current_screen", "screen", "window_id"), &DisplaySystem::window_set_current_screen, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_get_position", "window_id"), &DisplaySystem::window_get_position, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_position_with_decorations", "window_id"), &DisplaySystem::window_get_position_with_decorations, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_position", "position", "window_id"), &DisplaySystem::window_set_position, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_get_size", "window_id"), &DisplaySystem::window_get_size, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_size", "size", "window_id"), &DisplaySystem::window_set_size, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_rect_changed_callback", "callback", "window_id"), &DisplaySystem::window_set_rect_changed_callback, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_window_event_callback", "callback", "window_id"), &DisplaySystem::window_set_window_event_callback, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_input_event_callback", "callback", "window_id"), &DisplaySystem::window_set_input_event_callback, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_input_text_callback", "callback", "window_id"), &DisplaySystem::window_set_input_text_callback, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_drop_files_callback", "callback", "window_id"), &DisplaySystem::window_set_drop_files_callback, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_get_attached_instance_id", "window_id"), &DisplaySystem::window_get_attached_instance_id, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_get_max_size", "window_id"), &DisplaySystem::window_get_max_size, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_max_size", "max_size", "window_id"), &DisplaySystem::window_set_max_size, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_get_min_size", "window_id"), &DisplaySystem::window_get_min_size, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_min_size", "min_size", "window_id"), &DisplaySystem::window_set_min_size, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_get_size_with_decorations", "window_id"), &DisplaySystem::window_get_size_with_decorations, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_get_mode", "window_id"), &DisplaySystem::window_get_mode, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_mode", "mode", "window_id"), &DisplaySystem::window_set_mode, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_set_flag", "flag", "enabled", "window_id"), &DisplaySystem::window_set_flag, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_flag", "flag", "window_id"), &DisplaySystem::window_get_flag, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_set_icon", "icon", "window_id"), &DisplaySystem::window_set_icon, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_set_window_buttons_offset", "offset", "window_id"), &DisplaySystem::window_set_window_buttons_offset, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_safe_title_margins", "window_id"), &DisplaySystem::window_get_safe_title_margins, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_request_attention", "window_id"), &DisplaySystem::window_request_attention, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_taskbar_progress_value", "value", "window_id"), &DisplaySystem::window_set_taskbar_progress_value, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_taskbar_progress_state", "state", "window_id"), &DisplaySystem::window_set_taskbar_progress_state, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_move_to_foreground", "window_id"), &DisplaySystem::window_move_to_foreground, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_is_focused", "window_id"), &DisplaySystem::window_is_focused, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_can_draw", "window_id"), &DisplaySystem::window_can_draw, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_set_transient", "window_id", "parent_window_id"), &DisplaySystem::window_set_transient);
	ClassRegistry::bind_method(D_METHOD("window_set_exclusive", "window_id", "exclusive"), &DisplaySystem::window_set_exclusive);

	ClassRegistry::bind_method(D_METHOD("window_set_ime_active", "active", "window_id"), &DisplaySystem::window_set_ime_active, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_ime_position", "position", "window_id"), &DisplaySystem::window_set_ime_position, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_set_ime_rect", "rect", "window_id"), &DisplaySystem::window_set_ime_rect, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_set_vsync_mode", "vsync_mode", "window_id"), &DisplaySystem::window_set_vsync_mode, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_vsync_mode", "window_id"), &DisplaySystem::window_get_vsync_mode, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_is_hdr_output_supported", "window_id"), &DisplaySystem::window_is_hdr_output_supported, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_request_hdr_output", "enable", "window_id"), &DisplaySystem::window_request_hdr_output, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_is_hdr_output_requested", "window_id"), &DisplaySystem::window_is_hdr_output_requested, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_is_hdr_output_enabled", "window_id"), &DisplaySystem::window_is_hdr_output_enabled, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_set_hdr_output_reference_luminance", "reference_luminance", "window_id"), &DisplaySystem::window_set_hdr_output_reference_luminance, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_hdr_output_reference_luminance", "window_id"), &DisplaySystem::window_get_hdr_output_reference_luminance, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_hdr_output_current_reference_luminance", "window_id"), &DisplaySystem::window_get_hdr_output_current_reference_luminance, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_set_hdr_output_max_luminance", "max_luminance", "window_id"), &DisplaySystem::window_set_hdr_output_max_luminance, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_hdr_output_max_luminance", "window_id"), &DisplaySystem::window_get_hdr_output_max_luminance, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_get_hdr_output_current_max_luminance", "window_id"), &DisplaySystem::window_get_hdr_output_current_max_luminance, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_get_output_max_linear_value", "window_id"), &DisplaySystem::window_get_output_max_linear_value, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_is_maximize_allowed", "window_id"), &DisplaySystem::window_is_maximize_allowed, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_maximize_on_title_dbl_click"), &DisplaySystem::window_maximize_on_title_dbl_click);
	ClassRegistry::bind_method(D_METHOD("window_minimize_on_title_dbl_click"), &DisplaySystem::window_minimize_on_title_dbl_click);

	ClassRegistry::bind_method(D_METHOD("window_start_drag", "window_id"), &DisplaySystem::window_start_drag, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("window_start_resize", "edge", "window_id"), &DisplaySystem::window_start_resize, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("window_set_color", "color"), &DisplaySystem::window_set_color);

	ClassRegistry::bind_method(D_METHOD("accessibility_should_increase_contrast"), &DisplaySystem::accessibility_should_increase_contrast);
	ClassRegistry::bind_method(D_METHOD("accessibility_should_reduce_animation"), &DisplaySystem::accessibility_should_reduce_animation);
	ClassRegistry::bind_method(D_METHOD("accessibility_should_reduce_transparency"), &DisplaySystem::accessibility_should_reduce_transparency);
	ClassRegistry::bind_method(D_METHOD("accessibility_screen_reader_active"), &DisplaySystem::accessibility_screen_reader_active);

	ClassRegistry::bind_method(D_METHOD("ime_get_selection"), &DisplaySystem::ime_get_selection);
	ClassRegistry::bind_method(D_METHOD("ime_get_text"), &DisplaySystem::ime_get_text);

	ClassRegistry::bind_method(D_METHOD("virtual_keyboard_show", "existing_text", "position", "type", "max_length", "cursor_start", "cursor_end"), &DisplaySystem::virtual_keyboard_show, DEFVAL(Rect2()), DEFVAL(DisplaySystemEnums::KEYBOARD_TYPE_DEFAULT), DEFVAL(-1), DEFVAL(-1), DEFVAL(-1));
	ClassRegistry::bind_method(D_METHOD("virtual_keyboard_hide"), &DisplaySystem::virtual_keyboard_hide);

	ClassRegistry::bind_method(D_METHOD("virtual_keyboard_get_height"), &DisplaySystem::virtual_keyboard_get_height);

	ClassRegistry::bind_method(D_METHOD("has_hardware_keyboard"), &DisplaySystem::has_hardware_keyboard);
	ClassRegistry::bind_method(D_METHOD("set_hardware_keyboard_connection_change_callback", "callable"), &DisplaySystem::set_hardware_keyboard_connection_change_callback);

	ClassRegistry::bind_method(D_METHOD("cursor_set_shape", "shape"), &DisplaySystem::cursor_set_shape);
	ClassRegistry::bind_method(D_METHOD("cursor_get_shape"), &DisplaySystem::cursor_get_shape);
	ClassRegistry::bind_method(D_METHOD("cursor_set_custom_image", "cursor", "shape", "hotspot"), &DisplaySystem::cursor_set_custom_image, DEFVAL(DisplaySystemEnums::CURSOR_ARROW), DEFVAL(Vector2()));

	ClassRegistry::bind_method(D_METHOD("send_toast_notification", "title", "text", "image", "callback"), &DisplaySystem::send_toast_notification);
	ClassRegistry::bind_method(D_METHOD("hide_toast_notification", "id"), &DisplaySystem::hide_toast_notification);

	ClassRegistry::bind_method(D_METHOD("get_swap_cancel_ok"), &DisplaySystem::get_swap_cancel_ok);

	ClassRegistry::bind_method(D_METHOD("enable_for_stealing_focus", "process_id"), &DisplaySystem::enable_for_stealing_focus);

	ClassRegistry::bind_method(D_METHOD("dialog_show", "title", "description", "buttons", "callback"), &DisplaySystem::dialog_show);
	ClassRegistry::bind_method(D_METHOD("dialog_input_text", "title", "description", "existing_text", "callback"), &DisplaySystem::dialog_input_text);

	ClassRegistry::bind_method(D_METHOD("file_dialog_show", "title", "current_directory", "filename", "show_hidden", "mode", "filters", "callback", "parent_window_id"), &DisplaySystem::file_dialog_show, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("file_dialog_with_options_show", "title", "current_directory", "root", "filename", "show_hidden", "mode", "filters", "options", "callback", "parent_window_id"), &DisplaySystem::file_dialog_with_options_show, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	ClassRegistry::bind_method(D_METHOD("beep"), &DisplaySystem::beep);

	ClassRegistry::bind_method(D_METHOD("keyboard_get_layout_count"), &DisplaySystem::keyboard_get_layout_count);
	ClassRegistry::bind_method(D_METHOD("keyboard_get_current_layout"), &DisplaySystem::keyboard_get_current_layout);
	ClassRegistry::bind_method(D_METHOD("keyboard_set_current_layout", "index"), &DisplaySystem::keyboard_set_current_layout);
	ClassRegistry::bind_method(D_METHOD("keyboard_get_layout_language", "index"), &DisplaySystem::keyboard_get_layout_language);
	ClassRegistry::bind_method(D_METHOD("keyboard_get_layout_name", "index"), &DisplaySystem::keyboard_get_layout_name);
	ClassRegistry::bind_method(D_METHOD("keyboard_get_keycode_from_physical", "keycode"), &DisplaySystem::keyboard_get_keycode_from_physical);
	ClassRegistry::bind_method(D_METHOD("keyboard_get_label_from_physical", "keycode"), &DisplaySystem::keyboard_get_label_from_physical);

	ClassRegistry::bind_method(D_METHOD("show_emoji_and_symbol_picker"), &DisplaySystem::show_emoji_and_symbol_picker);
	ClassRegistry::bind_method(D_METHOD("color_picker", "callback"), &DisplaySystem::color_picker);

	ClassRegistry::bind_method(D_METHOD("process_events"), &DisplaySystem::process_events);
	ClassRegistry::bind_method(D_METHOD("force_process_and_drop_events"), &DisplaySystem::force_process_and_drop_events);

	ClassRegistry::bind_method(D_METHOD("set_native_icon", "filename"), &DisplaySystem::set_native_icon);
	ClassRegistry::bind_method(D_METHOD("set_icon", "image"), &DisplaySystem::set_icon);

	ClassRegistry::bind_method(D_METHOD("create_status_indicator", "icon", "tooltip", "callback"), &DisplaySystem::create_status_indicator);
	ClassRegistry::bind_method(D_METHOD("status_indicator_set_icon", "id", "icon"), &DisplaySystem::status_indicator_set_icon);
	ClassRegistry::bind_method(D_METHOD("status_indicator_set_tooltip", "id", "tooltip"), &DisplaySystem::status_indicator_set_tooltip);
	ClassRegistry::bind_method(D_METHOD("status_indicator_set_menu", "id", "menu_rid"), &DisplaySystem::status_indicator_set_menu);
	ClassRegistry::bind_method(D_METHOD("status_indicator_set_callback", "id", "callback"), &DisplaySystem::status_indicator_set_callback);
	ClassRegistry::bind_method(D_METHOD("status_indicator_get_rect", "id"), &DisplaySystem::status_indicator_get_rect);
	ClassRegistry::bind_method(D_METHOD("delete_status_indicator", "id"), &DisplaySystem::delete_status_indicator);

	ClassRegistry::bind_method(D_METHOD("tablet_get_driver_count"), &DisplaySystem::tablet_get_driver_count);
	ClassRegistry::bind_method(D_METHOD("tablet_get_driver_name", "idx"), &DisplaySystem::tablet_get_driver_name);
	ClassRegistry::bind_method(D_METHOD("tablet_get_current_driver"), &DisplaySystem::tablet_get_current_driver);
	ClassRegistry::bind_method(D_METHOD("tablet_set_current_driver", "name"), &DisplaySystem::tablet_set_current_driver);

	ClassRegistry::bind_method(D_METHOD("is_window_transparency_available"), &DisplaySystem::is_window_transparency_available);

	ClassRegistry::bind_method(D_METHOD("register_additional_output", "object"), &DisplaySystem::register_additional_output);
	ClassRegistry::bind_method(D_METHOD("unregister_additional_output", "object"), &DisplaySystem::unregister_additional_output);
	ClassRegistry::bind_method(D_METHOD("has_additional_outputs"), &DisplaySystem::has_additional_outputs);

	ClassRegistry::bind_method(D_METHOD("is_in_pip_mode", "window_id"), &DisplaySystem::is_in_pip_mode, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("pip_mode_enter", "window_id"), &DisplaySystem::pip_mode_enter, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("pip_mode_set_aspect_ratio", "numerator", "denominator", "window_id"), &DisplaySystem::pip_mode_set_aspect_ratio, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));
	ClassRegistry::bind_method(D_METHOD("pip_mode_set_auto_enter_on_background", "auto_enter_on_background", "window_id"), &DisplaySystem::pip_mode_set_auto_enter_on_background, DEFVAL(DisplaySystemEnums::MAIN_WINDOW_ID));

	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_SUBWINDOWS);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_TOUCHSCREEN);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_MOUSE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_MOUSE_WARP);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_CLIPBOARD);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_VIRTUAL_KEYBOARD);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_CURSOR_SHAPE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_CUSTOM_CURSOR_SHAPE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_NATIVE_DIALOG);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_IME);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_WINDOW_TRANSPARENCY);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_HIDPI);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_ICON);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_NATIVE_ICON);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_ORIENTATION);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_SWAP_BUFFERS);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_CLIPBOARD_PRIMARY);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_TEXT_TO_SPEECH);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_EXTEND_TO_TITLE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_SCREEN_CAPTURE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_STATUS_INDICATOR);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_NATIVE_HELP);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_NATIVE_DIALOG_INPUT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_NATIVE_DIALOG_FILE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_NATIVE_DIALOG_FILE_EXTRA);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_WINDOW_DRAG);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_SCREEN_EXCLUDE_FROM_CAPTURE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_WINDOW_EMBEDDING);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_NATIVE_DIALOG_FILE_MIME);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_EMOJI_AND_SYMBOL_PICKER);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_NATIVE_COLOR_PICKER);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_SELF_FITTING_WINDOWS);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_ACCESSIBILITY_SCREEN_READER);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_HDR_OUTPUT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_PIP_MODE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FEATURE_EMBEDDED);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::MOUSE_MODE_VISIBLE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::MOUSE_MODE_HIDDEN);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::MOUSE_MODE_CAPTURED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::MOUSE_MODE_CONFINED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::MOUSE_MODE_CONFINED_HIDDEN);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::MOUSE_MODE_MAX);

	BIND_CONSTANT(DisplaySystemEnums::INVALID_SCREEN);
	BIND_CONSTANT(DisplaySystemEnums::SCREEN_WITH_MOUSE_FOCUS);
	BIND_CONSTANT(DisplaySystemEnums::SCREEN_WITH_KEYBOARD_FOCUS);
	BIND_CONSTANT(DisplaySystemEnums::SCREEN_PRIMARY);
	BIND_CONSTANT(DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW);

	BIND_CONSTANT(DisplaySystemEnums::MAIN_WINDOW_ID);
	BIND_CONSTANT(DisplaySystemEnums::INVALID_WINDOW_ID);
	BIND_CONSTANT(DisplaySystemEnums::INVALID_INDICATOR_ID);
	BIND_CONSTANT(DisplaySystemEnums::INVALID_NOTIFICATION_ID);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::SCREEN_LANDSCAPE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::SCREEN_PORTRAIT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::SCREEN_REVERSE_LANDSCAPE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::SCREEN_REVERSE_PORTRAIT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::SCREEN_SENSOR_LANDSCAPE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::SCREEN_SENSOR_PORTRAIT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::SCREEN_SENSOR);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::SENSOR_ORIENTATION_UNDEFINED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::SENSOR_ORIENTATION_PORTRAIT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::SENSOR_ORIENTATION_LANDSCAPE);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::KEYBOARD_TYPE_DEFAULT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::KEYBOARD_TYPE_MULTILINE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::KEYBOARD_TYPE_NUMBER);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::KEYBOARD_TYPE_NUMBER_DECIMAL);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::KEYBOARD_TYPE_PHONE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::KEYBOARD_TYPE_EMAIL_ADDRESS);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::KEYBOARD_TYPE_PASSWORD);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::KEYBOARD_TYPE_URL);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_ARROW);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_IBEAM);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_POINTING_HAND);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_CROSS);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_WAIT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_BUSY);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_DRAG);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_CAN_DROP);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_FORBIDDEN);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_VSIZE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_HSIZE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_BDIAGSIZE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_FDIAGSIZE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_MOVE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_VSPLIT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_HSPLIT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_HELP);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::CURSOR_MAX);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::FILE_DIALOG_MODE_OPEN_FILE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FILE_DIALOG_MODE_OPEN_FILES);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FILE_DIALOG_MODE_OPEN_DIR);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FILE_DIALOG_MODE_OPEN_ANY);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::FILE_DIALOG_MODE_SAVE_FILE);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_MODE_WINDOWED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_MODE_MINIMIZED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_MODE_MAXIMIZED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_MODE_FULLSCREEN);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_MODE_EXCLUSIVE_FULLSCREEN);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::PROGRESS_STATE_NOPROGRESS);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::PROGRESS_STATE_INDETERMINATE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::PROGRESS_STATE_NORMAL);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::PROGRESS_STATE_ERROR);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::PROGRESS_STATE_PAUSED);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_RESIZE_DISABLED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_BORDERLESS);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_ALWAYS_ON_TOP);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_TRANSPARENT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_NO_FOCUS);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_POPUP);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_EXTEND_TO_TITLE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_MOUSE_PASSTHROUGH);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_SHARP_CORNERS);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_EXCLUDE_FROM_CAPTURE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_POPUP_WM_HINT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_MINIMIZE_DISABLED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_MAXIMIZE_DISABLED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_FLAG_MAX);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_MOUSE_ENTER);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_MOUSE_EXIT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_FOCUS_IN);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_FOCUS_OUT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_CLOSE_REQUEST);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_GO_BACK_REQUEST);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_DPI_CHANGE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_TITLEBAR_CHANGE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_FORCE_CLOSE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EVENT_OUTPUT_MAX_LINEAR_VALUE_CHANGED);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EDGE_TOP_LEFT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EDGE_TOP);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EDGE_TOP_RIGHT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EDGE_LEFT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EDGE_RIGHT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EDGE_BOTTOM_LEFT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EDGE_BOTTOM);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EDGE_BOTTOM_RIGHT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_EDGE_MAX);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::NOTIFICATION_ACTIVATED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::NOTIFICATION_DISMISSED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::NOTIFICATION_FAILED);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::VSYNC_DISABLED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::VSYNC_ENABLED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::VSYNC_ADAPTIVE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::VSYNC_MAILBOX);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::DISPLAY_HANDLE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_HANDLE);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::WINDOW_VIEW);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::OPENGL_CONTEXT);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::EGL_DISPLAY);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::EGL_CONFIG);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::GLX_VISUALID);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::GLX_FBCONFIG);

	BIND_ENUM_CONSTANT(DisplaySystemEnums::TTS_UTTERANCE_STARTED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::TTS_UTTERANCE_ENDED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::TTS_UTTERANCE_CANCELED);
	BIND_ENUM_CONSTANT(DisplaySystemEnums::TTS_UTTERANCE_BOUNDARY);
}

Ref<Image> DisplaySystem::_get_cursor_image_from_resource(const Ref<Resource> &p_cursor, const Vector2 &p_hotspot) {
	Ref<Image> image;
	ERR_FAIL_COND_V_MSG(p_hotspot.x < 0 || p_hotspot.y < 0, image, "Hotspot outside cursor image.");

	Ref<Texture2D> texture = p_cursor;
	if (texture.is_valid()) {
		image = texture->get_image();
	} else {
		image = p_cursor;
	}
	ERR_FAIL_COND_V(image.is_null(), image);

	Size2 image_size = image->get_size();
	ERR_FAIL_COND_V_MSG(p_hotspot.x > image_size.width || p_hotspot.y > image_size.height, image, "Hotspot outside cursor image.");
	ERR_FAIL_COND_V_MSG(image_size.width > 256 || image_size.height > 256, image, "Cursor image too big. Max supported size is 256x256.");

	if (image->is_compressed()) {
		image = image->duplicate(true);
		Error err = image->decompress();
		ERR_FAIL_COND_V_MSG(err != OK, Ref<Image>(), "Couldn't decompress VRAM-compressed custom mouse cursor image. Switch to a lossless compression mode in the Import dock.");
	}
	return image;
}

void DisplaySystem::register_create_function(const char *p_name, CreateFunction p_function, GetRenderingDriversFunction p_get_drivers) {
	ERR_FAIL_COND(system_create_count == MAX_SYSTEMS);
	// Headless display server is always last
	system_create_functions[system_create_count] = system_create_functions[system_create_count - 1];
	system_create_functions[system_create_count - 1].name = p_name;
	system_create_functions[system_create_count - 1].create_function = p_function;
	system_create_functions[system_create_count - 1].get_rendering_drivers_function = p_get_drivers;
	system_create_count++;
}

int DisplaySystem::get_create_function_count() {
	return system_create_count;
}

const char *DisplaySystem::get_create_function_name(int p_index) {
	ERR_FAIL_INDEX_V(p_index, system_create_count, nullptr);
	return system_create_functions[p_index].name;
}

Vector<String> DisplaySystem::get_create_function_rendering_drivers(int p_index) {
	ERR_FAIL_INDEX_V(p_index, system_create_count, Vector<String>());
	return system_create_functions[p_index].get_rendering_drivers_function();
}

DisplaySystem *DisplaySystem::create(int p_index, const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error) {
	ERR_FAIL_INDEX_V(p_index, system_create_count, nullptr);
	return system_create_functions[p_index].create_function(p_rendering_driver, p_mode, p_vsync_mode, p_flags, p_position, p_resolution, p_screen, p_context, p_parent_window, r_error);
}

void DisplaySystem::_input_set_mouse_mode(InputClassEnums::MouseMode p_mode) {
	singleton->mouse_set_mode(DisplaySystemEnums::MouseMode(p_mode));
}

InputClassEnums::MouseMode DisplaySystem::_input_get_mouse_mode() {
	return InputClassEnums::MouseMode(singleton->mouse_get_mode());
}

void DisplaySystem::_input_set_mouse_mode_override(InputClassEnums::MouseMode p_mode) {
	singleton->mouse_set_mode_override(DisplaySystemEnums::MouseMode(p_mode));
}

InputClassEnums::MouseMode DisplaySystem::_input_get_mouse_mode_override() {
	return InputClassEnums::MouseMode(singleton->mouse_get_mode_override());
}

void DisplaySystem::_input_set_mouse_mode_override_enabled(bool p_enabled) {
	singleton->mouse_set_mode_override_enabled(p_enabled);
}

bool DisplaySystem::_input_is_mouse_mode_override_enabled() {
	return singleton->mouse_is_mode_override_enabled();
}

void DisplaySystem::_input_warp(const Vector2 &p_to_pos) {
	singleton->warp_mouse(p_to_pos);
}

InputClassEnums::CursorShape DisplaySystem::_input_get_current_cursor_shape() {
	return (InputClassEnums::CursorShape)singleton->cursor_get_shape();
}

void DisplaySystem::_input_set_custom_mouse_cursor_func(const Ref<Resource> &p_image, InputClassEnums::CursorShape p_shape, const Vector2 &p_hotspot) {
	singleton->cursor_set_custom_image(p_image, (DisplaySystemEnums::CursorShape)p_shape, p_hotspot);
}

bool DisplaySystem::is_rendering_device_supported() {
#if defined(RD_ENABLED)
	RenderingDevice *device = RenderingDevice::get_singleton();
	if (device) {
		return true;
	}

	if (supported_rendering_device == DisplaySystemEnums::RenderingDeviceCreationStatus::SUCCESS) {
		return true;
	} else if (supported_rendering_device == DisplaySystemEnums::RenderingDeviceCreationStatus::FAILURE) {
		return false;
	}

	Error err;

#if defined(WINDOWS_ENABLED) || defined(LINUXBSD_ENABLED)
	// On some drivers combining OpenGL and RenderingDevice can result in crash, offload the check to the subprocess.
	List<String> arguments;
	arguments.push_back("--test-rd-support");
	if (get_singleton()) {
		arguments.push_back("--display-driver");
		arguments.push_back(get_singleton()->get_name().to_lower());
	}

	String pipe;
	int exitcode = 0;
	err = OS::get_singleton()->execute(OS::get_singleton()->get_executable_path(), arguments, &pipe, &exitcode);
	if (err == OK && exitcode == 0) {
		supported_rendering_device = DisplaySystemEnums::RenderingDeviceCreationStatus::SUCCESS;
		return true;
	} else {
		supported_rendering_device = DisplaySystemEnums::RenderingDeviceCreationStatus::FAILURE;
	}
#else // WINDOWS_ENABLED

	RenderingContextDriver *rcd = nullptr;

#if defined(VULKAN_ENABLED)
	rcd = memnew(RenderingContextDriverVulkan);
#endif
#ifdef D3D12_ENABLED
	if (rcd == nullptr) {
		rcd = memnew(RenderingContextDriverD3D12);
	}
#endif
#ifdef METAL_ENABLED
	if (rcd == nullptr) {
		ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wunguarded-availability")
		// Eliminate "RenderingContextDriverMetal is only available on iOS 14.0 or newer".
		rcd = memnew(RenderingContextDriverMetal);
		ORBLIT_CLANG_WARNING_POP
	}
#endif

	if (rcd != nullptr) {
		err = rcd->initialize();
		if (err == OK) {
			RenderingDevice *rd = memnew(RenderingDevice);
			err = rd->initialize(rcd);
			memdelete(rd);
			rd = nullptr;
			if (err == OK) {
				// Creating a RenderingDevice is quite slow.
				// Cache the result for future usage, so that it's much faster on subsequent calls.
				supported_rendering_device = DisplaySystemEnums::RenderingDeviceCreationStatus::SUCCESS;
				memdelete(rcd);
				rcd = nullptr;
				return true;
			} else {
				supported_rendering_device = DisplaySystemEnums::RenderingDeviceCreationStatus::FAILURE;
			}
		}

		memdelete(rcd);
		rcd = nullptr;
	}

#endif // WINDOWS_ENABLED
#endif // RD_ENABLED
	return false;
}

bool DisplaySystem::can_create_rendering_device() {
	if (get_singleton() && get_singleton()->get_name() == "headless") {
		return false;
	}

#if defined(RD_ENABLED)
	RenderingDevice *device = RenderingDevice::get_singleton();
	if (device) {
		return true;
	}

	if (created_rendering_device == DisplaySystemEnums::RenderingDeviceCreationStatus::SUCCESS) {
		return true;
	} else if (created_rendering_device == DisplaySystemEnums::RenderingDeviceCreationStatus::FAILURE) {
		return false;
	}

	Error err;

#ifdef WINDOWS_ENABLED
	// On some NVIDIA drivers combining OpenGL and RenderingDevice can result in crash, offload the check to the subprocess.
	List<String> arguments;
	arguments.push_back("--test-rd-creation");

	String pipe;
	int exitcode = 0;
	err = OS::get_singleton()->execute(OS::get_singleton()->get_executable_path(), arguments, &pipe, &exitcode);
	if (err == OK && exitcode == 0) {
		created_rendering_device = DisplaySystemEnums::RenderingDeviceCreationStatus::SUCCESS;
		return true;
	} else {
		created_rendering_device = DisplaySystemEnums::RenderingDeviceCreationStatus::FAILURE;
	}
#else // WINDOWS_ENABLED

	RenderingContextDriver *rcd = nullptr;

#if defined(VULKAN_ENABLED)
	rcd = memnew(RenderingContextDriverVulkan);
#endif
#ifdef D3D12_ENABLED
	if (rcd == nullptr) {
		rcd = memnew(RenderingContextDriverD3D12);
	}
#endif
#ifdef METAL_ENABLED
	if (rcd == nullptr) {
		ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wunguarded-availability")
		// Eliminate "RenderingContextDriverMetal is only available on iOS 14.0 or newer".
		rcd = memnew(RenderingContextDriverMetal);
		ORBLIT_CLANG_WARNING_POP
	}
#endif

	if (rcd != nullptr) {
		err = rcd->initialize();
		if (err == OK) {
			RenderingDevice *rd = memnew(RenderingDevice);
			err = rd->initialize(rcd);
			memdelete(rd);
			rd = nullptr;
			if (err == OK) {
				// Creating a RenderingDevice is quite slow.
				// Cache the result for future usage, so that it's much faster on subsequent calls.
				created_rendering_device = DisplaySystemEnums::RenderingDeviceCreationStatus::SUCCESS;
				memdelete(rcd);
				rcd = nullptr;
				return true;
			} else {
				created_rendering_device = DisplaySystemEnums::RenderingDeviceCreationStatus::FAILURE;
			}
		}

		memdelete(rcd);
		rcd = nullptr;
	}

#endif // WINDOWS_ENABLED
#endif // RD_ENABLED
	return false;
}

DisplaySystem::DisplaySystem() {
	singleton = this;
	Input::set_mouse_mode_func = _input_set_mouse_mode;
	Input::get_mouse_mode_func = _input_get_mouse_mode;
	Input::set_mouse_mode_override_func = _input_set_mouse_mode_override;
	Input::get_mouse_mode_override_func = _input_get_mouse_mode_override;
	Input::set_mouse_mode_override_enabled_func = _input_set_mouse_mode_override_enabled;
	Input::is_mouse_mode_override_enabled_func = _input_is_mouse_mode_override_enabled;
	Input::warp_mouse_func = _input_warp;
	Input::get_current_cursor_shape_func = _input_get_current_cursor_shape;
	Input::set_custom_mouse_cursor_func = _input_set_custom_mouse_cursor_func;
}

DisplaySystem::~DisplaySystem() {
	singleton = nullptr;
}
