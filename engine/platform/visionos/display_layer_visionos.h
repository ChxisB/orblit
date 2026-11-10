/**************************************************************************/
/*  display_layer_visionos.h                                              */

#pragma once

#include "backends/apple_embedded/display_layer_apple_embedded.h"

@interface GDTMetalLayer : CAMetalLayer <GDTDisplayLayer>
@end
