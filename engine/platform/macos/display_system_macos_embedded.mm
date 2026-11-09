/**************************************************************************/
/*  display_system_macos_embedded.mm                                      */

#import "display_system_macos_embedded.h"

#import "embedded_debugger.h"

#import "core/config/project_settings.h"
#import "core/debugger/engine_debugger.h"
#import "core/input/input.h"
#import "core/input/input_event.h"
#import "core/io/marshalls.h"
#import "core/os/main_loop.h"
#import "core/os/os.h"
#import "systems/display/native_menu.h"

#if defined(GLES3_ENABLED)
#import "embedded_gl_manager.h"

#import "backends/opengl/rasterizer_gles3.h"

#import <platform_gl.h>
#endif

#if defined(RD_ENABLED)
#import "systems/rendering/renderer_rd/renderer_compositor_rd.h"
#import "systems/rendering/rendering_device.h"

#if defined(VULKAN_ENABLED)
#import "rendering_context_driver_vulkan_macos.h"
#endif // VULKAN_ENABLED
#if defined(METAL_ENABLED)
#import "backends/metal/rendering_context_driver_metal.h"
#endif
#endif // RD_ENABLED

// Keep Quartz after rendering includes, as it includes system GL.h
// which clashes with GLAD.
#import "macos_quartz_core_spi.h"

