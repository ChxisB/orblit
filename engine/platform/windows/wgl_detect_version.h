/**************************************************************************/
/*  wgl_detect_version.h                                                  */

#pragma once

#if defined(WINDOWS_ENABLED) && defined(GLES3_ENABLED)

class Dictionary;

Dictionary detect_wgl();

#endif // WINDOWS_ENABLED && GLES3_ENABLED
