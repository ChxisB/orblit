/**************************************************************************/
/*  jolt_stream_wrappers.h                                                */

#pragma once

#ifdef DEBUG_ENABLED

#include "core/io/file_access.h"

#include <Jolt/Jolt.h>

#include <Jolt/Core/StreamIn.h>
#include <Jolt/Core/StreamOut.h>

class JoltStreamOutputWrapper final : public JPH::StreamOut {
	Ref<FileAccess> file_access;

public:
	explicit JoltStreamOutputWrapper(const Ref<FileAccess> &p_file_access) :
			file_access(p_file_access) {}

	virtual void WriteBytes(const void *p_data, size_t p_bytes) override {
		file_access->store_buffer(static_cast<const uint8_t *>(p_data), static_cast<uint64_t>(p_bytes));
	}

	virtual bool IsFailed() const override {
		return file_access->get_error() != OK;
	}
};

class JoltStreamInputWrapper final : public JPH::StreamIn {
	Ref<FileAccess> file_access;

public:
	explicit JoltStreamInputWrapper(const Ref<FileAccess> &p_file_access) :
			file_access(p_file_access) {}

	virtual void ReadBytes(void *p_data, size_t p_bytes) override {
		file_access->get_buffer(static_cast<uint8_t *>(p_data), static_cast<uint64_t>(p_bytes));
	}

	virtual bool IsEOF() const override {
		return file_access->eof_reached();
	}

	virtual bool IsFailed() const override {
		return file_access->get_error() != OK;
	}
};

#endif