DisplaySystemMacOSEmbedded::DisplaySystemMacOSEmbedded(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, Error &r_error) {
	EmbeddedDebugger::initialize(this);

	r_error = OK; // default to OK

	native_menu = memnew(NativeMenu);

	Input::get_singleton()->set_event_dispatch_function(_dispatch_input_events);

	rendering_driver = p_rendering_driver;

#if defined(RD_ENABLED)
#if defined(VULKAN_ENABLED)
#if defined(__x86_64__)
	bool fallback_to_vulkan = GLOBAL_GET("rendering/rendering_device/fallback_to_vulkan");
	if (!fallback_to_vulkan) {
		WARN_PRINT("Metal is not supported on Intel Macs, switching to Vulkan.");
	}
	// Metal rendering driver not available on Intel.
	if (rendering_driver == "metal") {
		rendering_driver = "vulkan";
		OS::get_singleton()->set_current_rendering_driver_name(rendering_driver, OS::RENDERING_SOURCE_FALLBACK);
	}
#endif
	if (rendering_driver == "vulkan") {
		rendering_context = memnew(RenderingContextDriverVulkanMacOS);
	}
#endif
#if defined(METAL_ENABLED)
	if (rendering_driver == "metal") {
		rendering_context = memnew(RenderingContextDriverMetal);
	}
#endif

	if (rendering_context) {
		if (rendering_context->initialize() != OK) {
			memdelete(rendering_context);
			rendering_context = nullptr;
#if defined(GLES3_ENABLED)
			bool fallback_to_opengl3 = GLOBAL_GET("rendering/rendering_device/fallback_to_opengl3");
			if (fallback_to_opengl3 && rendering_driver != "opengl3") {
				WARN_PRINT("Your device does not seem to support MoltenVK or Metal, switching to OpenGL 3.");
				rendering_driver = "opengl3";
				OS::get_singleton()->set_current_rendering_method("gl_compatibility", OS::RENDERING_SOURCE_FALLBACK);
				OS::get_singleton()->set_current_rendering_driver_name(rendering_driver, OS::RENDERING_SOURCE_FALLBACK);
			} else
#endif
			{
				r_error = ERR_CANT_CREATE;
				ERR_FAIL_MSG("Could not initialize " + rendering_driver);
			}
		}
	}
#endif

#if defined(GLES3_ENABLED)
	if (rendering_driver == "opengl3_angle") {
		WARN_PRINT("ANGLE not supported for embedded display, switching to native OpenGL.");
		rendering_driver = "opengl3";
		OS::get_singleton()->set_current_rendering_driver_name(rendering_driver, OS::RENDERING_SOURCE_FALLBACK);
	}

	if (rendering_driver == "opengl3") {
		gl_manager = memnew(GLManagerEmbedded);
		if (gl_manager->initialize() != OK) {
			memdelete(gl_manager);
			gl_manager = nullptr;
			r_error = ERR_UNAVAILABLE;
			ERR_FAIL_MSG("Could not initialize native OpenGL.");
		}
		layer = [CALayer new];
		// OpenGL content is flipped, so it must be transformed.
		layer.anchorPoint = CGPointMake(0, 0);
		layer.transform = CATransform3DMakeScale(1.0, -1.0, 1.0);

		Size2i render_size = _source_to_render_size(p_resolution);
		Error err = gl_manager->window_create(window_id_counter, layer, render_size.width, render_size.height);
		if (err != OK) {
			ERR_FAIL_MSG("Could not create OpenGL context.");
		}
		gl_manager->set_vsync_enabled(p_vsync_mode != DisplaySystemEnums::VSYNC_DISABLED);
	}
#endif

#if defined(RD_ENABLED)
	if (rendering_context) {
		layer = [CAMetalLayer new];
		layer.anchorPoint = CGPointMake(0, 1);

		union {
#ifdef VULKAN_ENABLED
			RenderingContextDriverVulkanMacOS::WindowPlatformData vulkan;
#endif
#ifdef METAL_ENABLED
			RenderingContextDriverMetal::WindowPlatformData metal;
#endif
		} wpd;
#ifdef VULKAN_ENABLED
		if (rendering_driver == "vulkan") {
			wpd.vulkan.layer_ptr = (CAMetalLayer *const *)&layer;
		}
#endif
#ifdef METAL_ENABLED
		if (rendering_driver == "metal") {
			wpd.metal.layer = (__bridge CA::MetalLayer *)layer;
		}
#endif
		Error err = rendering_context->window_create(window_id_counter, &wpd);
		ERR_FAIL_COND_MSG(err != OK, vformat("Can't create a %s context", rendering_driver));

		Size2i render_size = _source_to_render_size(p_resolution);
		rendering_context->window_set_size(window_id_counter, render_size.width, render_size.height);
		rendering_context->window_set_vsync_mode(window_id_counter, p_vsync_mode);
	}
#endif

#if defined(GLES3_ENABLED)
	if (rendering_driver == "opengl3") {
		RasterizerGLES3::make_current(true);
	}
	if (rendering_driver == "opengl3_angle") {
		RasterizerGLES3::make_current(false);
	}
#endif
#if defined(RD_ENABLED)
	if (rendering_context) {
		rendering_device = memnew(RenderingDevice);
		rendering_device->initialize(rendering_context, DisplaySystemEnums::MAIN_WINDOW_ID);
		rendering_device->screen_create(DisplaySystemEnums::MAIN_WINDOW_ID);

		RendererCompositorRD::make_current();
	}
#endif

	CGFloat display_scale = state.screen_max_scale;
	CGFloat render_scale = screen_get_max_scale();
	layer.contentsScale = render_scale;
	layer.magnificationFilter = kCAFilterNearest;
	layer.minificationFilter = kCAFilterNearest;
	transparent = ((p_flags & DisplaySystemEnums::WINDOW_FLAG_TRANSPARENT_BIT) == DisplaySystemEnums::WINDOW_FLAG_TRANSPARENT_BIT);
	layer.opaque = !(OS::get_singleton()->is_layered_allowed() && transparent);
	layer.actions = @{ @"contents" : [NSNull null] }; // Disable implicit animations for contents.
	// AppKit frames, bounds and positions are always in points.
	CGRect bounds = CGRectMake(0, 0, p_resolution.width, p_resolution.height);
	bounds = CGRectApplyAffineTransform(bounds, CGAffineTransformInvert(CGAffineTransformMakeScale(display_scale, display_scale)));
	layer.bounds = bounds;

	CGSConnectionID connection_id = CGSMainConnectionID();
	ca_context = [CAContext contextWithCGSConnection:connection_id options:@{ kCAContextCIFilterBehavior : @"ignore" }];
	ca_context.layer = layer;

	{
		Array arr = { ca_context.contextId };
		EngineDebugger::get_singleton()->send_message("game_view:set_context_id", arr);
	}
}

