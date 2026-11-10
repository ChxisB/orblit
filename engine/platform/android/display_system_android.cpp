/**************************************************************************/
/*  display_system_android.cpp                                            */

#include "display_system_android.h"

#include "java_io_wrapper.h"
#include "java_wrapper.h"
#include "os_android.h"
#include "tts_android.h"

#include "core/config/project_settings.h"
#include "core/input/input.h"
#include "core/input/input_event.h"
#include "core/os/os.h"
#include "systems/display/accessibility_system.h"
#include "systems/display/native_menu.h"

#if defined(RD_ENABLED)
#include "systems/rendering/renderer_rd/renderer_compositor_rd.h"
#include "systems/rendering/rendering_device.h"

#if defined(VULKAN_ENABLED)
#include "rendering_context_driver_vulkan_android.h"
#endif
#endif

#ifdef GLES3_ENABLED
#include "backends/opengl/rasterizer_gles3.h"

#include <EGL/egl.h>
#endif

#if defined(RD_ENABLED)
static RenderingContextDriver *rendering_context_global = nullptr;
static bool rendering_context_global_checked = false;
#endif

DisplaySystemAndroid *DisplaySystemAndroid::get_singleton() {
	return static_cast<DisplaySystemAndroid *>(DisplaySystem::get_singleton());
}

bool DisplaySystemAndroid::has_feature(DisplaySystemEnums::Feature p_feature) const {
	switch (p_feature) {
		case DisplaySystemEnums::FEATURE_PIP_MODE: {
			OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
			ERR_FAIL_NULL_V(orblit_java, false);
			return orblit_java->is_pip_mode_supported();
		} break;

		case DisplaySystemEnums::FEATURE_CURSOR_SHAPE:
		//case DisplaySystemEnums::FEATURE_CUSTOM_CURSOR_SHAPE:
		//case DisplaySystemEnums::FEATURE_HIDPI:
		//case DisplaySystemEnums::FEATURE_ICON:
		//case DisplaySystemEnums::FEATURE_IME:
		case DisplaySystemEnums::FEATURE_MOUSE:
		//case DisplaySystemEnums::FEATURE_MOUSE_WARP:
		case DisplaySystemEnums::FEATURE_NATIVE_DIALOG:
		case DisplaySystemEnums::FEATURE_NATIVE_DIALOG_INPUT:
		case DisplaySystemEnums::FEATURE_NATIVE_DIALOG_FILE:
		//case DisplaySystemEnums::FEATURE_NATIVE_DIALOG_FILE_EXTRA:
		case DisplaySystemEnums::FEATURE_NATIVE_DIALOG_FILE_MIME:
		//case DisplaySystemEnums::FEATURE_NATIVE_ICON:
		case DisplaySystemEnums::FEATURE_WINDOW_TRANSPARENCY:
		case DisplaySystemEnums::FEATURE_CLIPBOARD:
		case DisplaySystemEnums::FEATURE_KEEP_SCREEN_ON:
		case DisplaySystemEnums::FEATURE_ORIENTATION:
		case DisplaySystemEnums::FEATURE_TOUCHSCREEN:
		case DisplaySystemEnums::FEATURE_VIRTUAL_KEYBOARD:
		case DisplaySystemEnums::FEATURE_TEXT_TO_SPEECH:
			return true;
		case DisplaySystemEnums::FEATURE_ACCESSIBILITY_SCREEN_READER:
			return AccessibilitySystem::get_singleton()->is_supported();
		default:
			return false;
	}
}

String DisplaySystemAndroid::get_name() const {
	return "Android";
}

int DisplaySystemAndroid::accessibility_should_increase_contrast() const {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, -1);
	return orblit_java->is_high_contrast_active();
}

int DisplaySystemAndroid::accessibility_screen_reader_active() const {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, -1);
	return orblit_java->is_screen_reader_active();
}

int DisplaySystemAndroid::accessibility_should_reduce_animation() const {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, -1);
	return orblit_java->is_animation_disabled();
}

bool DisplaySystemAndroid::tts_is_speaking() const {
	return TTS_Android::is_speaking();
}

bool DisplaySystemAndroid::tts_is_paused() const {
	return TTS_Android::is_paused();
}

TypedArray<Dictionary> DisplaySystemAndroid::tts_get_voices() const {
	return TTS_Android::get_voices();
}

void DisplaySystemAndroid::tts_speak(const String &p_text, const String &p_voice, int p_volume, float p_pitch, float p_rate, int64_t p_utterance_id, bool p_interrupt) {
	TTS_Android::speak(p_text, p_voice, p_volume, p_pitch, p_rate, p_utterance_id, p_interrupt);
}

void DisplaySystemAndroid::tts_pause() {
	TTS_Android::pause();
}

