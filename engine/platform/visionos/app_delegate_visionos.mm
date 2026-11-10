/**************************************************************************/
/*  app_delegate_visionos.mm                                              */

#import "app_delegate_visionos.h"

#include "app_delegate_service_visionos.h"

@implementation GDTAppDelegateVisionOS

+ (void)load {
	[self addService:[GDTAppDelegateServiceVisionOS new]];
}

@end
