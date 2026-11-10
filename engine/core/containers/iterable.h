/**************************************************************************/
/*  iterable.h                                                            */

#pragma once

#include <utility>

template <typename I>
class Iterable {
	I _begin;
	I _end;

public:
	I begin() { return _begin; }
	I end() { return _end; }

	Iterable(I &&p_begin, I &&p_end) :
			_begin(std::move(p_begin)), _end(std::move(p_end)) {}
	Iterable(const I &p_begin, const I &p_end) :
			_begin(p_begin), _end(p_end) {}
};
