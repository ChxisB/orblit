/**************************************************************************/
/*  api.h                                                                 */

#pragma once

#if defined(IOS_ENABLED)
extern void orblit_apple_embedded_plugins_initialize();
extern void orblit_apple_embedded_plugins_deinitialize();
#endif

void register_ios_api();
void unregister_ios_api();
