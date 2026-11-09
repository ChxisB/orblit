/**************************************************************************/
/*  editor_utils_jni.h                                                    */

#pragma once

#include <jni.h>

extern "C" {
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_EditorUtils_runScene(JNIEnv *p_env, jclass, jstring p_scene, jobjectArray p_scene_args);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_EditorUtils_orientationChanged(JNIEnv *p_env, jclass, jboolean p_portrait);
}
