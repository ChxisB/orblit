/**************************************************************************/
/*  shared_object.h                                                       */

#pragma once

#include "core/containers/vector.h"
#include "core/string/ustring.h"

struct SharedObject {
	String path;
	Vector<String> tags;
	String target;

	SharedObject(const String &p_path, const Vector<String> &p_tags, const String &p_target) :
			path(p_path),
			tags(p_tags),
			target(p_target) {
	}

	SharedObject() {}
};
