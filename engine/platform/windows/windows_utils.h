/**************************************************************************/
/*  windows_utils.h                                                       */

#pragma once

#ifdef WINDOWS_ENABLED

#include "core/containers/hash_map.h"
#include "core/string/ustring.h"

class WindowsUtils {
	static HashMap<String, Vector<String>> temp_pdbs;

public:
	static Error copy_and_rename_pdb(const String &p_dll_path);
	static void remove_temp_pdbs(const String &p_dll_path);
};

#endif // WINDOWS_ENABLED
