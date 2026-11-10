/**************************************************************************/
/*  display_system_visionos.mm                                            */

#import "display_system_visionos.h"

#import "app_delegate_service_visionos.h"
#import "gc_keyboard_handler.h"

#import "core/os/os.h"

DisplaySystemVisionOS *DisplaySystemVisionOS::get_singleton() {
	return (DisplaySystemVisionOS *)DisplaySystemAppleEmbedded::get_singleton();
}

DisplaySystemVisionOS::DisplaySystemVisionOS(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error) :
		DisplaySystemAppleEmbedded(p_rendering_driver, p_mode, p_vsync_mode, p_flags, p_position, p_resolution, p_screen, p_context, p_parent_window, r_error) {
	String rendering_method = OS::get_singleton()->get_current_rendering_method();
	GDTRenderMode app_delegate_render_mode = GDTAppDelegateServiceVisionOS.renderMode;
	if (app_delegate_render_mode == GDTRenderModeCompositorServices && rendering_method == "forward_plus") {
		WARN_PRINT_ONCE("visionOS in immersive mode doesn't support the Forward+ renderer, switching to the Mobile renderer.");
		OS::get_singleton()->set_current_rendering_method("mobile", OS::RENDERING_SOURCE_FALLBACK);
	}

	// In Compositor Services mode, there is no UIViewController to receive UIPress
	// events, so we use the Game Controller framework's GCKeyboard API instead.
	if (app_delegate_render_mode == GDTRenderModeCompositorServices) {
		gc_keyboard = memnew(GCKeyboardHandler);
		gc_keyboard->start();
	}
}

DisplaySystemVisionOS::~DisplaySystemVisionOS() {
	if (gc_keyboard) {
		memdelete(gc_keyboard);
		gc_keyboard = nullptr;
	}
}

DisplaySystem *DisplaySystemVisionOS::create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error) {
	return memnew(DisplaySystemVisionOS(p_rendering_driver, p_mode, p_vsync_mode, p_flags, p_position, p_resolution, p_screen, p_context, p_parent_window, r_error));
}

void DisplaySystemVisionOS::register_visionos_driver() {
	register_create_function("visionOS", create_func, get_rendering_drivers_func);
}

String DisplaySystemVisionOS::get_name() const {
	return "visionOS";
}

int DisplaySystemVisionOS::screen_get_dpi(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, 72);

	// TODO(Apple): Compute this properly from SwiftUI Metric APIs
	return 72;
}

float DisplaySystemVisionOS::screen_get_refresh_rate(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, SCREEN_REFRESH_RATE_FALLBACK);

	return 90;
}

float DisplaySystemVisionOS::screen_get_scale(int p_screen) const {
	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, 1.0f);

	return 1;
}

bool DisplaySystemVisionOS::_screen_hdr_is_supported() const {
	return true;
}

float DisplaySystemVisionOS::_screen_potential_edr_headroom() const {
	// Current Apple Vision Pro hardware supports an EDR headroom of 2.0 (two times the SDR range).
	// See https://developer.apple.com/videos/play/wwdc2023/10089?time=603
	return 2.0f;
}

float DisplaySystemVisionOS::_screen_current_edr_headroom() const {
	// Current Apple Vision Pro hardware  supports an EDR headroom of 2.0 (two times the SDR range).
	// See https://developer.apple.com/videos/play/wwdc2023/10089?time=603
	return 2.0f;
}
