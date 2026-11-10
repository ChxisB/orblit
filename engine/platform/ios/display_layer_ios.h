/**************************************************************************/
/*  display_layer_ios.h                                                   */

#pragma once

#include "backends/apple_embedded/display_layer_apple_embedded.h"

#if defined(GLES3_ENABLED)
#import <OpenGLES/EAGLDrawable.h>
#endif
#import <QuartzCore/QuartzCore.h>

// An ugly workaround for iOS simulator
#if defined(TARGET_OS_SIMULATOR) && TARGET_OS_SIMULATOR
#if defined(__IPHONE_13_0)
API_AVAILABLE(ios(13.0))
@interface GDTMetalLayer : CAMetalLayer <GDTDisplayLayer>
#else
@interface GDTMetalLayer : CALayer <GDTDisplayLayer>
#endif
#else
@interface GDTMetalLayer : CAMetalLayer <GDTDisplayLayer>
#endif
@end

#if defined(GLES3_ENABLED)
API_DEPRECATED("OpenGLES is deprecated", ios(2.0, 12.0))
@interface GDTOpenGLLayer : CAEAGLLayer <GDTDisplayLayer>

@end
#endif