void DisplaySystemAndroid::tts_resume() {
	TTS_Android::resume();
}

void DisplaySystemAndroid::tts_stop() {
	TTS_Android::stop();
}

bool DisplaySystemAndroid::is_dark_mode_supported() const {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, false);

	return orblit_java->is_dark_mode_supported();
}

bool DisplaySystemAndroid::is_dark_mode() const {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, false);

	return orblit_java->is_dark_mode();
}

void DisplaySystemAndroid::set_system_theme_change_callback(const Callable &p_callable) {
	system_theme_changed = p_callable;
}

void DisplaySystemAndroid::emit_system_theme_changed() {
	if (system_theme_changed.is_valid()) {
		system_theme_changed.call_deferred();
	}
}

void DisplaySystemAndroid::set_hardware_keyboard_connection_change_callback(const Callable &p_callable) {
	hardware_keyboard_connection_changed = p_callable;
}

void DisplaySystemAndroid::emit_hardware_keyboard_connection_changed(bool p_connected) {
	if (hardware_keyboard_connection_changed.is_valid()) {
		hardware_keyboard_connection_changed.call_deferred(p_connected);
	}
}

void DisplaySystemAndroid::clipboard_set(const String &p_text) {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL(orblit_java);

	if (orblit_java->has_set_clipboard()) {
		orblit_java->set_clipboard(p_text);
	} else {
		DisplaySystem::clipboard_set(p_text);
	}
}

String DisplaySystemAndroid::clipboard_get() const {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, String());

	if (orblit_java->has_get_clipboard()) {
		return orblit_java->get_clipboard();
	} else {
		return DisplaySystem::clipboard_get();
	}
}

bool DisplaySystemAndroid::clipboard_has() const {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, false);

	if (orblit_java->has_has_clipboard()) {
		return orblit_java->has_clipboard();
	} else {
		return DisplaySystem::clipboard_has();
	}
}

Error DisplaySystemAndroid::dialog_show(String p_title, String p_description, Vector<String> p_buttons, const Callable &p_callback) {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, FAILED);
	dialog_callback = p_callback;
	return orblit_java->show_dialog(p_title, p_description, p_buttons);
}

void DisplaySystemAndroid::emit_dialog_callback(int p_button_index) {
	if (dialog_callback.is_valid()) {
		dialog_callback.call_deferred(p_button_index);
	}
}

Error DisplaySystemAndroid::dialog_input_text(String p_title, String p_description, String p_partial, const Callable &p_callback) {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, FAILED);
	input_dialog_callback = p_callback;
	return orblit_java->show_input_dialog(p_title, p_description, p_partial);
}

void DisplaySystemAndroid::emit_input_dialog_callback(String p_text) {
	if (input_dialog_callback.is_valid()) {
		input_dialog_callback.call_deferred(p_text);
	}
}

Error DisplaySystemAndroid::file_dialog_show(const String &p_title, const String &p_current_directory, const String &p_filename, bool p_show_hidden, DisplaySystemEnums::FileDialogMode p_mode, const Vector<String> &p_filters, const Callable &p_callback, DisplaySystemEnums::WindowID p_window_id) {
	ERR_FAIL_COND_V(p_window_id != DisplaySystemEnums::MAIN_WINDOW_ID, ERR_UNAVAILABLE);
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, FAILED);
	file_picker_callback = p_callback;
	return orblit_java->show_file_picker(p_current_directory, p_filename, p_mode, p_filters);
}

void DisplaySystemAndroid::emit_file_picker_callback(bool p_ok, const Vector<String> &p_selected_paths) {
	if (file_picker_callback.is_valid()) {
		file_picker_callback.call_deferred(p_ok, p_selected_paths, 0);
	}
}

Color DisplaySystemAndroid::get_accent_color() const {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, Color(0, 0, 0, 0));
	return orblit_java->get_accent_color();
}

Color DisplaySystemAndroid::get_base_color() const {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, Color(0, 0, 0, 0));
	return orblit_java->get_base_color();
}

TypedArray<Rect2> DisplaySystemAndroid::get_display_cutouts(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, TypedArray<Rect2>());

	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL_V(orblit_io_java, Array());
	return orblit_io_java->get_display_cutouts();
}

Rect2i DisplaySystemAndroid::get_display_safe_area(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, Rect2i());

	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL_V(orblit_io_java, Rect2i());
	return orblit_io_java->get_display_safe_area();
}

void DisplaySystemAndroid::screen_set_keep_on(bool p_enable) {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL(orblit_java);

	orblit_java->set_keep_screen_on(p_enable);
	keep_screen_on = p_enable;
}

bool DisplaySystemAndroid::screen_is_kept_on() const {
	return keep_screen_on;
}

