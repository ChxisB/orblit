/**************************************************************************/
/*  macos_utils.h                                                         */

#pragma once

#ifdef MACOS_ENABLED

#include "core/string/ustring.h"

bool macos_is_app_bundle_installed(const String &p_bundle_id);

#endif
