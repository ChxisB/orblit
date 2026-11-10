/**************************************************************************/
/*  editor_string_names.h                                                 */

#pragma once

#include "core/string/string_name.h"

class EditorStringNames {
	inline static EditorStringNames *singleton = nullptr;

public:
	static void create() { singleton = memnew(EditorStringNames); }
	static void free() {
		memdelete(singleton);
		singleton = nullptr;
	}

	_FORCE_INLINE_ static EditorStringNames *get_singleton() { return singleton; }

	const StringName Editor = StringName("Editor");
	const StringName EditorFonts = StringName("EditorFonts");
	const StringName EditorIcons = StringName("EditorIcons");
	const StringName EditorStyles = StringName("EditorStyles");
};

#define EditorStringName(m_name) EditorStringNames::get_singleton()->m_name
