/**************************************************************************/
/*  zip_reader.h                                                          */

#pragma once

#include "core/io/file_access.h"
#include "core/object/ref_counted.h"

#include <thirdparty/minizip/unzip.h>

class ZIPReader : public RefCounted {
	ORBLIT_CLASS(ZIPReader, RefCounted)

	Ref<FileAccess> fa;
	unzFile uzf = nullptr;

protected:
	static void register_members();

public:
	Error open(const String &p_path);
	Error close();

	PackedStringArray get_files();
	PackedByteArray read_file(const String &p_path, bool p_case_sensitive);
	bool file_exists(const String &p_path, bool p_case_sensitive);
	int get_compression_level(const String &p_path, bool p_case_sensitive);

	ZIPReader();
	~ZIPReader();
};
