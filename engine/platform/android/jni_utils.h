/**************************************************************************/
/*  jni_utils.h                                                           */

#pragma once

#include "thread_jandroid.h"

#include "core/string/ustring.h"
#include "core/variant/variant.h"

#include <jni.h>

jobject _variant_to_jobject(JNIEnv *env, Variant::Type p_type, const Variant *p_arg, int p_depth = 0);

String _get_class_name(JNIEnv *env, jclass cls, bool *array);

Variant _jobject_to_variant(JNIEnv *env, jobject obj, int p_depth = 0);

Variant::Type get_jni_type(const String &p_type);

/**
 * Convert a Orblit Callable to a org.orblitengine.orblit.variant.Callable java object.
 * @param p_env JNI environment instance
 * @param p_callable Callable parameter to convert. If null or invalid type, a null jobject is returned.
 * @return org.orblitengine.orblit.variant.Callable jobject or null
 */
jobject callable_to_jcallable(JNIEnv *p_env, const Variant &p_callable);

/**
 * Convert a org.orblitengine.orblit.variant.Callable java object to a Orblit Callable variant.
 * @param p_env JNI environment instance
 * @param p_jcallable_obj org.orblitengine.orblit.variant.Callable java object to convert.
 * @return Callable variant
 */
Callable jcallable_to_callable(JNIEnv *p_env, jobject p_jcallable_obj);

/**
 * Converts a java.lang.CharSequence object to a Orblit String.
 * @param p_env  JNI environment instance
 * @param p_charsequence java.lang.CharSequence object to convert
 * @return Orblit String instance.
 */
String charsequence_to_string(JNIEnv *p_env, jobject p_charsequence);

/**
 * Converts JNI jstring to Orblit String.
 * @param source Source JNI string. If null an empty string is returned.
 * @param env JNI environment instance. If null obtained by get_jni_env().
 * @return Orblit string instance.
 */
static inline String jstring_to_string(jstring source, JNIEnv *env = nullptr) {
	String result;
	if (source) {
		if (!env) {
			env = get_jni_env();
		}
		const char *const source_utf8 = env->GetStringUTFChars(source, nullptr);
		if (source_utf8) {
			result.append_utf8(source_utf8);
			env->ReleaseStringUTFChars(source, source_utf8);
		}
	}
	return result;
}

/**
 * Set up thread-safe Android ClassLoader (used by jni_find_class() below).
 */
void setup_android_class_loader();
void cleanup_android_class_loader();

/**
 * Thread-safe JNI class finder using Android ClassLoader.
 * Works on any thread, unlike standard FindClass which may fail on non-main threads.
 * @param p_env JNI environment instance.
 * @param p_class_name Class name to find.
 * @return jclass reference or null if not found.
 */
jclass jni_find_class(JNIEnv *p_env, const char *p_class_name);
