/**************************************************************************/
/*  java_lib_jni.cpp                                                      */

#include "java_lib_jni.h"

#include "android_input_handler.h"
#include "api/java_class_wrapper.h"
#include "dir_access_jandroid.h"
#include "display_system_android.h"
#include "file_access_android.h"
#include "file_access_filesystem_jandroid.h"
#include "java_io_wrapper.h"
#include "java_wrapper.h"
#include "jni_utils.h"
#include "net_socket_android.h"
#include "os_android.h"
#include "plugin/plugin_jni.h"
#include "thread_jandroid.h"
#include "tts_android.h"

#include "core/config/engine.h"
#include "core/config/project_settings.h"
#include "core/input/input.h"
#include "core/os/main_loop.h"
#include "core/os/os.h"
#include "core/os/thread.h"
#include "core/os/thread_safe.h"
#include "core/profiling/profiling.h"
#include "startup/main.h"
#include "systems/camera/camera_system.h"
#include "systems/display/accessibility_system.h"
#include "systems/rendering/rendering_system.h"

#ifndef XR_DISABLED
#include "systems/xr/xr_system.h"
#endif // XR_DISABLED

#ifdef TOOLS_ENABLED
#include "editor/settings/editor_settings.h"
#endif

#include <android/asset_manager_jni.h>
#include <android/input.h>
#include <android/native_window_jni.h>
#include <unistd.h>

static JavaClassWrapper *java_class_wrapper = nullptr;
static OS_Android *os_android = nullptr;
static AndroidInputHandler *input_handler = nullptr;
static OrblitJavaWrapper *orblit_java = nullptr;
static OrblitIOJavaWrapper *orblit_io_java = nullptr;

enum StartupStep {
	STEP_TERMINATED = -1,
	STEP_SETUP,
	STEP_SHOW_LOGO,
	STEP_STARTED
};

static SafeNumeric<int> step; // Shared between UI and render threads

static Vector3 accelerometer;
static Vector3 gravity;
static Vector3 magnetometer;
static Vector3 gyroscope;
static Quaternion device_orientation;

static void _terminate(JNIEnv *env, bool p_restart = false) {
	if (step.get() == STEP_TERMINATED) {
		return;
	}

	step.set(STEP_TERMINATED); // Ensure no further steps are attempted and no further events are sent

	AccessibilitySystem *ac = AccessibilitySystem::get_singleton();
	if (ac && ac->has_window(DisplaySystemEnums::MAIN_WINDOW_ID)) {
		ac->window_destroy(DisplaySystemEnums::MAIN_WINDOW_ID);
	}

	// lets cleanup
	// Unregister android plugins
	unregister_plugins_singletons();

	memdelete(java_class_wrapper);
	if (input_handler) {
		delete input_handler;
	}
	// Whether restarting is handled by 'Main::cleanup()'
	bool restart_on_cleanup = false;
	if (os_android) {
		restart_on_cleanup = os_android->is_restart_on_exit_set();
		os_android->main_loop_end();
		Main::cleanup();
		delete os_android;
	}
	if (orblit_io_java) {
		delete orblit_io_java;
	}

	TTS_Android::terminate();
	FileAccessAndroid::terminate();
	DirAccessJAndroid::terminate();
	FileAccessFilesystemJAndroid::terminate();
	NetSocketAndroid::terminate();

	cleanup_android_class_loader();
	cleanup_profiler();

	if (orblit_java) {
		orblit_java->on_orblit_terminating(env);
		if (!restart_on_cleanup) {
			if (p_restart) {
				orblit_java->restart(env);
			} else {
				orblit_java->force_quit(env);
			}
		}
		delete orblit_java;
	}
}

static String rendering_source_to_string(OS::RenderingSource p_source) {
	switch (p_source) {
		case OS::RENDERING_SOURCE_DEFAULT:
			return "Default";
		case OS::RENDERING_SOURCE_PROJECT_SETTING:
			return "ProjectSettings";
		case OS::RENDERING_SOURCE_COMMANDLINE:
			return "Commandline";
		case OS::RENDERING_SOURCE_FALLBACK:
			return "Fallback";
		default:
			return "Unknown";
	}
}

