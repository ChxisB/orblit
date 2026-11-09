/**************************************************************************/
/*  app_delegate_ios.mm                                                   */

#import "app_delegate_ios.h"

#import "backends/apple_embedded/app_delegate_service_apple_embedded.h"

@implementation GDTAppDelegateIOS

+ (void)load {
	[self addService:[GDTAppDelegateService new]];
}

@end
