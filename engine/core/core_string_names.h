/**************************************************************************/
/*  core_string_names.h                                                   */

#pragma once

#include "core/string/string_name.h"

class CoreStringNames {
	inline static CoreStringNames *singleton = nullptr;

public:
	static void create() { singleton = memnew(CoreStringNames); }
	static void free() {
		memdelete(singleton);
		singleton = nullptr;
	}

	_FORCE_INLINE_ static CoreStringNames *get_singleton() { return singleton; }

	const StringName free_ = "free"; // free would conflict with C++ keyword.
	const StringName changed = "changed";
	const StringName script = "script";
	const StringName script_changed = "script_changed";
	const StringName _iter_init = "_iter_init";
	const StringName _iter_next = "_iter_next";
	const StringName _iter_get = "_iter_get";
	const StringName get_rid = "get_rid";
	const StringName _to_string = "_to_string";
	const StringName _custom_features = "_custom_features";

	const StringName x = "x";
	const StringName y = "y";
	const StringName z = "z";
	const StringName w = "w";
	const StringName r = "r";
	const StringName g = "g";
	const StringName b = "b";
	const StringName a = "a";
	const StringName position = "position";
	const StringName size = "size";
	const StringName end = "end";
	const StringName basis = "basis";
	const StringName origin = "origin";
	const StringName normal = "normal";
	const StringName d = "d";
	const StringName h = "h";
	const StringName s = "s";
	const StringName v = "v";
	const StringName r8 = "r8";
	const StringName g8 = "g8";
	const StringName b8 = "b8";
	const StringName a8 = "a8";

	const StringName call = "call";
	const StringName call_deferred = "call_deferred";
	const StringName bind = "bind";
	const StringName notification = "notification";
	const StringName property_list_changed = "property_list_changed";
};

#define CoreStringName(m_name) CoreStringNames::get_singleton()->m_name
