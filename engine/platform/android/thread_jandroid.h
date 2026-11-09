/**************************************************************************/
/*  thread_jandroid.h                                                     */

#pragma once

#include <jni.h>

void init_thread_jandroid(JavaVM *p_jvm, JNIEnv *p_env);

void setup_android_thread();
JNIEnv *get_jni_env();
