/**************************************************************************/
/*  os_ios.h                                                              */

#pragma once

#ifdef IOS_ENABLED

#import "backends/apple_embedded/os_apple_embedded.h"

class OS_IOS : public OS_AppleEmbedded {
public:
	static OS_IOS *get_singleton();

	OS_IOS();
	~OS_IOS();

	virtual String get_name() const override;
};

#endif // IOS_ENABLED
