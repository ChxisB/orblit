/**************************************************************************/
/*  game_menu_utils_jni.h                                                 */

#pragma once

#include <jni.h>

extern "C" {
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setSuspend(JNIEnv *env, jclass clazz, jboolean enabled);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_nextFrame(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setNodeType(JNIEnv *env, jclass clazz, jint type);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setSelectMode(JNIEnv *env, jclass clazz, jint mode);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setSelectionVisible(JNIEnv *env, jclass clazz, jboolean visible);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setSelectionAvoidLocked(JNIEnv *env, jclass clazz, jboolean enabled);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setSelectionPreferGroup(JNIEnv *env, jclass clazz, jboolean enabled);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setCameraOverride(JNIEnv *env, jclass clazz, jboolean enabled);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setCameraManipulateMode(JNIEnv *env, jclass clazz, jint mode);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_resetCamera2DPosition(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_resetCamera3DPosition(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_playMainScene(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setDebugMuteAudio(JNIEnv *env, jclass clazz, jboolean enabled);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_resetTimeScale(JNIEnv *env, jclass clazz);
JNIEXPORT void JNICALL Java_org_orblitengine_orblit_editor_utils_GameMenuUtils_setTimeScale(JNIEnv *env, jclass clazz, jdouble scale);
}