DisplaySystemMacOSEmbedded::~DisplaySystemMacOSEmbedded() {
	if (native_menu) {
		memdelete(native_menu);
		native_menu = nullptr;
	}

	EmbeddedDebugger::deinitialize();

#if defined(GLES3_ENABLED)
	if (gl_manager) {
		memdelete(gl_manager);
		gl_manager = nullptr;
	}
#endif

#if defined(RD_ENABLED)
	if (rendering_device) {
		memdelete(rendering_device);
		rendering_device = nullptr;
	}

	if (rendering_context) {
		memdelete(rendering_context);
		rendering_context = nullptr;
	}
#endif
}

DisplaySystem *DisplaySystemMacOSEmbedded::create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t /* p_parent_window */, Error &r_error) {
	return memnew(DisplaySystemMacOSEmbedded(p_rendering_driver, p_mode, p_vsync_mode, p_flags, p_position, p_resolution, p_screen, p_context, r_error));
}

Vector<String> DisplaySystemMacOSEmbedded::get_rendering_drivers_func() {
	Vector<String> drivers;

#if defined(VULKAN_ENABLED)
	drivers.push_back("vulkan");
#endif
#if defined(METAL_ENABLED)
	drivers.push_back("metal");
#endif
#if defined(GLES3_ENABLED)
	drivers.push_back("opengl3");
#endif

	return drivers;
}

void DisplaySystemMacOSEmbedded::register_embedded_driver() {
	register_create_function("embedded", create_func, get_rendering_drivers_func);
}

// MARK: - Mouse

void DisplaySystemMacOSEmbedded::_mouse_apply_mode(DisplaySystemEnums::MouseMode p_prev_mode, DisplaySystemEnums::MouseMode p_new_mode) {
	EngineDebugger::get_singleton()->send_message("game_view:mouse_set_mode", { p_new_mode });
}

void DisplaySystemMacOSEmbedded::warp_mouse(const Point2i &p_position) {
	_THREAD_SAFE_METHOD_
	Input::get_singleton()->set_mouse_position(p_position);
	// Convert from game pixels to points for the editor.
	float inv_scale = 1.0f / screen_get_max_scale();
	EngineDebugger::get_singleton()->send_message("game_view:warp_mouse", { Point2i(Vector2(p_position) * inv_scale) });
}

Point2i DisplaySystemMacOSEmbedded::mouse_get_position() const {
	_THREAD_SAFE_METHOD_

	const NSPoint mouse_pos = [NSEvent mouseLocation];
	const float scale = screen_get_max_scale();

	for (NSScreen *screen in [NSScreen screens]) {
		NSRect frame = [screen frame];
		if (NSMouseInRect(mouse_pos, frame, NO)) {
			Vector2i pos = Vector2i((int)mouse_pos.x, (int)mouse_pos.y);
			pos *= scale;
			// TODO(sgc): fix this
			// pos -= _get_screens_origin();
			pos.y *= -1;
			return pos;
		}
	}
	return Vector2i();
}

BitField<MouseButtonMask> DisplaySystemMacOSEmbedded::mouse_get_button_state() const {
	BitField<MouseButtonMask> last_button_state = MouseButtonMask::NONE;

	NSUInteger buttons = [NSEvent pressedMouseButtons];
	if (buttons & (1 << 0)) {
		last_button_state.set_flag(MouseButtonMask::LEFT);
	}
	if (buttons & (1 << 1)) {
		last_button_state.set_flag(MouseButtonMask::RIGHT);
	}
	if (buttons & (1 << 2)) {
		last_button_state.set_flag(MouseButtonMask::MIDDLE);
	}
	if (buttons & (1 << 3)) {
		last_button_state.set_flag(MouseButtonMask::MB_XBUTTON1);
	}
	if (buttons & (1 << 4)) {
		last_button_state.set_flag(MouseButtonMask::MB_XBUTTON2);
	}
	return last_button_state;
}

// MARK: Events

void DisplaySystemMacOSEmbedded::window_set_rect_changed_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	window_resize_callbacks[p_window] = p_callable;
}

