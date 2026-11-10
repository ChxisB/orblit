/**************************************************************************/
/*  rect2i.cpp                                                            */

#include "rect2i.h"

#include "core/math/rect2.h"
#include "core/string/ustring.h"

Rect2i::operator String() const {
	return "[P: " + String(position) + ", S: " + String(size) + "]";
}

Rect2i::operator Rect2() const {
	return Rect2(position, size);
}
