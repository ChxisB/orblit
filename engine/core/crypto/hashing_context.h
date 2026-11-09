/**************************************************************************/
/*  hashing_context.h                                                     */

#pragma once

#include "core/object/ref_counted.h"
#include "core/variant/type_info.h"

class HashingContext : public RefCounted {
	ORBLIT_CLASS(HashingContext, RefCounted);

public:
	enum HashType : int32_t {
		HASH_MD5,
		HASH_SHA1,
		HASH_SHA256
	};

private:
	void *ctx = nullptr;
	HashType type = HASH_MD5;

protected:
	static void register_members();
	void _create_ctx(HashType p_type);
	void _delete_ctx();

public:
	Error start(HashType p_type);
	Error update(const PackedByteArray &p_chunk);
	PackedByteArray finish();

	~HashingContext();
};

VARIANT_ENUM_CAST(HashingContext::HashType);