void DisplaySystemAndroid::screen_set_orientation(DisplaySystemEnums::ScreenOrientation p_orientation, int p_screen) {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX(p_screen, screen_count);

	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL(orblit_io_java);

	orblit_io_java->set_screen_orientation(p_orientation);
}

DisplaySystemEnums::ScreenOrientation DisplaySystemAndroid::screen_get_orientation(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, DisplaySystemEnums::SCREEN_LANDSCAPE);

	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL_V(orblit_io_java, DisplaySystemEnums::SCREEN_LANDSCAPE);

	const int orientation = orblit_io_java->get_screen_orientation();
	ERR_FAIL_INDEX_V_MSG(orientation, 7, DisplaySystemEnums::SCREEN_LANDSCAPE, "Unrecognized screen orientation");
	return (DisplaySystemEnums::ScreenOrientation)orientation;
}

int DisplaySystemAndroid::get_display_rotation() const {
	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL_V(orblit_io_java, 0);

	return orblit_io_java->get_display_rotation();
}

int DisplaySystemAndroid::get_screen_count() const {
	return 1;
}

int DisplaySystemAndroid::get_primary_screen() const {
	return 0;
}

Point2i DisplaySystemAndroid::screen_get_position(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, Point2i());

	return Point2i(0, 0);
}

Size2i DisplaySystemAndroid::screen_get_size(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, Size2i());

	return OS_Android::get_singleton()->get_display_size();
}

Rect2i DisplaySystemAndroid::screen_get_usable_rect(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, Rect2i());

	Size2i display_size = OS_Android::get_singleton()->get_display_size();
	return Rect2i(0, 0, display_size.width, display_size.height);
}

int DisplaySystemAndroid::screen_get_dpi(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, 160);

	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL_V(orblit_io_java, 160);

	return orblit_io_java->get_screen_dpi();
}

float DisplaySystemAndroid::screen_get_scale(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, 1.0f);

	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL_V(orblit_io_java, 1.0f);

	float screen_scale = orblit_io_java->get_scaled_density();

	// Update the scale to avoid cropping.
	Size2i screen_size = screen_get_size(p_screen);
	if (screen_size != Size2i()) {
		bool is_portrait = screen_size.height > screen_size.width;
		float width_scale = screen_size.width / (float)(is_portrait ? OS_Android::DEFAULT_WINDOW_HEIGHT : OS_Android::DEFAULT_WINDOW_WIDTH);
		float height_scale = screen_size.height / (float)(is_portrait ? OS_Android::DEFAULT_WINDOW_WIDTH : OS_Android::DEFAULT_WINDOW_HEIGHT);
		screen_scale = MIN(screen_scale, MIN(width_scale, height_scale));
	}

	return screen_scale;
}

float DisplaySystemAndroid::screen_get_refresh_rate(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, SCREEN_REFRESH_RATE_FALLBACK);

	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	if (!orblit_io_java) {
		ERR_PRINT("An error occurred while trying to get the screen refresh rate.");
		return SCREEN_REFRESH_RATE_FALLBACK;
	}

	return orblit_io_java->get_screen_refresh_rate(SCREEN_REFRESH_RATE_FALLBACK);
}

bool DisplaySystemAndroid::is_touchscreen_available() const {
	return true;
}

void DisplaySystemAndroid::virtual_keyboard_show(const String &p_existing_text, const Rect2 &p_screen_rect, DisplaySystemEnums::VirtualKeyboardType p_type, int p_max_length, int p_cursor_start, int p_cursor_end) {
	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL(orblit_io_java);

	if (orblit_io_java->has_vk()) {
		orblit_io_java->show_vk(p_existing_text, (int)p_type, p_max_length, p_cursor_start, p_cursor_end);
	} else {
		ERR_PRINT("Virtual keyboard not available");
	}
}

void DisplaySystemAndroid::virtual_keyboard_hide() {
	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL(orblit_io_java);

	if (orblit_io_java->has_vk()) {
		orblit_io_java->hide_vk();
	} else {
		ERR_PRINT("Virtual keyboard not available");
	}
}

int DisplaySystemAndroid::virtual_keyboard_get_height() const {
	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL_V(orblit_io_java, 0);

	return orblit_io_java->get_vk_height();
}

bool DisplaySystemAndroid::has_hardware_keyboard() const {
	OrblitIOJavaWrapper *orblit_io_java = OS_Android::get_singleton()->get_orblit_io_java();
	ERR_FAIL_NULL_V(orblit_io_java, false);

	return orblit_io_java->has_hardware_keyboard();
}

void DisplaySystemAndroid::window_set_window_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_COND(p_window != DisplaySystemEnums::MAIN_WINDOW_ID);
	window_event_callback = p_callable;
}

void DisplaySystemAndroid::window_set_input_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_COND(p_window != DisplaySystemEnums::MAIN_WINDOW_ID);
	input_event_callback = p_callable;
}

