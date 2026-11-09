/**************************************************************************/
/*  java_io_wrapper.h                                                     */

#pragma once

#include "core/math/rect2i.h"
#include "core/variant/typed_array.h"

#include <android/log.h>
#include <jni.h>

// Class that makes functions in java/src/org/orblitengine/orblit/OrblitIO.java callable from C++
class OrblitIOJavaWrapper {
private:
	jobject orblit_io_instance;
	jclass cls;

	jmethodID _open_URI = nullptr;
	jmethodID _get_cache_dir = nullptr;
	jmethodID _get_data_dir = nullptr;
	jmethodID _get_temp_dir = nullptr;
	jmethodID _get_display_cutouts = nullptr;
	jmethodID _get_display_rotation = nullptr;
	jmethodID _get_display_safe_area = nullptr;
	jmethodID _get_locale = nullptr;
	jmethodID _get_model = nullptr;
	jmethodID _get_screen_DPI = nullptr;
	jmethodID _get_scaled_density = nullptr;
	jmethodID _get_screen_refresh_rate = nullptr;
	jmethodID _get_unique_id = nullptr;
	jmethodID _show_keyboard = nullptr;
	jmethodID _hide_keyboard = nullptr;
	jmethodID _has_hardware_keyboard = nullptr;
	jmethodID _set_screen_orientation = nullptr;
	jmethodID _get_screen_orientation = nullptr;
	jmethodID _get_system_dir = nullptr;

public:
	OrblitIOJavaWrapper(JNIEnv *p_env, jobject p_orblit_io_instance);
	~OrblitIOJavaWrapper();

	jobject get_instance();

	Error open_uri(const String &p_uri);
	String get_cache_dir();
	String get_temp_dir();
	String get_user_data_dir(const String &p_user_dir);
	String get_locale();
	String get_model();
	int get_screen_dpi();
	float get_scaled_density();
	float get_screen_refresh_rate(float fallback);
	TypedArray<Rect2> get_display_cutouts();
	Rect2i get_display_safe_area();
	String get_unique_id();
	bool has_vk();
	bool has_hardware_keyboard();
	void show_vk(const String &p_existing, int p_type, int p_max_input_length, int p_cursor_start, int p_cursor_end);
	void hide_vk();
	int get_vk_height();
	void set_vk_height(int p_height);
	void set_screen_orientation(int p_orient);
	int get_screen_orientation();
	String get_system_dir(int p_dir, bool p_shared_storage);
	int get_display_rotation();
};
