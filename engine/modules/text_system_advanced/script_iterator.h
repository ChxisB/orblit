/**************************************************************************/
/*  script_iterator.h                                                     */

#pragma once

#include "core/string/ustring.h"
#include "core/containers/vector.h"

#include <hb-icu.h>
#include <hb.h>
#include <unicode/uchar.h>
#include <unicode/uloc.h>
#include <unicode/uscript.h>
#include <unicode/ustring.h>
#include <unicode/utypes.h>

class ScriptIterator {
	static const int PAREN_STACK_DEPTH = 128;
	static const int EMOJI_STACK_DEPTH = 32;

public:
	struct ScriptRange {
		int start = 0;
		int end = 0;
		hb_script_t script = HB_SCRIPT_COMMON;
	};
	Vector<ScriptRange> script_ranges;

private:
	inline static bool same_script(int32_t p_script_one, int32_t p_script_two);
	inline static bool is_emoji(UChar32 p_c, UChar32 p_next);

public:
	ScriptIterator(const String &p_string, int p_start, int p_length);
};
