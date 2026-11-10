/**************************************************************************/
/*  native_extension_interface_header_generator.h                         */

#pragma once

#ifdef TOOLS_ENABLED

#include "core/io/file_access.h"

class NativeExtensionInterfaceHeaderGenerator {
public:
	static void generate_native_extension_interface_header(const String &p_path);

private:
	static void write_doc(const Ref<FileAccess> &p_fa, const Array &p_doc, const String &p_indent = "");
	static void write_simple_type(const Ref<FileAccess> &p_fa, const Dictionary &p_type);
	static void write_enum_type(const Ref<FileAccess> &p_fa, const Dictionary &p_enum);
	static void write_function_type(const Ref<FileAccess> &p_fa, const Dictionary &p_func);
	static void write_struct_type(const Ref<FileAccess> &p_fa, const Dictionary &p_struct);

	static String format_type_and_name(const String &p_type, const String &p_name);
	static String make_args_text(const Array &p_args);

	static void write_interface(const Ref<FileAccess> &p_fa, const Dictionary &p_interface);
};

#endif // TOOLS_ENABLED