void DisplaySystemAndroid::window_set_input_text_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_COND(p_window != DisplaySystemEnums::MAIN_WINDOW_ID);
	input_text_callback = p_callable;
}

void DisplaySystemAndroid::window_set_rect_changed_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_COND(p_window != DisplaySystemEnums::MAIN_WINDOW_ID);
	rect_changed_callback = p_callable;
}

void DisplaySystemAndroid::window_set_drop_files_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

template <typename... Args>
void DisplaySystemAndroid::_window_callback(const Callable &p_callable, bool p_deferred, const Args &...p_rest_args) const {
	if (p_callable.is_valid()) {
		if (p_deferred) {
			p_callable.call_deferred(p_rest_args...);
		} else {
			p_callable.call(p_rest_args...);
		}
	}
}

void DisplaySystemAndroid::send_window_event(DisplaySystemEnums::WindowEvent p_event, bool p_deferred) const {
	_window_callback(window_event_callback, p_deferred, int(p_event));
}

void DisplaySystemAndroid::send_input_event(const Ref<InputEvent> &p_event) const {
	_window_callback(input_event_callback, false, p_event);
}

void DisplaySystemAndroid::send_input_text(const String &p_text) const {
	_window_callback(input_text_callback, false, p_text, false);
}

void DisplaySystemAndroid::_dispatch_input_events(const Ref<InputEvent> &p_event) {
	DisplaySystemAndroid::get_singleton()->send_input_event(p_event);
}

Vector<DisplaySystemEnums::WindowID> DisplaySystemAndroid::get_window_list() const {
	Vector<DisplaySystemEnums::WindowID> ret;
	ret.push_back(DisplaySystemEnums::MAIN_WINDOW_ID);
	return ret;
}

DisplaySystemEnums::WindowID DisplaySystemAndroid::get_window_at_screen_position(const Point2i &p_position) const {
	return DisplaySystemEnums::MAIN_WINDOW_ID;
}

int64_t DisplaySystemAndroid::window_get_native_handle(DisplaySystemEnums::HandleType p_handle_type, DisplaySystemEnums::WindowID p_window) const {
	ERR_FAIL_COND_V(p_window != DisplaySystemEnums::MAIN_WINDOW_ID, 0);
	switch (p_handle_type) {
		case DisplaySystemEnums::WINDOW_HANDLE: {
			return reinterpret_cast<int64_t>(static_cast<OS_Android *>(OS::get_singleton())->get_orblit_java()->get_activity());
		}
		case DisplaySystemEnums::WINDOW_VIEW: {
			return 0; // Not supported.
		}
#ifdef GLES3_ENABLED
		case DisplaySystemEnums::DISPLAY_HANDLE: {
			if (rendering_driver == "opengl3") {
				return reinterpret_cast<int64_t>(eglGetCurrentDisplay());
			}
			return 0;
		}
		case DisplaySystemEnums::OPENGL_CONTEXT: {
			if (rendering_driver == "opengl3") {
				return reinterpret_cast<int64_t>(eglGetCurrentContext());
			}
			return 0;
		}
		case DisplaySystemEnums::EGL_DISPLAY: {
			// @todo Find a way to get this from the Java side.
			return 0;
		}
		case DisplaySystemEnums::EGL_CONFIG: {
			// @todo Find a way to get this from the Java side.
			return 0;
		}
#endif
		default: {
			return 0;
		}
	}
}

void DisplaySystemAndroid::window_attach_instance_id(ObjectID p_instance, DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_COND(p_window != DisplaySystemEnums::MAIN_WINDOW_ID);
	window_attached_instance_id = p_instance;
}

ObjectID DisplaySystemAndroid::window_get_attached_instance_id(DisplaySystemEnums::WindowID p_window) const {
	ERR_FAIL_COND_V(p_window != DisplaySystemEnums::MAIN_WINDOW_ID, ObjectID());
	return window_attached_instance_id;
}

