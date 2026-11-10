/**************************************************************************/
/*  dialog_utils_jni.h                                                    */

#pragma once

#include <jni.h>

extern "C" {
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_utils_DialogUtils_dialogCallback(JNIEnv *env, jclass clazz, jint p_button_index);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_utils_DialogUtils_inputDialogCallback(JNIEnv *env, jclass clazz, jstring p_text);
}