void DisplaySystemMacOSEmbedded::window_set_window_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	window_event_callbacks[p_window] = p_callable;
}
void DisplaySystemMacOSEmbedded::window_set_input_event_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	input_event_callbacks[p_window] = p_callable;
}

void DisplaySystemMacOSEmbedded::window_set_input_text_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	input_text_callbacks[p_window] = p_callable;
}

void DisplaySystemMacOSEmbedded::window_set_drop_files_callback(const Callable &p_callable, DisplaySystemEnums::WindowID p_window) {
	// Not supported
}

void DisplaySystemMacOSEmbedded::process_events() {
	Input *input = Input::get_singleton();
	input->flush_buffered_events();
}

void DisplaySystemMacOSEmbedded::_dispatch_input_events(const Ref<InputEvent> &p_event) {
	Ref<InputEventFromWindow> event_from_window = p_event;
	DisplaySystemEnums::WindowID window_id = DisplaySystemEnums::INVALID_WINDOW_ID;
	if (event_from_window.is_valid()) {
		window_id = event_from_window->get_window_id();
	}
	DisplaySystemMacOSEmbedded *ds = (DisplaySystemMacOSEmbedded *)DisplaySystem::get_singleton();
	ds->send_input_event(p_event, window_id);
}

void DisplaySystemMacOSEmbedded::send_input_event(const Ref<InputEvent> &p_event, DisplaySystemEnums::WindowID p_id) const {
	if (p_id != DisplaySystemEnums::INVALID_WINDOW_ID) {
		const Callable *cb = input_event_callbacks.getptr(p_id);
		if (cb) {
			_window_callback(*cb, p_event);
		}
	} else {
		for (const KeyValue<DisplaySystemEnums::WindowID, Callable> &E : input_event_callbacks) {
			_window_callback(E.value, p_event);
		}
	}
}

void DisplaySystemMacOSEmbedded::send_input_text(const String &p_text, DisplaySystemEnums::WindowID p_id) const {
	const Callable *cb = input_text_callbacks.getptr(p_id);
	if (cb) {
		_window_callback(*cb, p_text);
	}
}

void DisplaySystemMacOSEmbedded::send_window_event_by_id(DisplaySystemEnums::WindowEvent p_event, DisplaySystemEnums::WindowID p_id) const {
	const Callable *cb = window_event_callbacks.getptr(p_id);
	if (cb) {
		_window_callback(*cb, int(p_event));
	}
}

void DisplaySystemMacOSEmbedded::_window_callback(const Callable &p_callable, const Variant &p_arg) const {
	if (p_callable.is_valid()) {
		p_callable.call(p_arg);
	}
}

// MARK: -

bool DisplaySystemMacOSEmbedded::has_feature(DisplaySystemEnums::Feature p_feature) const {
	switch (p_feature) {
		case DisplaySystemEnums::FEATURE_CURSOR_SHAPE:
		case DisplaySystemEnums::FEATURE_IME:
		case DisplaySystemEnums::FEATURE_CUSTOM_CURSOR_SHAPE:
			// case DisplaySystemEnums::FEATURE_HIDPI:
			// case DisplaySystemEnums::FEATURE_ICON:
		case DisplaySystemEnums::FEATURE_MOUSE:
		case DisplaySystemEnums::FEATURE_HDR_OUTPUT:
		case DisplaySystemEnums::FEATURE_MOUSE_WARP:
			// case DisplaySystemEnums::FEATURE_NATIVE_DIALOG:
			// case DisplaySystemEnums::FEATURE_NATIVE_ICON:
			// case DisplaySystemEnums::FEATURE_WINDOW_TRANSPARENCY:
		case DisplaySystemEnums::FEATURE_CLIPBOARD:
			// case DisplaySystemEnums::FEATURE_KEEP_SCREEN_ON:
			// case DisplaySystemEnums::FEATURE_ORIENTATION:
			// case DisplaySystemEnums::FEATURE_VIRTUAL_KEYBOARD:
		case DisplaySystemEnums::FEATURE_TEXT_TO_SPEECH:
			// case DisplaySystemEnums::FEATURE_TOUCHSCREEN:
		case DisplaySystemEnums::FEATURE_EMBEDDED:
			return true;
		default:
			return false;
	}
}

