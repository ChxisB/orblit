/**************************************************************************/
/*  web_game_bridge.h                                                     */

#pragma once

#include <cstdint>

class Object;

// Handles are how the C# game names an entity. See web_game_bridge.cpp.

// Returns a handle for the object, or 0 when the table is full.
int32_t web_game_acquire_handle(const Object *p_object);

// Frees the slot. A handle that is not live is ignored, so a double release
// is safe.
void web_game_release_handle(int32_t p_handle);
