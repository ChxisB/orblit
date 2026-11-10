/**************************************************************************/
/*  java_lib_jni.h                                                        */

#pragma once

#include <android/log.h>
#include <jni.h>

// These functions can be called from within JAVA and are the means by which our JAVA implementation calls back into our C++ code.
// See java/src/org/orblitengine/orblit/OrblitLib.java for the JAVA side of this (yes that's why we have the long names)
extern "C" {
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_initialize(JNIEnv *env, jclass clazz, jobject p_orblit_native_bridge, jobject p_asset_manager, jobject p_orblit_io, jobject p_net_utils, jobject p_directory_access_handler, jobject p_file_access_handler, jboolean p_use_apk_expansion);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_ondestroy(JNIEnv *env, jclass clazz);
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_setup(JNIEnv *env, jclass clazz, jobjectArray p_cmdline, jobject p_orblit_tts);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_resize(JNIEnv *env, jclass clazz, jobject p_surface, jint p_width, jint p_height);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_newcontext(JNIEnv *env, jclass clazz, jobject p_surface);
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_step(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_ttsCallback(JNIEnv *env, jclass clazz, jint event, jlong id, jint pos);
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_createAccessKitAdapter(JNIEnv *env, jclass clazz, jlong p_window_id);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_freeAccessKitAdapter(JNIEnv *env, jclass clazz, jlong p_window_id);
JNIEXPORT jobject JNICALL Java_org_orblitengine_orblit_OrblitLib_createAccessibilityNodeInfo(JNIEnv *env, jclass clazz, jlong p_window_id, jobject p_host, jint p_virtual_view_id);
JNIEXPORT jobject JNICALL Java_org_orblitengine_orblit_OrblitLib_findAccessibilityFocus(JNIEnv *env, jclass clazz, jlong p_window_id, jobject p_host, jint p_focus_type);
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_performAccessibilityAction(JNIEnv *env, jclass clazz, jlong p_window_id, jobject p_host, jint p_virtual_view_id, jint p_action, jobject p_arguments);
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_onAccessibilityHoverEvent(JNIEnv *env, jclass clazz, jlong p_window_id, jobject p_host, jint p_action, jfloat p_x, jfloat p_y);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_back(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_dispatchMouseEvent(JNIEnv *env, jclass clazz, jint p_event_type, jint p_button_mask, jfloat p_x, jfloat p_y, jfloat p_delta_x, jfloat p_delta_y, jboolean p_double_click, jboolean p_source_mouse_relative, jfloat p_pressure, jfloat p_tilt_x, jfloat p_tilt_y, jboolean p_emulated);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_dispatchTouchEvent(JNIEnv *env, jclass clazz, jint ev, jint pointer, jint pointer_count, jfloatArray positions, jboolean p_double_tap, jboolean p_long_press);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_magnify(JNIEnv *env, jclass clazz, jfloat p_x, jfloat p_y, jfloat p_factor);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_pan(JNIEnv *env, jclass clazz, jfloat p_x, jfloat p_y, jfloat p_delta_x, jfloat p_delta_y);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_key(JNIEnv *env, jclass clazz, jint p_physical_keycode, jint p_unicode, jint p_key_label, jboolean p_pressed, jboolean p_echo);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_joybutton(JNIEnv *env, jclass clazz, jint p_device, jint p_button, jboolean p_pressed);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_joyaxis(JNIEnv *env, jclass clazz, jint p_device, jint p_axis, jfloat p_value);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_joyhat(JNIEnv *env, jclass clazz, jint p_device, jint p_hat_x, jint p_hat_y);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_joyconnectionchanged(JNIEnv *env, jclass clazz, jint p_device, jboolean p_connected, jstring p_name);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_accelerometer(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_gravity(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_magnetometer(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_gyroscope(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_deviceOrientation(JNIEnv *env, jclass clazz, jfloat x, jfloat y, jfloat z, jfloat w);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_focusin(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_focusout(JNIEnv *env, jclass clazz);
JNIEXPORT jstring JNICALL Java_org_orblitengine_orblit_OrblitLib_getGlobal(JNIEnv *env, jclass clazz, jstring path);
JNIEXPORT jobjectArray JNICALL Java_org_orblitengine_orblit_OrblitLib_getRendererInfo(JNIEnv *env, jclass clazz, jboolean p_vulkan_requirements_met);
JNIEXPORT jstring JNICALL Java_org_orblitengine_orblit_OrblitLib_getEditorSetting(JNIEnv *env, jclass clazz, jstring p_setting_key);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_setEditorSetting(JNIEnv *env, jclass clazz, jstring p_key, jobject p_data);
JNIEXPORT jobject JNICALL Java_org_orblitengine_orblit_OrblitLib_getEditorProjectMetadata(JNIEnv *env, jclass clazz, jstring p_section, jstring p_key, jobject p_default_value);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_setEditorProjectMetadata(JNIEnv *env, jclass clazz, jstring p_section, jstring p_key, jobject p_data);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_setVirtualKeyboardHeight(JNIEnv *env, jclass clazz, jint p_height);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_requestPermissionResult(JNIEnv *env, jclass clazz, jstring p_permission, jboolean p_result);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onNightModeChanged(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_hardwareKeyboardConnected(JNIEnv *env, jclass clazz, jboolean p_connected);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_filePickerCallback(JNIEnv *env, jclass clazz, jboolean p_ok, jobjectArray p_selected_paths);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onRendererResumed(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onRendererPaused(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onOrientationChange(JNIEnv *env, jclass clazz, jint p_orientation);
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_shouldDispatchInputToRenderThread(JNIEnv *env, jclass clazz);
JNIEXPORT jstring JNICALL Java_org_orblitengine_orblit_OrblitLib_getProjectResourceDir(JNIEnv *env, jclass clazz);
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_isEditorHint(JNIEnv *env, jclass clazz);
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_isProjectManagerHint(JNIEnv *env, jclass clazz);
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_OrblitLib_hasFeature(JNIEnv *env, jclass clazz, jstring p_feature);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_OrblitLib_onPictureInPictureModeChanged(JNIEnv *env, jclass clazz, jboolean p_is_in_picture_in_picture_mode);
}
