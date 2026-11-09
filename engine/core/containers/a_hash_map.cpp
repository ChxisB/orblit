/**************************************************************************/
/*  a_hash_map.cpp                                                        */

#include "a_hash_map.h"

#include "core/variant/variant.h"

// Explicit instantiation.
template class AHashMap<int, int>;
template class AHashMap<String, int>;
template class AHashMap<StringName, StringName>;
template class AHashMap<StringName, Variant>;
template class AHashMap<StringName, int>;
