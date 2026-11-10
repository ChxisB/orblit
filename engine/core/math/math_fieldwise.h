/**************************************************************************/
/*  math_fieldwise.h                                                      */

#pragma once

#ifdef DEBUG_ENABLED

#include "core/variant/variant.h"

Variant fieldwise_assign(const Variant &p_target, const Variant &p_source, const String &p_field);

#endif // DEBUG_ENABLED