String DisplaySystemMacOSEmbedded::get_name() const {
	return "embedded";
}

int DisplaySystemMacOSEmbedded::get_screen_count() const {
	return 1;
}

Point2i DisplaySystemMacOSEmbedded::screen_get_position(int p_screen) const {
	_THREAD_SAFE_METHOD_

	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, Point2i());

	return Point2i(0, 0);
}

Size2i DisplaySystemMacOSEmbedded::screen_get_size(int p_screen) const {
	_THREAD_SAFE_METHOD_

	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, Size2i());

	return window_get_size(DisplaySystemEnums::MAIN_WINDOW_ID);
}

Rect2i DisplaySystemMacOSEmbedded::screen_get_usable_rect(int p_screen) const {
	_THREAD_SAFE_METHOD_

	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, Rect2i());

	return Rect2i(screen_get_position(p_screen), screen_get_size(p_screen));
}

int DisplaySystemMacOSEmbedded::screen_get_dpi(int p_screen) const {
	_THREAD_SAFE_METHOD_

	p_screen = _get_screen_index(p_screen);
	int screen_count = get_screen_count();
	ERR_FAIL_INDEX_V(p_screen, screen_count, 72);

	return 96;
}

float DisplaySystemMacOSEmbedded::screen_get_scale(int p_screen) const {
	_THREAD_SAFE_METHOD_

	if (!OS::get_singleton()->is_hidpi_allowed()) {
		return 1.0f;
	}

	switch (p_screen) {
		case DisplaySystemEnums::SCREEN_WITH_MOUSE_FOCUS:
		case DisplaySystemEnums::SCREEN_WITH_KEYBOARD_FOCUS:
		case DisplaySystemEnums::SCREEN_PRIMARY:
		case DisplaySystemEnums::SCREEN_OF_MAIN_WINDOW:
		case 0:
			return state.screen_window_scale;
		default:
			return 1.0f;
	}
}

Vector<DisplaySystemEnums::WindowID> DisplaySystemMacOSEmbedded::get_window_list() const {
	Vector<DisplaySystemEnums::WindowID> list;
	list.push_back(DisplaySystemEnums::MAIN_WINDOW_ID);
	return list;
}

DisplaySystemEnums::WindowID DisplaySystemMacOSEmbedded::get_window_at_screen_position(const Point2i &p_position) const {
	return DisplaySystemEnums::MAIN_WINDOW_ID;
}

void DisplaySystemMacOSEmbedded::window_attach_instance_id(ObjectID p_instance, DisplaySystemEnums::WindowID p_window) {
	window_attached_instance_id[p_window] = p_instance;
}

ObjectID DisplaySystemMacOSEmbedded::window_get_attached_instance_id(DisplaySystemEnums::WindowID p_window) const {
	return window_attached_instance_id[p_window];
}

void DisplaySystemMacOSEmbedded::window_set_title(const String &p_title, DisplaySystemEnums::WindowID p_window) {
	// Not supported
}

int DisplaySystemMacOSEmbedded::window_get_current_screen(DisplaySystemEnums::WindowID p_window) const {
	_THREAD_SAFE_METHOD_
	ERR_FAIL_COND_V(p_window != DisplaySystemEnums::MAIN_WINDOW_ID, DisplaySystemEnums::INVALID_SCREEN);

	return 0;
}

void DisplaySystemMacOSEmbedded::window_set_current_screen(int p_screen, DisplaySystemEnums::WindowID p_window) {
	// Not supported
}

Point2i DisplaySystemMacOSEmbedded::window_get_position(DisplaySystemEnums::WindowID p_window) const {
	return Point2i();
}

Point2i DisplaySystemMacOSEmbedded::window_get_position_with_decorations(DisplaySystemEnums::WindowID p_window) const {
	return Point2i();
}

void DisplaySystemMacOSEmbedded::window_set_position(const Point2i &p_position, DisplaySystemEnums::WindowID p_window) {
	// Probably not supported for single window iOS app
}

