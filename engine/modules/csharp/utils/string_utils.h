/**************************************************************************/
/*  string_utils.h                                                        */

#pragma once

#include "core/string/ustring.h"

#include <cstdarg>

String sformat(const String &p_text, const String &p1 = String(), const String &p2 = String(),
		const String &p3 = String(), const String &p4 = String(), const String &p5 = String(), const String &p6 = String());

#ifdef TOOLS_ENABLED
bool is_csharp_keyword(const String &p_name);

String escape_csharp_keyword(const String &p_name);
#endif

Error read_all_file_utf8(const String &p_path, String &r_content);

#if defined(__GNUC__)
#define _PRINTF_FORMAT_ATTRIBUTE_1_0 __attribute__((format(printf, 1, 0)))
#define _PRINTF_FORMAT_ATTRIBUTE_1_2 __attribute__((format(printf, 1, 2)))
#else
#define _PRINTF_FORMAT_ATTRIBUTE_1_0
#define _PRINTF_FORMAT_ATTRIBUTE_1_2
#endif

String str_format(const char *p_format, ...) _PRINTF_FORMAT_ATTRIBUTE_1_2;
String str_format(const char *p_format, va_list p_list) _PRINTF_FORMAT_ATTRIBUTE_1_0;
char *str_format_new(const char *p_format, ...) _PRINTF_FORMAT_ATTRIBUTE_1_2;
char *str_format_new(const char *p_format, va_list p_list) _PRINTF_FORMAT_ATTRIBUTE_1_0;
