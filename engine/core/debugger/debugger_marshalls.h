/**************************************************************************/
/*  debugger_marshalls.h                                                  */

#pragma once

#include "core/input/shortcut.h"
#include "core/object/script_language.h"

struct DebuggerMarshalls {
	struct ScriptStackVariable {
		String name;
		Variant value;
		int type = -1;
		int var_type = -1;
		String type_hint;

		Array serialize(int p_max_size = 1 << 20); // 1 MiB default.
		bool deserialize(const Array &p_arr);
	};

	struct ScriptStackDump {
		List<ScriptLanguage::StackInfo> frames;

		Array serialize();
		bool deserialize(const Array &p_arr);
	};

	struct OutputError {
		int hr = -1;
		int min = -1;
		int sec = -1;
		int msec = -1;
		String source_file;
		String source_func;
		int source_line = -1;
		String error;
		String error_descr;
		bool warning = false;
		Vector<ScriptLanguage::StackInfo> callstack;

		Array serialize();
		bool deserialize(const Array &p_arr);
	};

	static Array serialize_key_shortcut(const Ref<Shortcut> &p_shortcut);
	static Ref<Shortcut> deserialize_key_shortcut(const Array &p_keys);

	static String parse_type_from_variant(const Variant &p_variant);
};
