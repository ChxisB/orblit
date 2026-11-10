/**************************************************************************/
/*  project_zip_packer.h                                                  */

#pragma once

class String;

typedef void *zipFile;

class ProjectZIPPacker {
	static void _zip_file(const String &p_path, const String &p_base_path, zipFile p_zip);
	static void _zip_recursive(const String &p_path, const String &p_base_path, zipFile p_zip);

public:
	static String get_project_zip_safe_name();
	static void pack_project_zip(const String &p_path);
	static void pack_zip_absolute_path(const String &p_output_path, const String &p_source_absolute_path);
};
