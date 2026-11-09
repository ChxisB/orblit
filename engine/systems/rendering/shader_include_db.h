/**************************************************************************/
/*  shader_include_db.h                                                   */

#pragma once

#include "core/object/object.h"

class ShaderIncludeDB : public Object {
	ORBLIT_CLASS(ShaderIncludeDB, Object)

private:
	static HashMap<String, String> built_in_includes;

protected:
	static void register_members();

public:
	static void register_built_in_include_file(const String &p_filename, const String &p_shader_code);
	static PackedStringArray list_built_in_include_files();
	static bool has_built_in_include_file(const String &p_filename);
	static String get_built_in_include_file(const String &p_filename);
	static String parse_include_files(const String &p_code);
};