void DisplaySystemMacOSEmbedded::window_set_transient(DisplaySystemEnums::WindowID p_window, DisplaySystemEnums::WindowID p_parent) {
	// Not supported
}

void DisplaySystemMacOSEmbedded::window_set_max_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window) {
	// Not supported
}

Size2i DisplaySystemMacOSEmbedded::window_get_max_size(DisplaySystemEnums::WindowID p_window) const {
	return Size2i();
}

void DisplaySystemMacOSEmbedded::window_set_min_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window) {
	// Not supported
}

Size2i DisplaySystemMacOSEmbedded::window_get_min_size(DisplaySystemEnums::WindowID p_window) const {
	return Size2i();
}

void DisplaySystemMacOSEmbedded::window_set_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window) {
	print_line("Embedded window can't be resized.");
}

void DisplaySystemMacOSEmbedded::_window_set_size(const Size2i p_size, DisplaySystemEnums::WindowID p_window) {
	[CATransaction begin];
	[CATransaction setDisableActions:YES];

	CGFloat display_scale = state.screen_max_scale;
	CGFloat render_scale = screen_get_max_scale();
	CGRect bounds = CGRectMake(0, 0, p_size.width, p_size.height);
	bounds = CGRectApplyAffineTransform(bounds, CGAffineTransformInvert(CGAffineTransformMakeScale(display_scale, display_scale)));
	layer.bounds = bounds;
	layer.contentsScale = render_scale;

	Size2i render_size = _source_to_render_size(p_size);
#if defined(RD_ENABLED)
	if (rendering_context) {
		rendering_context->window_set_size(p_window, render_size.width, render_size.height);
	}
#endif
#if defined(GLES3_ENABLED)
	if (gl_manager) {
		gl_manager->window_resize(p_window, render_size.width, render_size.height);
	}
#endif
	[CATransaction commit];

	Callable *cb = window_resize_callbacks.getptr(p_window);
	if (cb) {
		Variant resize_rect = Rect2i(Point2i(), render_size);
		_window_callback(window_resize_callbacks[p_window], resize_rect);
	}
}

Size2i DisplaySystemMacOSEmbedded::window_get_size(DisplaySystemEnums::WindowID p_window) const {
#if defined(RD_ENABLED)
	if (rendering_context) {
		RenderingContextDriver::SurfaceID surface = rendering_context->surface_get_from_window(p_window);
		ERR_FAIL_COND_V_MSG(surface == 0, Size2i(), "Invalid window ID");
		uint32_t width = rendering_context->surface_get_width(surface);
		uint32_t height = rendering_context->surface_get_height(surface);
		return Size2i(width, height);
	}
#endif
#ifdef GLES3_ENABLED
	if (gl_manager) {
		return gl_manager->window_get_size(p_window);
	}
#endif
	return Size2i();
}

Size2i DisplaySystemMacOSEmbedded::window_get_size_with_decorations(DisplaySystemEnums::WindowID p_window) const {
	return window_get_size(p_window);
}

void DisplaySystemMacOSEmbedded::window_set_mode(DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::WindowID p_window) {
	// Not supported
}

DisplaySystemEnums::WindowMode DisplaySystemMacOSEmbedded::window_get_mode(DisplaySystemEnums::WindowID p_window) const {
	return DisplaySystemEnums::WindowMode::WINDOW_MODE_WINDOWED;
}

bool DisplaySystemMacOSEmbedded::window_is_maximize_allowed(DisplaySystemEnums::WindowID p_window) const {
	return false;
}

void DisplaySystemMacOSEmbedded::window_set_flag(DisplaySystemEnums::WindowFlags p_flag, bool p_enabled, DisplaySystemEnums::WindowID p_window) {
	if (p_flag == DisplaySystemEnums::WINDOW_FLAG_TRANSPARENT && p_window == DisplaySystemEnums::MAIN_WINDOW_ID) {
		transparent = p_enabled;
		layer.opaque = !(OS::get_singleton()->is_layered_allowed() && transparent);
	}
}

