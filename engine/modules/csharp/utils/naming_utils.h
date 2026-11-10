/**************************************************************************/
/*  naming_utils.h                                                        */

#pragma once

#include "core/string/ustring.h"

String pascal_to_pascal_case(const String &p_identifier);

String snake_to_pascal_case(const String &p_identifier, bool p_input_is_upper = false);

String snake_to_camel_case(const String &p_identifier, bool p_input_is_upper = false);
