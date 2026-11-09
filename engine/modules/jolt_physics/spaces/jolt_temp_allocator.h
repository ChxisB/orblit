/**************************************************************************/
/*  jolt_temp_allocator.h                                                 */

#pragma once

#include <Jolt/Jolt.h>

#include <Jolt/Core/TempAllocator.h>

#include <cstdint>

class JoltTempAllocator final : public JPH::TempAllocator {
	uint64_t capacity = 0;
	uint64_t top = 0;
	uint8_t *base = nullptr;

public:
	explicit JoltTempAllocator();
	virtual ~JoltTempAllocator() override;

	virtual void *Allocate(JPH::uint p_size) override;
	virtual void Free(void *p_ptr, JPH::uint p_size) override;
};
