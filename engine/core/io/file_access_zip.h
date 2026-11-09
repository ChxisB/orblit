/**************************************************************************/
/*  file_access_zip.h                                                     */

#pragma once

#ifdef MINIZIP_ENABLED

#include "core/io/file_access_pack.h"

#include <thirdparty/minizip/unzip.h>

class ZipArchive : public PackSource {
public:
	struct File {
		int package = -1;
		unz_file_pos file_pos;
	};

private:
	struct Package {
		String filename;
	};
	Vector<Package> packages;

	HashMap<String, File> files;

	static inline ZipArchive *instance = nullptr;

public:
	void close_handle(unzFile p_file) const;
	unzFile get_file_handle(const String &p_file) const;

	Error add_package(const String &p_name);

	bool file_exists(const String &p_name) const;

	virtual bool try_open_pack(const String &p_path, bool p_replace_files, uint64_t p_offset, const Vector<uint8_t> &p_decryption_key = Vector<uint8_t>()) override;
	Ref<FileAccess> get_file(const String &p_path, PackedData::PackedFile *p_file, const Vector<uint8_t> &p_decryption_key = Vector<uint8_t>()) override;

	static ZipArchive *get_singleton();

	ZipArchive();
	~ZipArchive();
};

class FileAccessZip : public FileAccess {
	ORBLIT_SOFTCLASS(FileAccessZip, FileAccess);
	unzFile zfile = nullptr;
	unz_file_info64 file_info;

	mutable bool at_eof = false;

	void _close();

public:
	virtual Error open_internal(const String &p_path, int p_mode_flags) override; ///< open a file
	virtual bool is_open() const override; ///< true when file is open

	virtual void seek(uint64_t p_position) override; ///< seek to a given position
	virtual void seek_end(int64_t p_position = 0) override; ///< seek from the end of file
	virtual uint64_t get_position() const override; ///< get position in the file
	virtual uint64_t get_length() const override; ///< get size of the file

	virtual bool eof_reached() const override; ///< reading passed EOF

	virtual uint64_t get_buffer(uint8_t *p_dst, uint64_t p_length) const override;

	virtual Error get_error() const override; ///< get last error

	virtual Error resize(int64_t p_length) override { return ERR_UNAVAILABLE; }
	virtual void flush() override;
	virtual bool store_buffer(const uint8_t *p_src, uint64_t p_length) override;

	virtual bool file_exists(const String &p_name) override; ///< return true if a file exists

	virtual uint64_t _get_modified_time(const String &p_file) override { return 0; }
	virtual uint64_t _get_access_time(const String &p_file) override { return 0; }
	virtual int64_t _get_size(const String &p_file) override { return -1; }
	virtual BitField<FileAccess::UnixPermissionFlags> _get_unix_permissions(const String &p_file) override { return 0; }
	virtual Error _set_unix_permissions(const String &p_file, BitField<FileAccess::UnixPermissionFlags> p_permissions) override { return FAILED; }

	virtual bool _get_hidden_attribute(const String &p_file) override { return false; }
	virtual Error _set_hidden_attribute(const String &p_file, bool p_hidden) override { return ERR_UNAVAILABLE; }
	virtual bool _get_read_only_attribute(const String &p_file) override { return false; }
	virtual Error _set_read_only_attribute(const String &p_file, bool p_ro) override { return ERR_UNAVAILABLE; }

	virtual void close() override;

	FileAccessZip(const String &p_path, const PackedData::PackedFile &p_file);
	~FileAccessZip();
};

#endif // MINIZIP_ENABLED
