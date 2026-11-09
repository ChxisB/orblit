/**************************************************************************/
/*  pck_packer.h                                                          */

#pragma once

#include "core/object/ref_counted.h"

class FileAccess;

class PCKPacker : public RefCounted {
	ORBLIT_CLASS(PCKPacker, RefCounted);

	Ref<FileAccess> file;
	int alignment = 0;

	Vector<uint8_t> key;
	bool enc_dir = false;

	uint64_t file_base = 0;
	uint64_t file_base_ofs = 0;
	uint64_t dir_base_ofs = 0;

	static void register_members();

	struct File {
		String path;
		String src_path;
		uint64_t ofs = 0;
		uint64_t size = 0;
		bool encrypted = false;
		bool removal = false;
		Vector<uint8_t> md5;
	};
	Vector<File> files;

	Error _add_file(const String &p_target_path, const String &p_source_path, const Vector<uint8_t> &p_data, bool p_encrypt = false);

public:
	Error pck_start(const String &p_pck_path, int p_alignment = 32, const String &p_key = "0000000000000000000000000000000000000000000000000000000000000000", bool p_encrypt_directory = false);
	Error add_file(const String &p_target_path, const String &p_source_path, bool p_encrypt = false);
	Error add_file_from_buffer(const String &p_target_path, const Vector<uint8_t> &p_data, bool p_encrypt = false);
	Error add_file_removal(const String &p_target_path);
	Error flush(bool p_verbose = false);

	~PCKPacker();
};
