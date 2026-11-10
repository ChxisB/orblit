/**************************************************************************/
/*  thread_windows.cpp                                                    */

#ifdef WINDOWS_ENABLED

#include "thread_windows.h"

#include "core/os/thread.h"
#include "core/string/ustring.h"

#include <windows.h>

typedef HRESULT(WINAPI *SetThreadDescriptionPtr)(HANDLE p_thread, PCWSTR p_thread_description);
SetThreadDescriptionPtr w10_SetThreadDescription = nullptr;

static Error set_name(const String &p_name) {
	HANDLE hThread = GetCurrentThread();
	HRESULT res = E_FAIL;
	if (w10_SetThreadDescription) {
		res = w10_SetThreadDescription(hThread, (LPCWSTR)p_name.utf16().get_data()); // Windows 10 Redstone (1607) only.
	}
	return SUCCEEDED(res) ? OK : ERR_INVALID_PARAMETER;
}

void init_thread_win() {
	w10_SetThreadDescription = (SetThreadDescriptionPtr)(void *)GetProcAddress(LoadLibraryW(L"kernel32.dll"), "SetThreadDescription");

	Thread::_set_platform_functions({ set_name });
}

#endif // WINDOWS_ENABLED