bool DisplaySystemMacOSEmbedded::window_get_flag(DisplaySystemEnums::WindowFlags p_flag, DisplaySystemEnums::WindowID p_window) const {
	if (p_flag == DisplaySystemEnums::WINDOW_FLAG_TRANSPARENT && p_window == DisplaySystemEnums::MAIN_WINDOW_ID) {
		return transparent;
	}
	return false;
}

void DisplaySystemMacOSEmbedded::window_request_attention(DisplaySystemEnums::WindowID p_window) {
	// Not supported
}

void DisplaySystemMacOSEmbedded::window_set_taskbar_progress_value(float p_value, DisplaySystemEnums::WindowID p_window) {
	// Not supported.
}

void DisplaySystemMacOSEmbedded::window_set_taskbar_progress_state(DisplaySystemEnums::ProgressState p_state, DisplaySystemEnums::WindowID p_window) {
	// Not supported.
}

void DisplaySystemMacOSEmbedded::window_move_to_foreground(DisplaySystemEnums::WindowID p_window) {
	// Not supported
}

bool DisplaySystemMacOSEmbedded::window_is_focused(DisplaySystemEnums::WindowID p_window) const {
	return true;
}

float DisplaySystemMacOSEmbedded::screen_get_max_scale() const {
	if (!OS::get_singleton()->is_hidpi_allowed()) {
		return 1.0f;
	}
	return state.screen_max_scale;
}

Size2i DisplaySystemMacOSEmbedded::_source_to_render_size(const Size2i &p_source_size) const {
	CGFloat display_scale = state.screen_max_scale;
	CGFloat render_scale = screen_get_max_scale();
	return Size2i((int)(p_source_size.width / display_scale * render_scale),
			(int)(p_source_size.height / display_scale * render_scale));
}

bool DisplaySystemMacOSEmbedded::window_can_draw(DisplaySystemEnums::WindowID p_window) const {
	return true;
}

bool DisplaySystemMacOSEmbedded::can_any_window_draw() const {
	return true;
}

void DisplaySystemMacOSEmbedded::window_set_ime_active(const bool p_active, DisplaySystemEnums::WindowID p_window) {
	EngineDebugger::get_singleton()->send_message("game_view:window_set_ime_active", { p_active });
}

void DisplaySystemMacOSEmbedded::window_set_ime_position(const Point2i &p_pos, DisplaySystemEnums::WindowID p_window) {
	if (p_pos == ime_last_position) {
		return;
	}
	EngineDebugger::get_singleton()->send_message("game_view:window_set_ime_position", { p_pos });
	ime_last_position = p_pos;
}

DisplaySystemMacOSBase::HDROutput &DisplaySystemMacOSEmbedded::_get_hdr_output(DisplaySystemEnums::WindowID p_window) {
	return hdr_output;
}

const DisplaySystemMacOSBase::HDROutput &DisplaySystemMacOSEmbedded::_get_hdr_output(DisplaySystemEnums::WindowID p_window) const {
	return hdr_output;
}

void DisplaySystemMacOSEmbedded::window_get_edr_values(DisplaySystemEnums::WindowID p_window, CGFloat *r_max_potential_edr_value, CGFloat *r_max_edr_value) const {
	_THREAD_SAFE_METHOD_

#define SET_VAL(v, val) \
	if (v) { \
		*v = val; \
	}

	SET_VAL(r_max_potential_edr_value, state.screen_max_edr);
	SET_VAL(r_max_edr_value, state.screen_max_potential_edr);

#undef SET_VAL
}

void DisplaySystemMacOSEmbedded::update_screen_parameters() {
	if (hdr_output.requested) {
		_update_hdr_output(DisplaySystemEnums::MAIN_WINDOW_ID, hdr_output);
	}
}

