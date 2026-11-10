/**************************************************************************/
/*  extension_api_dump.h                                                  */

#pragma once

#ifdef TOOLS_ENABLED

#include "core/error/error_list.h"
#include "core/string/ustring.h"
#include "core/variant/dictionary.h"

class NativeExtensionAPIDump {
public:
	static Dictionary generate_extension_api(bool p_include_docs = false);
	static void generate_extension_json_file(const String &p_path, bool p_include_docs = false);
	static Error validate_extension_json_file(const String &p_path);
};

#endif
