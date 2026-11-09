/**************************************************************************/
/*  plural_rules.h                                                        */

#pragma once

#include "core/containers/lru.h"
#include "core/object/ref_counted.h"

class Expression;

class PluralRules : public Object {
	ORBLIT_SOFTCLASS(PluralRules, Object);

	mutable LRUCache<int, int> cache;

	// These two fields are initialized in the constructor.
	const int nplurals;
	const String plural;

	// Cache temporary variables related to `evaluate()` to make it faster.
	class EQNode : public RefCounted {
		ORBLIT_SOFTCLASS(EQNode, RefCounted);

	public:
		String regex;
		Ref<EQNode> left;
		Ref<EQNode> right;
	};
	Ref<EQNode> equi_tests;
	Ref<Expression> expr;

	int _find_unquoted(const String &p_src, char32_t p_chr) const;
	int _eq_test(const Array &p_input_val, const Ref<EQNode> &p_node, const Variant &p_result) const;
	void _cache_plural_tests(const String &p_plural_rule, Ref<EQNode> &p_node);

	PluralRules(int p_nplurals, const String &p_plural);

public:
	int evaluate(int p_n) const;

	int get_nplurals() const { return nplurals; }
	String get_plural() const { return plural; }

	static PluralRules *parse(const String &p_rules);
};