void DisplaySystemAndroid::window_set_title(const String &p_title, DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

int DisplaySystemAndroid::window_get_current_screen(DisplaySystemEnums::WindowID p_window) const {
	ERR_FAIL_COND_V(p_window != DisplaySystemEnums::MAIN_WINDOW_ID, DisplaySystemEnums::INVALID_SCREEN);
	return 0;
}

void DisplaySystemAndroid::window_set_current_screen(int p_screen, DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

Point2i DisplaySystemAndroid::window_get_position(DisplaySystemEnums::WindowID p_window) const {
	return Point2i();
}

Point2i DisplaySystemAndroid::window_get_position_with_decorations(DisplaySystemEnums::WindowID p_window) const {
	return Point2i();
}

void DisplaySystemAndroid::window_set_position(const Point2i &p_position, DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

void DisplaySystemAndroid::window_set_transient(DisplaySystemEnums::WindowID p_window, DisplaySystemEnums::WindowID p_parent) {
	// Not supported on Android.
}

void DisplaySystemAndroid::window_set_max_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

Size2i DisplaySystemAndroid::window_get_max_size(DisplaySystemEnums::WindowID p_window) const {
	return Size2i();
}

void DisplaySystemAndroid::window_set_min_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

Size2i DisplaySystemAndroid::window_get_min_size(DisplaySystemEnums::WindowID p_window) const {
	return Size2i();
}

void DisplaySystemAndroid::window_set_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

Size2i DisplaySystemAndroid::window_get_size(DisplaySystemEnums::WindowID p_window) const {
	return OS_Android::get_singleton()->get_display_size();
}

Size2i DisplaySystemAndroid::window_get_size_with_decorations(DisplaySystemEnums::WindowID p_window) const {
	return OS_Android::get_singleton()->get_display_size();
}

void DisplaySystemAndroid::window_set_mode(DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::WindowID p_window) {
	OS_Android::get_singleton()->get_orblit_java()->enable_immersive_mode(p_mode == DisplaySystemEnums::WINDOW_MODE_FULLSCREEN || p_mode == DisplaySystemEnums::WINDOW_MODE_EXCLUSIVE_FULLSCREEN);
}

DisplaySystemEnums::WindowMode DisplaySystemAndroid::window_get_mode(DisplaySystemEnums::WindowID p_window) const {
	if (OS_Android::get_singleton()->get_orblit_java()->is_in_immersive_mode()) {
		return DisplaySystemEnums::WINDOW_MODE_FULLSCREEN;
	} else {
		return DisplaySystemEnums::WINDOW_MODE_MAXIMIZED;
	}
}

bool DisplaySystemAndroid::window_is_maximize_allowed(DisplaySystemEnums::WindowID p_window) const {
	return false;
}

void DisplaySystemAndroid::window_set_flag(DisplaySystemEnums::WindowFlags p_flag, bool p_enabled, DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

bool DisplaySystemAndroid::window_get_flag(DisplaySystemEnums::WindowFlags p_flag, DisplaySystemEnums::WindowID p_window) const {
	ERR_FAIL_COND_V(p_window != DisplaySystemEnums::MAIN_WINDOW_ID, false);
	switch (p_flag) {
		case DisplaySystemEnums::WindowFlags::WINDOW_FLAG_TRANSPARENT:
			return is_window_transparency_available();

		default:
			return false;
	}
}

void DisplaySystemAndroid::window_request_attention(DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

void DisplaySystemAndroid::window_move_to_foreground(DisplaySystemEnums::WindowID p_window) {
	// Not supported on Android.
}

bool DisplaySystemAndroid::window_is_focused(DisplaySystemEnums::WindowID p_window) const {
	return true;
}

bool DisplaySystemAndroid::window_can_draw(DisplaySystemEnums::WindowID p_window) const {
	return true;
}

bool DisplaySystemAndroid::can_any_window_draw() const {
	return true;
}

void DisplaySystemAndroid::window_set_color(const Color &p_color) {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL(orblit_java);
	orblit_java->set_window_color(p_color);
}

void DisplaySystemAndroid::process_events() {
	Input::get_singleton()->flush_buffered_events();
}

Vector<String> DisplaySystemAndroid::get_rendering_drivers_func() {
	Vector<String> drivers;

#ifdef GLES3_ENABLED
	drivers.push_back("opengl3");
#endif
#ifdef VULKAN_ENABLED
	drivers.push_back("vulkan");
#endif

	return drivers;
}

DisplaySystem *DisplaySystemAndroid::create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error) {
	DisplaySystem *ds = memnew(DisplaySystemAndroid(p_rendering_driver, p_mode, p_vsync_mode, p_flags, p_position, p_resolution, p_screen, p_context, p_parent_window, r_error));
	if (r_error != OK) {
		if (p_rendering_driver == "vulkan") {
			OS::get_singleton()->alert(
					"Your device seems not to support the required Vulkan version.\n\n"
					"Please try exporting your game using the 'gl_compatibility' renderer.",
					"Unable to initialize Vulkan video driver");
		} else {
			OS::get_singleton()->alert(
					"Your device seems not to support the required OpenGL ES 3.0 version.",
					"Unable to initialize OpenGL video driver");
		}
	}
	return ds;
}

void DisplaySystemAndroid::register_android_driver() {
	register_create_function("android", create_func, get_rendering_drivers_func);
}

#ifdef VULKAN_ENABLED
bool DisplaySystemAndroid::check_vulkan_global_context(bool p_vulkan_requirements_met) {
	if (!rendering_context_global_checked) {
		bool fallback_to_opengl3 = GLOBAL_GET("rendering/rendering_device/fallback_to_opengl3");
		Error err = ERR_CANT_CREATE;
		if (p_vulkan_requirements_met) {
			rendering_context_global = memnew(RenderingContextDriverVulkanAndroid);
			err = rendering_context_global->initialize();
		}

		if (err != OK) {
			if (rendering_context_global != nullptr) {
				memdelete(rendering_context_global);
				rendering_context_global = nullptr;
			}

#if defined(GLES3_ENABLED)
			if (fallback_to_opengl3) {
				WARN_PRINT("Your device does not seem to support Vulkan, switching to OpenGL 3.");
				OS::get_singleton()->set_current_rendering_driver_name("opengl3", OS::RENDERING_SOURCE_FALLBACK);
				OS::get_singleton()->set_current_rendering_method("gl_compatibility", OS::RENDERING_SOURCE_FALLBACK);
			} else
#endif
			{
				ERR_PRINT("Failed to initialize Vulkan context.");
			}
		}

		rendering_context_global_checked = true;
	}

	return rendering_context_global != nullptr;
}

void DisplaySystemAndroid::free_vulkan_global_context() {
	if (rendering_context_global != nullptr) {
		memdelete(rendering_context_global);
		rendering_context_global = nullptr;
		rendering_context_global_checked = false;
	}
}
#endif

void DisplaySystemAndroid::reset_window() {
#if defined(RD_ENABLED)
	if (rendering_context_global) {
		if (rendering_device) {
			rendering_device->screen_free(DisplaySystemEnums::MAIN_WINDOW_ID);
		}

		DisplaySystemEnums::VSyncMode last_vsync_mode = rendering_context_global->window_get_vsync_mode(DisplaySystemEnums::MAIN_WINDOW_ID);
		rendering_context_global->window_destroy(DisplaySystemEnums::MAIN_WINDOW_ID);

		union {
#ifdef VULKAN_ENABLED
			RenderingContextDriverVulkanAndroid::WindowPlatformData vulkan;
#endif
		} wpd;
#ifdef VULKAN_ENABLED
		if (rendering_driver == "vulkan") {
			ANativeWindow *native_window = OS_Android::get_singleton()->get_native_window();
			ERR_FAIL_NULL(native_window);
			wpd.vulkan.window = native_window;
		}
#endif

		if (rendering_context_global->window_create(DisplaySystemEnums::MAIN_WINDOW_ID, &wpd) != OK) {
			ERR_PRINT(vformat("Failed to reset %s window.", rendering_driver));
			return;
		}

		Size2i display_size = OS_Android::get_singleton()->get_display_size();
		rendering_context_global->window_set_size(DisplaySystemEnums::MAIN_WINDOW_ID, display_size.width, display_size.height);
		rendering_context_global->window_set_vsync_mode(DisplaySystemEnums::MAIN_WINDOW_ID, last_vsync_mode);

		if (rendering_device) {
			rendering_device->screen_create(DisplaySystemEnums::MAIN_WINDOW_ID);
		}
	}
#endif
}

void DisplaySystemAndroid::notify_surface_changed(int p_width, int p_height) {
	if (rect_changed_callback.is_valid()) {
		rect_changed_callback.call(Rect2i(0, 0, p_width, p_height));
	}
}

void DisplaySystemAndroid::notify_application_paused() {
#if defined(RD_ENABLED)
	if (rendering_device && rendering_device->is_pipeline_cache_enabled()) {
		rendering_device->update_pipeline_cache();
	}
#endif // defined(RD_ENABLED)
}

DisplaySystemAndroid::DisplaySystemAndroid(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error) {
	rendering_driver = p_rendering_driver;

	keep_screen_on = GLOBAL_GET("display/window/energy_saving/keep_screen_on");

	native_menu = memnew(NativeMenu);

#ifdef VULKAN_ENABLED
	if (rendering_driver == "vulkan") {
		check_vulkan_global_context(true);
		if (rendering_context_global == nullptr) {
			ERR_PRINT("Can't initialize display server with Vulkan driver because no Vulkan context is available.");
			r_error = ERR_UNAVAILABLE;
			return;
		}

		ANativeWindow *native_window = OS_Android::get_singleton()->get_native_window();
		ERR_FAIL_NULL(native_window);

		RenderingContextDriverVulkanAndroid::WindowPlatformData wpd;
		wpd.window = native_window;

		if (rendering_context_global->window_create(DisplaySystemEnums::MAIN_WINDOW_ID, &wpd) != OK) {
			ERR_PRINT(vformat("Failed to create %s window.", rendering_driver));
			r_error = ERR_UNAVAILABLE;
			return;
		}

		Size2i display_size = OS_Android::get_singleton()->get_display_size();
		rendering_context_global->window_set_size(DisplaySystemEnums::MAIN_WINDOW_ID, display_size.width, display_size.height);
		rendering_context_global->window_set_vsync_mode(DisplaySystemEnums::MAIN_WINDOW_ID, p_vsync_mode);

		rendering_device = memnew(RenderingDevice);
		if (rendering_device->initialize(rendering_context_global, DisplaySystemEnums::MAIN_WINDOW_ID) != OK) {
			rendering_device = nullptr;
			r_error = ERR_UNAVAILABLE;
			return;
		}

		rendering_device->screen_create(DisplaySystemEnums::MAIN_WINDOW_ID);

		RendererCompositorRD::make_current();
	}
#endif

#if defined(GLES3_ENABLED)
	if (rendering_driver == "opengl3") {
		RasterizerGLES3::make_current(false);
	}
#endif

	Input::get_singleton()->set_event_dispatch_function(_dispatch_input_events);

	r_error = OK;
}

DisplaySystemAndroid::~DisplaySystemAndroid() {
	if (native_menu) {
		memdelete(native_menu);
		native_menu = nullptr;
	}

#if defined(RD_ENABLED)
	memdelete(rendering_device);

	free_vulkan_global_context();
#endif
}

void DisplaySystemAndroid::process_accelerometer(const Vector3 &p_accelerometer) {
	Input::get_singleton()->set_accelerometer(p_accelerometer);
}

void DisplaySystemAndroid::process_gravity(const Vector3 &p_gravity) {
	Input::get_singleton()->set_gravity(p_gravity);
}

void DisplaySystemAndroid::process_magnetometer(const Vector3 &p_magnetometer) {
	Input::get_singleton()->set_magnetometer(p_magnetometer);
}

void DisplaySystemAndroid::process_gyroscope(const Vector3 &p_gyroscope) {
	Input::get_singleton()->set_gyroscope(p_gyroscope);
}

void DisplaySystemAndroid::process_device_orientation(const Quaternion &p_orientation) {
	Input::get_singleton()->set_device_orientation(p_orientation);
}

void DisplaySystemAndroid::_mouse_update_mode() {
	DisplaySystemEnums::MouseMode wanted_mouse_mode = mouse_mode_override_enabled
			? mouse_mode_override
			: mouse_mode_base;

	if (!OS_Android::get_singleton()->get_orblit_java()->get_orblit_view()->can_update_pointer_icon() || !OS_Android::get_singleton()->get_orblit_java()->get_orblit_view()->can_capture_pointer()) {
		return;
	}
	if (mouse_mode == wanted_mouse_mode) {
		return;
	}

	if (wanted_mouse_mode == DisplaySystemEnums::MouseMode::MOUSE_MODE_HIDDEN) {
		OS_Android::get_singleton()->get_orblit_java()->get_orblit_view()->set_pointer_icon(CURSOR_TYPE_NULL);
	} else {
		cursor_set_shape(cursor_shape);
	}

	if (wanted_mouse_mode == DisplaySystemEnums::MouseMode::MOUSE_MODE_CAPTURED) {
		OS_Android::get_singleton()->get_orblit_java()->get_orblit_view()->request_pointer_capture();
	} else {
		OS_Android::get_singleton()->get_orblit_java()->get_orblit_view()->release_pointer_capture();
	}

	mouse_mode = wanted_mouse_mode;
}

void DisplaySystemAndroid::mouse_set_mode(DisplaySystemEnums::MouseMode p_mode) {
	ERR_FAIL_INDEX(p_mode, DisplaySystemEnums::MouseMode::MOUSE_MODE_MAX);
	if (p_mode == mouse_mode_base) {
		return;
	}
	mouse_mode_base = p_mode;
	_mouse_update_mode();
}

DisplaySystemEnums::MouseMode DisplaySystemAndroid::mouse_get_mode() const {
	return mouse_mode;
}

void DisplaySystemAndroid::mouse_set_mode_override(DisplaySystemEnums::MouseMode p_mode) {
	ERR_FAIL_INDEX(p_mode, DisplaySystemEnums::MouseMode::MOUSE_MODE_MAX);
	if (p_mode == mouse_mode_override) {
		return;
	}
	mouse_mode_override = p_mode;
	_mouse_update_mode();
}

DisplaySystemEnums::MouseMode DisplaySystemAndroid::mouse_get_mode_override() const {
	return mouse_mode_override;
}

void DisplaySystemAndroid::mouse_set_mode_override_enabled(bool p_override_enabled) {
	mouse_mode_override_enabled = p_override_enabled;
	_mouse_update_mode();
}

bool DisplaySystemAndroid::mouse_is_mode_override_enabled() const {
	return mouse_mode_override_enabled;
}

Point2i DisplaySystemAndroid::mouse_get_position() const {
	return Input::get_singleton()->get_mouse_position();
}

BitField<MouseButtonMask> DisplaySystemAndroid::mouse_get_button_state() const {
	return Input::get_singleton()->get_mouse_button_mask();
}

void DisplaySystemAndroid::_cursor_set_shape_helper(DisplaySystemEnums::CursorShape p_shape, bool force) {
	if (!OS_Android::get_singleton()->get_orblit_java()->get_orblit_view()->can_update_pointer_icon()) {
		return;
	}
	if (cursor_shape == p_shape && !force) {
		return;
	}

	cursor_shape = p_shape;

	if (mouse_mode == DisplaySystemEnums::MouseMode::MOUSE_MODE_VISIBLE || mouse_mode == DisplaySystemEnums::MouseMode::MOUSE_MODE_CONFINED) {
		OS_Android::get_singleton()->get_orblit_java()->get_orblit_view()->set_pointer_icon(android_cursors[cursor_shape]);
	}
}

void DisplaySystemAndroid::cursor_set_shape(DisplaySystemEnums::CursorShape p_shape) {
	ERR_FAIL_INDEX(p_shape, DisplaySystemEnums::CURSOR_MAX);
	_cursor_set_shape_helper(p_shape);
}

DisplaySystemEnums::CursorShape DisplaySystemAndroid::cursor_get_shape() const {
	return cursor_shape;
}

void DisplaySystemAndroid::cursor_set_custom_image(const Ref<Resource> &p_cursor, DisplaySystemEnums::CursorShape p_shape, const Vector2 &p_hotspot) {
	ERR_FAIL_INDEX(p_shape, DisplaySystemEnums::CURSOR_MAX);
	String cursor_path = p_cursor.is_valid() ? p_cursor->get_path() : "";
	if (!cursor_path.is_empty()) {
		cursor_path = ProjectSettings::get_singleton()->globalize_path(cursor_path);
	}
	OS_Android::get_singleton()->get_orblit_java()->get_orblit_view()->configure_pointer_icon(android_cursors[cursor_shape], cursor_path, p_hotspot);
	_cursor_set_shape_helper(p_shape, true);
}

void DisplaySystemAndroid::window_set_vsync_mode(DisplaySystemEnums::VSyncMode p_vsync_mode, DisplaySystemEnums::WindowID p_window) {
#if defined(RD_ENABLED)
	if (rendering_context_global) {
		rendering_context_global->window_set_vsync_mode(p_window, p_vsync_mode);
	}
#endif
}

DisplaySystemEnums::VSyncMode DisplaySystemAndroid::window_get_vsync_mode(DisplaySystemEnums::WindowID p_window) const {
#if defined(RD_ENABLED)
	if (rendering_context_global) {
		return rendering_context_global->window_get_vsync_mode(p_window);
	}
#endif
	return DisplaySystemEnums::VSYNC_ENABLED;
}

void DisplaySystemAndroid::reset_swap_buffers_flag() {
	swap_buffers_flag = false;
}

bool DisplaySystemAndroid::should_swap_buffers() const {
	return swap_buffers_flag;
}

void DisplaySystemAndroid::swap_buffers() {
	swap_buffers_flag = true;
}

void DisplaySystemAndroid::set_native_icon(const String &p_filename) {
	// NOT SUPPORTED
}

void DisplaySystemAndroid::set_icon(const Ref<Image> &p_icon) {
	// NOT SUPPORTED
}

bool DisplaySystemAndroid::is_window_transparency_available() const {
	return GLOBAL_GET_CACHED(bool, "display/window/per_pixel_transparency/allowed");
}

bool DisplaySystemAndroid::is_in_pip_mode(DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_COND_V(p_window != DisplaySystemEnums::MAIN_WINDOW_ID, false);
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL_V(orblit_java, false);
	return orblit_java->is_in_pip_mode();
}

void DisplaySystemAndroid::pip_mode_enter(DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_COND(p_window != DisplaySystemEnums::MAIN_WINDOW_ID);
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL(orblit_java);
	orblit_java->enter_pip_mode();
}

void DisplaySystemAndroid::pip_mode_set_aspect_ratio(int p_numerator, int p_denominator, DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_COND(p_window != DisplaySystemEnums::MAIN_WINDOW_ID);
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL(orblit_java);
	orblit_java->set_pip_mode_aspect_ratio(p_numerator, p_denominator);
}

void DisplaySystemAndroid::pip_mode_set_auto_enter_on_background(bool p_auto_enter_on_background, DisplaySystemEnums::WindowID p_window) {
	ERR_FAIL_COND(p_window != DisplaySystemEnums::MAIN_WINDOW_ID);
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	ERR_FAIL_NULL(orblit_java);
	orblit_java->set_auto_enter_pip_mode_on_background(p_auto_enter_on_background);
}
