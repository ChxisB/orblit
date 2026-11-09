/**************************************************************************/
/*  class_registry_api_json.h                                             */

#pragma once

#ifdef DEBUG_ENABLED

#include "core/object/class_registry.h"

void class_registry_api_to_json(const String &p_output_file, ClassRegistry::APIType p_api);

#endif // DEBUG_ENABLED