extern "C" {

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_setVirtualKeyboardHeight(JNIEnv *env, jclass clazz, jint p_height) {
	if (orblit_io_java) {
		orblit_io_java->set_vk_height(p_height);
	}
}

JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_initialize(JNIEnv *env, jclass clazz, jobject p_orblit_native_bridge, jobject p_asset_manager, jobject p_orblit_io, jobject p_net_utils, jobject p_directory_access_handler, jobject p_file_access_handler, jboolean p_use_apk_expansion) {
	init_profiler();

	JavaVM *jvm;
	env->GetJavaVM(&jvm);

	init_thread_jandroid(jvm, env);
	setup_android_class_loader();

	// create our wrapper classes
	orblit_java = new OrblitJavaWrapper(env, p_orblit_native_bridge);
	orblit_io_java = new OrblitIOJavaWrapper(env, p_orblit_io);

	FileAccessAndroid::setup(p_asset_manager);
	DirAccessJAndroid::setup(p_directory_access_handler);
	FileAccessFilesystemJAndroid::setup(p_file_access_handler);
	NetSocketAndroid::setup(p_net_utils);

	os_android = new OS_Android(orblit_java, orblit_io_java, p_use_apk_expansion);

	return true;
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_ondestroy(JNIEnv *env, jclass clazz) {
	_terminate(env, false);
}

JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_setup(JNIEnv *env, jclass clazz, jobjectArray p_cmdline, jobject p_orblit_tts) {
	setup_android_thread();

	const char **cmdline = nullptr;
	jstring *j_cmdline = nullptr;
	int cmdlen = 0;
	if (p_cmdline) {
		cmdlen = env->GetArrayLength(p_cmdline);
		if (cmdlen) {
			cmdline = (const char **)memalloc((cmdlen + 1) * sizeof(const char *));
			ERR_FAIL_NULL_V_MSG(cmdline, false, "Out of memory.");
			cmdline[cmdlen] = nullptr;
			j_cmdline = (jstring *)memalloc(cmdlen * sizeof(jstring));
			ERR_FAIL_NULL_V_MSG(j_cmdline, false, "Out of memory.");

			for (int i = 0; i < cmdlen; i++) {
				jstring string = (jstring)env->GetObjectArrayElement(p_cmdline, i);
				const char *rawString = env->GetStringUTFChars(string, nullptr);

				cmdline[i] = rawString;
				j_cmdline[i] = string;
			}
		}
	}

	Error err = Main::setup(OS_Android::ANDROID_EXEC_PATH, cmdlen, (char **)cmdline, false);
	if (cmdline) {
		if (j_cmdline) {
			for (int i = 0; i < cmdlen; ++i) {
				env->ReleaseStringUTFChars(j_cmdline[i], cmdline[i]);
			}
			memfree(j_cmdline);
		}
		memfree(cmdline);
	}

	// Note: --help and --version return ERR_HELP, but this should be translated to 0 if exit codes are propagated.
	if (err != OK) {
		return false;
	}

	Thread::release_main_thread(); // setup2 will be called from another thread.
	set_current_thread_safe_for_nodes(false);

	TTS_Android::setup(p_orblit_tts);

	java_class_wrapper = memnew(JavaClassWrapper);
	return true;
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_resize(JNIEnv *env, jclass clazz, jobject p_surface, jint p_width, jint p_height) {
	if (os_android) {
		os_android->set_display_size(Size2i(p_width, p_height));

		// No need to reset the surface during startup
		if (step.get() > STEP_SETUP) {
			if (p_surface) {
				ANativeWindow *native_window = ANativeWindow_fromSurface(env, p_surface);
				os_android->set_native_window(native_window);
			}

			DisplaySystemAndroid *dsa = DisplaySystemAndroid::get_singleton();
			ERR_FAIL_NULL(dsa);
			dsa->reset_window();
			dsa->notify_surface_changed(p_width, p_height);
		}
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_newcontext(JNIEnv *env, jclass clazz, jobject p_surface) {
	if (os_android) {
		if (step.get() == STEP_SETUP) {
			// During startup
			if (p_surface) {
				ANativeWindow *native_window = ANativeWindow_fromSurface(env, p_surface);
				os_android->set_native_window(native_window);
			}
		} else {
			// Rendering context recreated because it was lost; restart app to let it reload everything
			_terminate(env, true);
		}
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_back(JNIEnv *env, jclass clazz) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	if (DisplaySystemAndroid *dsa = Object::cast_to<DisplaySystemAndroid>(DisplaySystem::get_singleton())) {
		dsa->send_window_event(DisplaySystemEnums::WINDOW_EVENT_GO_BACK_REQUEST);
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_ttsCallback(JNIEnv *env, jclass clazz, jint event, jlong id, jint pos) {
	TTS_Android::_java_utterance_callback(event, id, pos);
}

JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_createAccessKitAdapter(JNIEnv *env, jclass clazz, jlong p_window_id) {
	AccessibilitySystem *ac = AccessibilitySystem::get_singleton();
	if (ac && ac->is_supported()) {
		return ac->window_create((DisplaySystemEnums::WindowID)p_window_id, nullptr);
	}
	return false;
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_freeAccessKitAdapter(JNIEnv *env, jclass clazz, jlong p_window_id) {
	AccessibilitySystem *ac = AccessibilitySystem::get_singleton();
	if (ac && ac->has_window((DisplaySystemEnums::WindowID)p_window_id)) {
		ac->window_destroy((DisplaySystemEnums::WindowID)p_window_id);
	}
}

JNIEXPORT jobject JNICALL Java_org_orblitengine_orblit_OrblitLib_createAccessibilityNodeInfo(JNIEnv *env, jclass clazz, jlong p_window_id, jobject p_host, jint p_virtual_view_id) {
	AccessibilitySystem *ac = AccessibilitySystem::get_singleton();
	if (ac) {
		return (jobject)ac->native_create_node_info((DisplaySystemEnums::WindowID)p_window_id, (void *)p_host, p_virtual_view_id);
	}
	return nullptr;
}

JNIEXPORT jobject JNICALL Java_org_orblitengine_orblit_OrblitLib_findAccessibilityFocus(JNIEnv *env, jclass clazz, jlong p_window_id, jobject p_host, jint p_focus_type) {
	AccessibilitySystem *ac = AccessibilitySystem::get_singleton();
	if (ac) {
		return (jobject)ac->native_find_focus((DisplaySystemEnums::WindowID)p_window_id, (void *)p_host, p_focus_type);
	}
	return nullptr;
}

JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_performAccessibilityAction(JNIEnv *env, jclass clazz, jlong p_window_id, jobject p_host, jint p_virtual_view_id, jint p_action, jobject p_arguments) {
	AccessibilitySystem *ac = AccessibilitySystem::get_singleton();
	if (ac) {
		return ac->native_perform_action((DisplaySystemEnums::WindowID)p_window_id, (void *)p_host, p_virtual_view_id, p_action, (void *)p_arguments);
	}
	return false;
}

JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_onAccessibilityHoverEvent(JNIEnv *env, jclass clazz, jlong p_window_id, jobject p_host, jint p_action, jfloat p_x, jfloat p_y) {
	AccessibilitySystem *ac = AccessibilitySystem::get_singleton();
	if (ac) {
		return ac->native_on_hover((DisplaySystemEnums::WindowID)p_window_id, (void *)p_host, p_action, p_x, p_y);
	}
	return false;
}

JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_step(JNIEnv *env, jclass clazz) {
	if (step.get() == STEP_TERMINATED) {
		return true;
	}

	if (step.get() == STEP_SETUP) {
		// Since Orblit is initialized on the UI thread, main_thread_id was set to that thread's id,
		// but for Orblit purposes, the main thread is the one running the game loop.
		// The logo is shown in the next frame otherwise we run into rendering issues.
		if (Main::setup2(false) != OK) {
			ERR_PRINT("Unable to complete engine setup!");
			OS::get_singleton()->alert("Unable to complete engine setup!");
			_terminate(env, false);
			return false;
		}
		input_handler = new AndroidInputHandler();
		step.increment();
		return true;
	}

	if (step.get() == STEP_SHOW_LOGO) {
		bool xr_enabled = false;
#ifndef XR_DISABLED
		// Unlike PCVR, there's no additional 2D screen onto which to render the boot logo,
		// so we skip this step if xr is enabled.
		if (XRSystem::get_xr_mode() == XRSystem::XRMODE_DEFAULT) {
			xr_enabled = GLOBAL_GET_CACHED(bool, "xr/shaders/enabled");
		} else {
			xr_enabled = XRSystem::get_xr_mode() == XRSystem::XRMODE_ON;
		}
#endif // XR_DISABLED
		if (!xr_enabled) {
			Main::setup_boot_logo();
		}

		step.increment();
		return true;
	}

	if (step.get() == STEP_STARTED) {
		if (Main::start() != EXIT_SUCCESS) {
			ERR_PRINT("Unable to start engine!");
			OS::get_singleton()->alert("Unable to start engine!");
			_terminate(env, false);
			return false;
		}

		orblit_java->on_orblit_setup_completed(env);
		os_android->main_loop_begin();
		orblit_java->on_orblit_main_loop_started(env);
		step.increment();
	}

	DisplaySystemAndroid *dsa = DisplaySystemAndroid::get_singleton();
	if (dsa) {
		dsa->process_accelerometer(accelerometer);
		dsa->process_gravity(gravity);
		dsa->process_magnetometer(magnetometer);
		dsa->process_gyroscope(gyroscope);
		dsa->process_device_orientation(device_orientation);
	}

	bool should_swap_buffers = false;
	if (os_android->main_loop_iterate(&should_swap_buffers)) {
		_terminate(env, false);
	}

	return should_swap_buffers;
}

// Called on the UI thread
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_dispatchMouseEvent(JNIEnv *env, jclass clazz, jint p_event_type, jint p_button_mask, jfloat p_x, jfloat p_y, jfloat p_delta_x, jfloat p_delta_y, jboolean p_double_click, jboolean p_source_mouse_relative, jfloat p_pressure, jfloat p_tilt_x, jfloat p_tilt_y, jboolean p_emulated) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	input_handler->process_mouse_event(p_event_type, p_button_mask, Point2(p_x, p_y), Vector2(p_delta_x, p_delta_y), p_double_click, p_source_mouse_relative, p_pressure, Vector2(p_tilt_x, p_tilt_y), p_emulated);
}

// Called on the UI thread
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_dispatchTouchEvent(JNIEnv *env, jclass clazz, jint ev, jint pointer, jint pointer_count, jfloatArray position, jboolean p_double_tap, jboolean p_long_press) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	Vector<AndroidInputHandler::TouchPos> points;
	for (int i = 0; i < pointer_count; i++) {
		jfloat p[6];
		env->GetFloatArrayRegion(position, i * 6, 6, p);
		AndroidInputHandler::TouchPos tp;
		tp.id = (int)p[0];
		tp.pos = Point2(p[1], p[2]);
		tp.pressure = p[3];
		tp.tilt = Vector2(p[4], p[5]);
		tp.double_tap = p_double_tap;
		tp.long_press = p_long_press;
		points.push_back(tp);
	}

	input_handler->process_touch_event(ev, pointer, points);
}

// Called on the UI thread
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_magnify(JNIEnv *env, jclass clazz, jfloat p_x, jfloat p_y, jfloat p_factor) {
	if (step.get() <= STEP_SETUP) {
		return;
	}
	input_handler->process_magnify(Point2(p_x, p_y), p_factor);
}

// Called on the UI thread
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_pan(JNIEnv *env, jclass clazz, jfloat p_x, jfloat p_y, jfloat p_delta_x, jfloat p_delta_y) {
	if (step.get() <= STEP_SETUP) {
		return;
	}
	input_handler->process_pan(Point2(p_x, p_y), Vector2(p_delta_x, p_delta_y));
}

// Called on the UI thread
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_joybutton(JNIEnv *env, jclass clazz, jint p_device, jint p_button, jboolean p_pressed) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	AndroidInputHandler::JoypadEvent jevent;
	jevent.device = p_device;
	jevent.type = AndroidInputHandler::JOY_EVENT_BUTTON;
	jevent.index = p_button;
	jevent.pressed = p_pressed;

	input_handler->process_joy_event(jevent);
}

// Called on the UI thread
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_joyaxis(JNIEnv *env, jclass clazz, jint p_device, jint p_axis, jfloat p_value) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	AndroidInputHandler::JoypadEvent jevent;
	jevent.device = p_device;
	jevent.type = AndroidInputHandler::JOY_EVENT_AXIS;
	jevent.index = p_axis;
	jevent.value = p_value;

	input_handler->process_joy_event(jevent);
}

// Called on the UI thread
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_joyhat(JNIEnv *env, jclass clazz, jint p_device, jint p_hat_x, jint p_hat_y) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	AndroidInputHandler::JoypadEvent jevent;
	jevent.device = p_device;
	jevent.type = AndroidInputHandler::JOY_EVENT_HAT;
	BitField<HatMask> hat = HatMask::CENTER;
	if (p_hat_x != 0) {
		if (p_hat_x < 0) {
			hat.set_flag(HatMask::LEFT);
		} else {
			hat.set_flag(HatMask::RIGHT);
		}
	}
	if (p_hat_y != 0) {
		if (p_hat_y < 0) {
			hat.set_flag(HatMask::UP);
		} else {
			hat.set_flag(HatMask::DOWN);
		}
	}
	jevent.hat = hat;

	input_handler->process_joy_event(jevent);
}

// Called on the UI thread
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_joyconnectionchanged(JNIEnv *env, jclass clazz, jint p_device, jboolean p_connected, jstring p_name) {
	if (os_android) {
		String name = jstring_to_string(p_name, env);
		Input *input = Input::get_singleton();
		if (input) {
			input->joy_connection_changed(p_device, p_connected, name);
		}
	}
}

// Called on the UI thread
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_key(JNIEnv *env, jclass clazz, jint p_physical_keycode, jint p_unicode, jint p_key_label, jboolean p_pressed, jboolean p_echo) {
	if (step.get() <= STEP_SETUP) {
		return;
	}
	input_handler->process_key_event(p_physical_keycode, p_unicode, p_key_label, p_pressed, p_echo);
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_accelerometer(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z) {
	accelerometer = Vector3(x, y, z);
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_gravity(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z) {
	gravity = Vector3(x, y, z);
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_magnetometer(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z) {
	magnetometer = Vector3(x, y, z);
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_gyroscope(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z) {
	gyroscope = Vector3(x, y, z);
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_deviceOrientation(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z, jfloat w) {
	device_orientation = Quaternion(x, y, z, w);
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_focusin(JNIEnv *env, jclass clazz) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	os_android->main_loop_focusin();
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_focusout(JNIEnv *env, jclass clazz) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	os_android->main_loop_focusout();
}

JNIEXPORT jstring JNICALL Java_org_orblitengine_orblit_OrblitLib_getGlobal(JNIEnv *env, jclass clazz, jstring path) {
	String js = jstring_to_string(path, env);

	Variant setting_with_override = GLOBAL_GET(js);
	String setting_value = (setting_with_override.get_type() == Variant::NIL) ? "" : setting_with_override;
	return env->NewStringUTF(setting_value.utf8().get_data());
}

JNIEXPORT jobjectArray JNICALL Java_org_orblitengine_orblit_OrblitLib_getRendererInfo(JNIEnv *env, jclass clazz, jboolean p_vulkan_requirements_met) {
	String rendering_driver_original = RenderingSystem::get_singleton()->get_current_rendering_driver_name();
	String rendering_driver_chosen = rendering_driver_original;
	String rendering_method = RenderingSystem::get_singleton()->get_current_rendering_method();

#ifdef VULKAN_ENABLED
	if (rendering_driver_original == "vulkan" && !DisplaySystemAndroid::check_vulkan_global_context(p_vulkan_requirements_met)) {
		// The Android display server only gets created after this step, so a static check must be used to check for Vulkan
		// availability instead. If the check fails, it'll fall back to OpenGL3 accordingly if the relevant project setting
		// is enabled. The Vulkan context created by this check will be reused by the DisplaySystem afterwards.
		rendering_driver_chosen = RenderingSystem::get_singleton()->get_current_rendering_driver_name();
		rendering_method = RenderingSystem::get_singleton()->get_current_rendering_method();
	}
#ifndef XR_DISABLED
	// When running in XR mode, vulkan initialization must be done by the XR module, so we ensure that the vulkan
	// global context is reset.
	// Note: This is temporary workaround.
	// A proper fix involves updating the Android init flow so that DisplaySystemAndroid can update the Android surface
	// type (vulkan or opengl) after it's initialized.
	bool xr_enabled = false;
	if (XRSystem::get_xr_mode() == XRSystem::XRMODE_DEFAULT) {
		xr_enabled = GLOBAL_GET_CACHED(bool, "xr/shaders/enabled");
	} else {
		xr_enabled = XRSystem::get_xr_mode() == XRSystem::XRMODE_ON;
	}
	if (xr_enabled) {
		DisplaySystemAndroid::free_vulkan_global_context();
	}
#endif // XR_DISABLED
#endif

	String rendering_driver_source = rendering_source_to_string(OS::get_singleton()->get_current_rendering_driver_name_source());
	String rendering_method_source = rendering_source_to_string(OS::get_singleton()->get_current_rendering_method_source());

	jobjectArray result = env->NewObjectArray(5, jni_find_class(env, "java/lang/String"), nullptr);
	env->SetObjectArrayElement(result, 0, env->NewStringUTF(rendering_driver_chosen.utf8().get_data()));
	env->SetObjectArrayElement(result, 1, env->NewStringUTF(rendering_driver_original.utf8().get_data()));
	env->SetObjectArrayElement(result, 2, env->NewStringUTF(rendering_method.utf8().get_data()));
	env->SetObjectArrayElement(result, 3, env->NewStringUTF(rendering_driver_source.utf8().get_data()));
	env->SetObjectArrayElement(result, 4, env->NewStringUTF(rendering_method_source.utf8().get_data()));

	return result;
}

JNIEXPORT jstring JNICALL Java_org_orblitengine_orblit_OrblitLib_getEditorSetting(JNIEnv *env, jclass clazz, jstring p_setting_key) {
	String editor_setting_value = "";
#ifdef TOOLS_ENABLED
	if (EditorSettings::get_singleton() != nullptr) {
		String orblit_setting_key = jstring_to_string(p_setting_key, env);
		Variant editor_setting = EDITOR_GET(orblit_setting_key);
		editor_setting_value = (editor_setting.get_type() == Variant::NIL) ? "" : editor_setting;
	}
#else
	WARN_PRINT("Access to the Editor Settings in only available on Editor builds");
#endif

	return env->NewStringUTF(editor_setting_value.utf8().get_data());
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_setEditorSetting(JNIEnv *env, jclass clazz, jstring p_key, jobject p_data) {
#ifdef TOOLS_ENABLED
	if (EditorSettings::get_singleton() != nullptr) {
		String key = jstring_to_string(p_key, env);
		Variant data = _jobject_to_variant(env, p_data);
		EditorSettings::get_singleton()->set(key, data);
	}
#else
	WARN_PRINT("Access to the Editor Settings in only available on Editor builds");
#endif
}

JNIEXPORT jobject JNICALL Java_org_orblitengine_orblit_OrblitLib_getEditorProjectMetadata(JNIEnv *env, jclass clazz, jstring p_section, jstring p_key, jobject p_default_value) {
	jvalue result;

#ifdef TOOLS_ENABLED
	if (EditorSettings::get_singleton() != nullptr) {
		String section = jstring_to_string(p_section, env);
		String key = jstring_to_string(p_key, env);
		Variant default_value = _jobject_to_variant(env, p_default_value);
		Variant data = EditorSettings::get_singleton()->get_project_metadata(section, key, default_value);
		result.l = _variant_to_jobject(env, data.get_type(), &data);
	}
#else
	WARN_PRINT("Access to the Editor Settings Project Metadata is only available on Editor builds");
#endif

	return result.l;
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_setEditorProjectMetadata(JNIEnv *env, jclass clazz, jstring p_section, jstring p_key, jobject p_data) {
#ifdef TOOLS_ENABLED
	if (EditorSettings::get_singleton() != nullptr) {
		String section = jstring_to_string(p_section, env);
		String key = jstring_to_string(p_key, env);
		Variant data = _jobject_to_variant(env, p_data);
		EditorSettings::get_singleton()->set_project_metadata(section, key, data);
	}
#else
	WARN_PRINT("Access to the Editor Settings Project Metadata is only available on Editor builds");
#endif
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onNightModeChanged(JNIEnv *env, jclass clazz) {
	DisplaySystemAndroid *ds = (DisplaySystemAndroid *)DisplaySystem::get_singleton();
	if (ds) {
		ds->emit_system_theme_changed();
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_hardwareKeyboardConnected(JNIEnv *env, jclass clazz, jboolean p_connected) {
	DisplaySystemAndroid *ds = (DisplaySystemAndroid *)DisplaySystem::get_singleton();
	if (ds) {
		ds->emit_hardware_keyboard_connection_changed(p_connected);
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_filePickerCallback(JNIEnv *env, jclass clazz, jboolean p_ok, jobjectArray p_selected_paths) {
	DisplaySystemAndroid *ds = (DisplaySystemAndroid *)DisplaySystem::get_singleton();
	if (ds) {
		Vector<String> selected_paths;

		jint length = env->GetArrayLength(p_selected_paths);
		for (jint i = 0; i < length; ++i) {
			jstring java_string = (jstring)env->GetObjectArrayElement(p_selected_paths, i);
			String path = jstring_to_string(java_string, env);
			selected_paths.push_back(path);
			env->DeleteLocalRef(java_string);
		}
		ds->emit_file_picker_callback(p_ok, selected_paths);
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_requestPermissionResult(JNIEnv *env, jclass clazz, jstring p_permission, jboolean p_result) {
	String permission = jstring_to_string(p_permission, env);
	if (permission == "android.permission.RECORD_AUDIO" && p_result) {
		AudioDriver::get_singleton()->input_start();
	}

	if (os_android->get_main_loop()) {
		os_android->get_main_loop()->emit_signal(SNAME("on_request_permissions_result"), permission, p_result == JNI_TRUE);
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onRendererResumed(JNIEnv *env, jclass clazz) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	// We force redraw to ensure we render at least once when resuming the app.
	Main::force_redraw();
	CameraSystem *camera_system = CameraSystem::get_singleton();
	if (camera_system) {
		camera_system->handle_application_resume();
	}
	if (os_android->get_main_loop()) {
		os_android->get_main_loop()->notification(MainLoop::NOTIFICATION_APPLICATION_RESUMED);
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onRendererPaused(JNIEnv *env, jclass clazz) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	CameraSystem *camera_system = CameraSystem::get_singleton();
	if (camera_system) {
		camera_system->handle_application_pause();
	}

	if (os_android->get_main_loop()) {
		os_android->get_main_loop()->notification(MainLoop::NOTIFICATION_APPLICATION_PAUSED);
	}

	if (DisplaySystemAndroid *dsa = Object::cast_to<DisplaySystemAndroid>(DisplaySystem::get_singleton())) {
		dsa->notify_application_paused();
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onOrientationChange(JNIEnv *env, jclass clazz, jint p_orientation) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	CameraSystem *camera_system = CameraSystem::get_singleton();
	if (camera_system) {
		camera_system->handle_display_rotation_change(p_orientation);
	}

	DisplaySystem *display_system = DisplaySystem::get_singleton();
	if (display_system) {
		display_system->emit_signal("orientation_changed", p_orientation);
	}
}

JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_shouldDispatchInputToRenderThread(JNIEnv *env, jclass clazz) {
	Input *input = Input::get_singleton();
	if (input) {
		return !input->is_agile_input_event_flushing();
	}
	return false;
}

JNIEXPORT jstring JNICALL Java_org_orblitengine_orblit_OrblitLib_getProjectResourceDir(JNIEnv *env, jclass clazz) {
	const String resource_dir = OS::get_singleton()->get_resource_dir();
	return env->NewStringUTF(resource_dir.utf8().get_data());
}
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_isEditorHint(JNIEnv *env, jclass clazz) {
	Engine *engine = Engine::get_singleton();
	if (engine) {
		return engine->is_editor_hint();
	}
	return false;
}

JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_isProjectManagerHint(JNIEnv *env, jclass clazz) {
	Engine *engine = Engine::get_singleton();
	if (engine) {
		return engine->is_project_manager_hint();
	}
	return false;
}

JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_hasFeature(JNIEnv *env, jclass clazz, jstring p_feature) {
	OS *os = OS::get_singleton();
	if (os) {
		String feature = jstring_to_string(p_feature, env);
		return os->has_feature(feature);
	}
	return false;
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onPictureInPictureModeChanged(JNIEnv *env, jclass clazz, jboolean p_is_in_picture_in_picture_mode) {
	if (step.get() <= STEP_SETUP) {
		return;
	}

	if (os_android->get_main_loop()) {
		if (p_is_in_picture_in_picture_mode) {
			os_android->get_main_loop()->notification(MainLoop::NOTIFICATION_APPLICATION_PIP_MODE_ENTERED);
		} else {
			os_android->get_main_loop()->notification(MainLoop::NOTIFICATION_APPLICATION_PIP_MODE_EXITED);
		}
	}
}
}
