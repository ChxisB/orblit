/**************************************************************************/
/*  dialog_utils_jni.cpp                                                  */

#include "dialog_utils_jni.h"

#include "display_system_android.h"
#include "jni_utils.h"

extern "C" {

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_utils_DialogUtils_dialogCallback(JNIEnv *env, jclass clazz, jint p_button_index) {
	DisplaySystemAndroid *ds = (DisplaySystemAndroid *)DisplaySystem::get_singleton();
	if (ds) {
		ds->emit_dialog_callback(p_button_index);
	}
}

JNIEXPORT void JNICALL Java_org_orblitengine_orblit_utils_DialogUtils_inputDialogCallback(JNIEnv *env, jclass clazz, jstring p_text) {
	DisplaySystemAndroid *ds = (DisplaySystemAndroid *)DisplaySystem::get_singleton();
	if (ds) {
		String text = jstring_to_string(p_text, env);
		ds->emit_input_dialog_callback(text);
	}
}
}
