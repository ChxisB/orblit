/**************************************************************************/
/*  callable_jni.h                                                        */

#pragma once

#include <jni.h>

extern "C" {
JNIEXPORT jobject JNICALL Java_org_orblitengine_orblit_variant_Callable_nativeCall(JNIEnv *p_env, jclass p_clazz, jlong p_native_callable, jobjectArray p_parameters);
JNIEXPORT jobject JNICALL Java_org_orblitengine_orblit_variant_Callable_nativeCallObject(JNIEnv *p_env, jclass p_clazz, jlong p_object_id, jstring p_method_name, jobjectArray p_parameters);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_variant_Callable_nativeCallObjectDeferred(JNIEnv *p_env, jclass p_clazz, jlong p_object_id, jstring p_method_name, jobjectArray p_parameters);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_variant_Callable_releaseNativePointer(JNIEnv *p_env, jclass clazz, jlong p_native_pointer);
}
