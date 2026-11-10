/**************************************************************************/
/*  os_visionos.h                                                         */

#pragma once

#ifdef VISIONOS_ENABLED

#import "backends/apple_embedded/os_apple_embedded.h"

class OS_VisionOS : public OS_AppleEmbedded {
public:
	static OS_VisionOS *get_singleton();

	OS_VisionOS();
	~OS_VisionOS();

	virtual String get_name() const override;
};

#endif // VISIONOS_ENABLED
