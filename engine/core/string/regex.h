/**************************************************************************/
/*  regex.h                                                               */

#pragma once

#include "core/object/ref_counted.h"
#include "core/string/ustring.h"
#include "core/containers/hash_map.h"
#include "core/containers/vector.h"
#include "core/variant/array.h"
#include "core/variant/dictionary.h"
#include "core/variant/typed_array.h"

class RegExMatch : public RefCounted {
	ORBLIT_CLASS(RegExMatch, RefCounted);

	struct Range {
		int start = 0;
		int end = 0;
	};

	String subject;
	Vector<Range> data;
	HashMap<String, int> names;

	friend class RegEx;

protected:
	static void register_members();

	int _find(const Variant &p_name) const;

public:
	String get_subject() const;
	int get_group_count() const;
	Dictionary get_names() const;

	PackedStringArray get_strings() const;
	String get_string(const Variant &p_name) const;
	int get_start(const Variant &p_name) const;
	int get_end(const Variant &p_name) const;
};

class RegEx : public RefCounted {
	ORBLIT_CLASS(RegEx, RefCounted);

	void *general_ctx = nullptr;
	void *code = nullptr;
	String pattern;

	void _pattern_info(uint32_t p_what, void *r_where) const;

	int _sub(const String &p_subject, const String &p_replacement, int p_offset, int p_end, uint32_t p_flags, String &r_output) const;

protected:
	static void register_members();

public:
	static Ref<RegEx> create_from_string(const String &p_pattern, bool p_show_error = true);

	void clear();
	Error compile(const String &p_pattern, bool p_show_error = true);

	Ref<RegExMatch> search(const String &p_subject, int p_offset = 0, int p_end = -1) const;
	TypedArray<RegExMatch> search_all(const String &p_subject, int p_offset = 0, int p_end = -1) const;
	String sub(const String &p_subject, const String &p_replacement, bool p_all = false, int p_offset = 0, int p_end = -1) const;

	bool is_valid() const;
	String get_pattern() const;
	int get_group_count() const;
	PackedStringArray get_names() const;

	RegEx();
	RegEx(const String &p_pattern);
	~RegEx();
};
