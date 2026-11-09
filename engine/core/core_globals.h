/**************************************************************************/
/*  core_globals.h                                                        */

#pragma once

// Home for state needed from global functions
// that cannot be stored in Engine or OS due to e.g. circular includes

class CoreGlobals {
public:
	static inline bool leak_reporting_enabled = true;
	static inline bool print_line_enabled = true;
	static inline bool print_error_enabled = true;
	static inline bool print_ready = false;
};
