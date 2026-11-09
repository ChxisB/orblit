/**************************************************************************/
/*  print_string.h                                                        */

#pragma once

#include "core/containers/span.h"

class Variant;
class String;

extern void (*_print_func)(String);

typedef void (*PrintHandlerFunc)(void *, const String &p_string, bool p_error, bool p_rich);

struct PrintHandlerList {
	PrintHandlerFunc printfunc = nullptr;
	void *userdata = nullptr;

	PrintHandlerList *next = nullptr;
};

String stringify_variants(const Span<Variant> &p_vars);

void add_print_handler(PrintHandlerList *p_handler);
void remove_print_handler(const PrintHandlerList *p_handler);

extern void __print_line(const String &p_string);
extern void __print_line_rich(const String &p_string);
extern void print_raw(const String &p_string);
extern void print_error(const String &p_string);
extern bool is_print_verbose_enabled();

// This version avoids processing the text to be printed until it actually has to be printed, saving some CPU usage.
#define print_verbose(m_text) \
	{ \
		if (is_print_verbose_enabled()) { \
			print_line(m_text); \
		} \
	}

template <typename... Args>
void print_line(Args... p_args) {
	Variant variants[sizeof...(p_args)] = { p_args... };
	__print_line(stringify_variants(Span(variants)));
}

template <typename... Args>
void print_line_rich(Args... p_args) {
	Variant variants[sizeof...(p_args)] = { p_args... };
	__print_line_rich(stringify_variants(Span(variants)));
}
