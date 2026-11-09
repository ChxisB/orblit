/**************************************************************************/
/*  hostfxr_resolver.h                                                    */

#pragma once

#include "core/string/ustring.h"

namespace orblitsharp {
namespace hostfxr_resolver {

bool try_get_path_from_dotnet_root(const String &p_dotnet_root, String &r_out_fxr_path);
bool try_get_path(String &r_out_dotnet_root, String &r_out_fxr_path);

} //namespace hostfxr_resolver
} //namespace orblitsharp
