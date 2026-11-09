/**************************************************************************/
/*  core_constants.h                                                      */

#pragma once

#include "core/string/string_name.h"
#include "core/containers/hash_map.h"
#include "core/containers/list.h"

class CoreConstants {
public:
	static int get_global_constant_count();
	static StringName get_global_constant_enum(int p_idx);
	static bool is_global_constant_bitfield(int p_idx);
	static bool get_ignore_value_in_docs(int p_idx);
	static const char *get_global_constant_name(int p_idx);
	static int64_t get_global_constant_value(int p_idx);
	static bool is_global_constant(const StringName &p_name);
	static int get_global_constant_index(const StringName &p_name);
	static bool is_global_enum(const StringName &p_enum);
	static void get_enum_values(const StringName &p_enum, HashMap<StringName, int64_t> *r_values);
#ifdef TOOLS_ENABLED
	static void get_global_enums(List<StringName> *r_values);
#endif
};
