/**************************************************************************/
/*  hashing_context.cpp                                                   */

#include "hashing_context.h"

#include "core/crypto/crypto_core.h"
#include "core/object/class_registry.h"

Error HashingContext::start(HashType p_type) {
	ERR_FAIL_COND_V(ctx != nullptr, ERR_ALREADY_IN_USE);
	_create_ctx(p_type);
	ERR_FAIL_NULL_V(ctx, ERR_UNAVAILABLE);
	switch (type) {
		case HASH_MD5:
			return ((CryptoCore::MD5Context *)ctx)->start();
		case HASH_SHA1:
			return ((CryptoCore::SHA1Context *)ctx)->start();
		case HASH_SHA256:
			return ((CryptoCore::SHA256Context *)ctx)->start();
	}
	return ERR_UNAVAILABLE;
}

Error HashingContext::update(const PackedByteArray &p_chunk) {
	ERR_FAIL_NULL_V(ctx, ERR_UNCONFIGURED);
	size_t len = p_chunk.size();
	ERR_FAIL_COND_V(len == 0, FAILED);
	const uint8_t *r = p_chunk.ptr();
	switch (type) {
		case HASH_MD5:
			return ((CryptoCore::MD5Context *)ctx)->update(&r[0], len);
		case HASH_SHA1:
			return ((CryptoCore::SHA1Context *)ctx)->update(&r[0], len);
		case HASH_SHA256:
			return ((CryptoCore::SHA256Context *)ctx)->update(&r[0], len);
	}
	return ERR_UNAVAILABLE;
}

PackedByteArray HashingContext::finish() {
	ERR_FAIL_NULL_V(ctx, PackedByteArray());
	PackedByteArray out;
	Error err = FAILED;
	switch (type) {
		case HASH_MD5:
			out.resize(16);
			err = ((CryptoCore::MD5Context *)ctx)->finish(out.ptrw());
			break;
		case HASH_SHA1:
			out.resize(20);
			err = ((CryptoCore::SHA1Context *)ctx)->finish(out.ptrw());
			break;
		case HASH_SHA256:
			out.resize(32);
			err = ((CryptoCore::SHA256Context *)ctx)->finish(out.ptrw());
			break;
	}
	_delete_ctx();
	ERR_FAIL_COND_V(err != OK, PackedByteArray());
	return out;
}

void HashingContext::_create_ctx(HashType p_type) {
	type = p_type;
	switch (type) {
		case HASH_MD5:
			ctx = memnew(CryptoCore::MD5Context);
			break;
		case HASH_SHA1:
			ctx = memnew(CryptoCore::SHA1Context);
			break;
		case HASH_SHA256:
			ctx = memnew(CryptoCore::SHA256Context);
			break;
		default:
			ctx = nullptr;
	}
}

void HashingContext::_delete_ctx() {
	switch (type) {
		case HASH_MD5:
			memdelete((CryptoCore::MD5Context *)ctx);
			break;
		case HASH_SHA1:
			memdelete((CryptoCore::SHA1Context *)ctx);
			break;
		case HASH_SHA256:
			memdelete((CryptoCore::SHA256Context *)ctx);
			break;
	}
	ctx = nullptr;
}

void HashingContext::register_members() {
	ClassRegistry::bind_method(D_METHOD("start", "type"), &HashingContext::start);
	ClassRegistry::bind_method(D_METHOD("update", "chunk"), &HashingContext::update);
	ClassRegistry::bind_method(D_METHOD("finish"), &HashingContext::finish);
	BIND_ENUM_CONSTANT(HASH_MD5);
	BIND_ENUM_CONSTANT(HASH_SHA1);
	BIND_ENUM_CONSTANT(HASH_SHA256);
}

HashingContext::~HashingContext() {
	if (ctx != nullptr) {
		_delete_ctx();
	}
}
