/**************************************************************************/
/*  file_access_memory.h                                                  */

#pragma once

#include "core/io/file_access.h"

class FileAccessMemory : public FileAccess {
	ORBLIT_SOFTCLASS(FileAccessMemory, FileAccess);
	uint8_t *data = nullptr;
	uint64_t length = 0;
	mutable uint64_t pos = 0;

	static Ref<FileAccess> create();

public:
	static void register_file(const String &p_name, const Vector<uint8_t> &p_data);
	static void cleanup();

	virtual Error open_custom(const uint8_t *p_data, uint64_t p_len); ///< open a file
	virtual Error open_internal(const String &p_path, int p_mode_flags) override; ///< open a file
	virtual bool is_open() const override; ///< true when file is open

	virtual void seek(uint64_t p_position) override; ///< seek to a given position
	virtual void seek_end(int64_t p_position) override; ///< seek from the end of file
	virtual uint64_t get_position() const override; ///< get position in the file
	virtual uint64_t get_length() const override; ///< get size of the file

	virtual bool eof_reached() const override; ///< reading passed EOF

	virtual uint64_t get_buffer(uint8_t *p_dst, uint64_t p_length) const override; ///< get an array of bytes

	virtual Error get_error() const override; ///< get last error

	virtual Error resize(int64_t p_length) override { return ERR_UNAVAILABLE; }
	virtual void flush() override;
	virtual bool store_buffer(const uint8_t *p_src, uint64_t p_length) override; ///< store an array of bytes

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

	virtual void close() override {}
};
