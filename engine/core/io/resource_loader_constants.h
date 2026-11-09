/**************************************************************************/
/*  resource_loader_constants.h                                           */

#pragma once

#include "core/variant/type_info.h"

namespace ResourceLoaderConstants {

enum CacheMode {
	CACHE_MODE_IGNORE,
	CACHE_MODE_REUSE,
	CACHE_MODE_REPLACE,
	CACHE_MODE_IGNORE_DEEP,
	CACHE_MODE_REPLACE_DEEP,
};

} // namespace ResourceLoaderConstants

VARIANT_ENUM_CAST(ResourceLoaderConstants::CacheMode)
