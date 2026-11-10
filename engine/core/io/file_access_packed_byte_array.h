/**************************************************************************/
/*  file_access_packed_byte_array.h                                       */

#pragma once

#include "core/io/file_access.h"

class FileAccessPackedByteArray : public FileAccess {
	ORBLIT_SOFTCLASS(FileAccessPackedByteArray, FileAccess);

	PackedByteArray data;
	mutable uint64_t pos = 0;

	static Ref<FileAccess> create();

public:
	virtual Error open_custom(const PackedByteArray &p_data);
	virtual Error open_internal(const String &p_path, int p_mode_flags) override { return ERR_UNAVAILABLE; }
	virtual bool is_open() const override { return true; }

	virtual void seek(uint64_t p_position) override;
	virtual void seek_end(int64_t p_position) override;
	virtual uint64_t get_position() const override;
	virtual uint64_t get_length() const override;

	virtual bool eof_reached() const override;

	virtual uint64_t get_buffer(uint8_t *p_dst, uint64_t p_length) const override;

	virtual Error get_error() const override;

	virtual Error resize(int64_t p_length) override;
	virtual void flush() override {}
	virtual bool store_buffer(const uint8_t *p_src, uint64_t p_length) override;

	virtual bool file_exists(const String &p_name) override { return false; }

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
