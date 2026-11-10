/**************************************************************************/
/*  file_access_packed_byte_array.cpp                                     */

#include "file_access_packed_byte_array.h"

Error FileAccessPackedByteArray::open_custom(const PackedByteArray &p_data) {
	data = p_data;
	pos = 0;
	return OK;
}

void FileAccessPackedByteArray::seek(uint64_t p_position) {
	ERR_FAIL_COND(p_position > (uint64_t)data.size());
	pos = p_position;
}

void FileAccessPackedByteArray::seek_end(int64_t p_position) {
	ERR_FAIL_COND((int64_t)data.size() + p_position < 0);
	seek(data.size() + p_position);
}

uint64_t FileAccessPackedByteArray::get_position() const {
	return pos;
}

uint64_t FileAccessPackedByteArray::get_length() const {
	return data.size();
}

bool FileAccessPackedByteArray::eof_reached() const {
	return pos >= (uint64_t)data.size();
}

uint64_t FileAccessPackedByteArray::get_buffer(uint8_t *p_dst, uint64_t p_length) const {
	ERR_FAIL_NULL_V(p_dst, -1);

	uint64_t left = data.size() - pos;
	uint64_t read = MIN(p_length, left);

	if (read < p_length) {
		WARN_PRINT("Reading less data than requested");
	}

	memcpy(p_dst, &data[pos], read);
	pos += read;

	return read;
}

Error FileAccessPackedByteArray::get_error() const {
	return pos >= (uint64_t)data.size() ? ERR_FILE_EOF : OK;
}

Error FileAccessPackedByteArray::resize(int64_t p_length) {
	return data.resize(p_length);
}

bool FileAccessPackedByteArray::store_buffer(const uint8_t *p_src, uint64_t p_length) {
	if (!p_length) {
		return true;
	}

	ERR_FAIL_NULL_V(p_src, false);

	uint64_t left = data.size() - pos;
	uint64_t write = MIN(p_length, left);

	memcpy(data.ptrw() + pos, p_src, write);
	pos += write;

	ERR_FAIL_COND_V_MSG(write < p_length, false, "Writing less data than requested.");

	return true;
}
