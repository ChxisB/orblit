/**************************************************************************/
/*  foundation_helpers.h                                                  */

#pragma once

#ifdef __OBJC__
#import <Foundation/NSString.h>
#else
#include <Foundation/NSString.hpp>
#endif

class String;
template <typename T>
class CharStringT;

using CharString = CharStringT<char>;

template <typename T>
class Span;

namespace conv {

#ifdef __OBJC__
/**
 * Converts a Orblit String to an NSString without allocating an intermediate UTF-8 buffer.
 * */
NSString *to_nsstring(const String &p_str);
/**
 * Converts a Orblit String to an NSString without allocating an intermediate UTF-8 buffer.
 * */
NSString *to_nsstring(Span<char> p_str);
/**
 * Converts a Orblit CharString to an NSString without allocating an intermediate UTF-8 buffer.
 * */
NSString *to_nsstring(const CharString &p_str);
/**
 * Converts an NSString to a Orblit String without allocating intermediate buffers.
 * */
String to_string(NSString *p_str);
#else
/**
 * Converts a Orblit String to an NSString without allocating an intermediate UTF-8 buffer.
 * */
NS::String *to_nsstring(const String &p_str);
/**
 * Converts a Orblit String to an NSString without allocating an intermediate UTF-8 buffer.
 * */
NS::String *to_nsstring(Span<char> p_str);
/**
 * Converts a Orblit CharString to an NSString without allocating an intermediate UTF-8 buffer.
 * */
NS::String *to_nsstring(const CharString &p_str);
/**
 * Converts an NSString to a Orblit String without allocating intermediate buffers.
 * */
String to_string(NS::String *p_str);

#endif

} //namespace conv