void DisplaySystemMacOSEmbedded::set_state(const DisplaySystemMacOSEmbeddedState &p_state) {
	if (state == p_state) {
		return;
	}

	uint32_t old_display_id = state.display_id;
	float old_scale = state.screen_max_scale;

	state = p_state;

	if (state.display_id != old_display_id) {
#if defined(GLES3_ENABLED)
		if (gl_manager) {
			gl_manager->set_display_id(state.display_id);
		}
#endif
	}
	if (state.screen_max_scale != old_scale) {
		// Recover source pixel size from current bounds using the old display scale.
		CGSize bounds_size = layer.bounds.size;
		Size2i source_size((int)(bounds_size.width * old_scale), (int)(bounds_size.height * old_scale));
		_window_set_size(source_size, DisplaySystemEnums::MAIN_WINDOW_ID);
	}
	if (hdr_output.requested) {
		_update_hdr_output(DisplaySystemEnums::MAIN_WINDOW_ID, hdr_output);
	}
}

void DisplaySystemMacOSEmbedded::window_set_vsync_mode(DisplaySystemEnums::VSyncMode p_vsync_mode, DisplaySystemEnums::WindowID p_window) {
#if defined(GLES3_ENABLED)
	if (gl_manager) {
		gl_manager->set_vsync_enabled(p_vsync_mode != DisplaySystemEnums::VSYNC_DISABLED);
	}
#endif

#if defined(RD_ENABLED)
	if (rendering_context) {
		rendering_context->window_set_vsync_mode(p_window, p_vsync_mode);
	}
#endif
}

DisplaySystemEnums::VSyncMode DisplaySystemMacOSEmbedded::window_get_vsync_mode(DisplaySystemEnums::WindowID p_window) const {
	_THREAD_SAFE_METHOD_
#if defined(GLES3_ENABLED)
	if (gl_manager) {
		return (gl_manager->is_vsync_enabled() ? DisplaySystemEnums::VSyncMode::VSYNC_ENABLED : DisplaySystemEnums::VSyncMode::VSYNC_DISABLED);
	}
#endif
#if defined(RD_ENABLED)
	if (rendering_context) {
		return rendering_context->window_get_vsync_mode(p_window);
	}
#endif
	return DisplaySystemEnums::VSYNC_ENABLED;
}

void DisplaySystemMacOSEmbedded::cursor_set_shape(DisplaySystemEnums::CursorShape p_shape) {
	cursor_shape = p_shape;
	EngineDebugger::get_singleton()->send_message("game_view:cursor_set_shape", { p_shape });
}

void DisplaySystemMacOSEmbedded::cursor_set_custom_image(const Ref<Resource> &p_cursor, DisplaySystemEnums::CursorShape p_shape, const Vector2 &p_hotspot) {
	PackedByteArray data;
	if (p_cursor.is_valid()) {
		Ref<Image> image = _get_cursor_image_from_resource(p_cursor, p_hotspot);
		if (image.is_valid()) {
			data = image->save_png_to_buffer();
		}
	}
	EngineDebugger::get_singleton()->send_message("game_view:cursor_set_custom_image", { data, p_shape, p_hotspot });
}

void DisplaySystemMacOSEmbedded::swap_buffers() {
#ifdef GLES3_ENABLED
	if (gl_manager) {
		gl_manager->swap_buffers();
	}
#endif
}

void DisplaySystemMacOSEmbeddedState::serialize(PackedByteArray &r_data) {
	r_data.resize(32);

	uint8_t *data = r_data.ptrw();
	data += encode_float(screen_max_scale, data);
	data += encode_float(screen_dpi, data);
	data += encode_float(screen_window_scale, data);
	data += encode_uint32(display_id, data);
	data += encode_double(screen_max_edr, data);
	data += encode_double(screen_max_potential_edr, data);

	// Assert we had enough space.
	DEV_ASSERT(r_data.size() >= (data - r_data.ptrw()));
}

Error DisplaySystemMacOSEmbeddedState::deserialize(const PackedByteArray &p_data) {
	const uint8_t *data = p_data.ptr();

	screen_max_scale = decode_float(data);
	data += sizeof(float);
	screen_dpi = decode_float(data);
	data += sizeof(float);
	screen_window_scale = decode_float(data);
	data += sizeof(float);
	display_id = decode_uint32(data);
	data += sizeof(uint32_t);
	screen_max_edr = decode_double(data);
	data += sizeof(double);
	screen_max_potential_edr = decode_double(data);

	return OK;
}
