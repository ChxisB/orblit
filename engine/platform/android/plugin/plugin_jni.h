/**************************************************************************/
/*  plugin_jni.h                                                          */

#pragma once

#include <android/log.h>
#include <jni.h>

void unregister_plugins_singletons();

extern "C" {
JNIEXPORT jboolean JNICALL Java_org_orblitengine_orblit_plugin_OrblitPlugin_nativeRegisterSingleton(JNIEnv *env, jclass clazz, jstring name, jobject obj);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_plugin_OrblitPlugin_nativeRegisterMethod(JNIEnv *env, jclass clazz, jstring sname, jstring name, jstring ret, jobjectArray args);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_plugin_OrblitPlugin_nativeRegisterSignal(JNIEnv *env, jclass clazz, jstring j_plugin_name, jstring j_signal_name, jobjectArray j_signal_param_types);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_plugin_OrblitPlugin_nativeEmitSignal(JNIEnv *env, jclass clazz, jstring j_plugin_name, jstring j_signal_name, jobjectArray j_signal_params);
}
