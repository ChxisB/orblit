/**************************************************************************/
/*  java_view_wrapper.h                                                   */

#pragma once

#include "core/math/vector2.h"

#include <android/log.h>
#include <jni.h>

// Class that makes functions in java/src/org/orblitengine/orblit/OrblitRenderView.java callable from C++
class OrblitJavaViewWrapper {
private:
	jclass _cls;

	jobject _orblit_view;

	jmethodID _can_capture_pointer = nullptr;
	jmethodID _request_pointer_capture = nullptr;
	jmethodID _release_pointer_capture = nullptr;

	jmethodID _configure_pointer_icon = nullptr;
	jmethodID _set_pointer_icon = nullptr;

public:
	OrblitJavaViewWrapper(jobject orblit_view);

	bool can_update_pointer_icon() const;
	bool can_capture_pointer() const;

	void request_pointer_capture();
	void release_pointer_capture();

	void configure_pointer_icon(int pointer_type, const String &image_path, const Vector2 &p_hotspot);
	void set_pointer_icon(int pointer_type);

	~OrblitJavaViewWrapper();
};
