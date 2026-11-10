/**************************************************************************/
/*  steam_tracker.h                                                       */

#pragma once

#if defined(STEAMAPI_ENABLED)

// SteamTracker is used to load SteamAPI dynamic library and initialize
// the interface, this notifies Steam that Orblit editor is running and
// allow tracking of the usage time of child instances of the engine
// (e.g., opened projects).
//
// Currently, SteamAPI is not used by the engine in any way, and is not
// exposed to the scripting APIs.

enum SteamAPIInitResult {
	SteamAPIInitResult_OK = 0,
	SteamAPIInitResult_FailedGeneric = 1,
	SteamAPIInitResult_NoSteamClient = 2,
	SteamAPIInitResult_VersionMismatch = 3,
};

// https://partner.steamgames.com/doc/api/steam_api#SteamAPI_Init
typedef bool (*SteamAPI_InitFunction)();
typedef SteamAPIInitResult (*SteamAPI_InitFlatFunction)(char *r_err_msg);

// https://partner.steamgames.com/doc/api/steam_api#SteamAPI_Shutdown
typedef void (*SteamAPI_ShutdownFunction)();

class SteamTracker {
	void *steam_library_handle = nullptr;
	SteamAPI_InitFunction steam_init_function = nullptr;
	SteamAPI_InitFlatFunction steam_init_flat_function = nullptr;
	SteamAPI_ShutdownFunction steam_shutdown_function = nullptr;
	bool steam_initialized = false;

public:
	SteamTracker();
	~SteamTracker();
};

#endif // STEAMAPI_ENABLED
