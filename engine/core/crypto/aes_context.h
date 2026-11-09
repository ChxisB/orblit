/**************************************************************************/
/*  aes_context.h                                                         */

#pragma once

#include "core/crypto/crypto_core.h"
#include "core/object/ref_counted.h"
#include "core/variant/type_info.h"

class AESContext : public RefCounted {
	ORBLIT_CLASS(AESContext, RefCounted);

public:
	enum Mode : int32_t {
		MODE_ECB_ENCRYPT,
		MODE_ECB_DECRYPT,
		MODE_CBC_ENCRYPT,
		MODE_CBC_DECRYPT,
		MODE_MAX
	};

private:
	Mode mode = MODE_MAX;
	CryptoCore::AESContext ctx;

protected:
	static void register_members();

public:
	Error start(Mode p_mode, const PackedByteArray &p_key, const PackedByteArray &p_iv = PackedByteArray());
	PackedByteArray update(const PackedByteArray &p_src);
	void finish();
};

VARIANT_ENUM_CAST(AESContext::Mode);
