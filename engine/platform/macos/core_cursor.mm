/**************************************************************************/
/*  core_cursor.mm                                                        */

#import "core_cursor.h"

@implementation OrblitCoreCursor
@synthesize _coreCursorType = _type;

- (id)initWithType:(int32_t)type {
	if ((self = [super init])) {
		_type = type;
	}
	return self;
}

@end
