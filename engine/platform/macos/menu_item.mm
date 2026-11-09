/**************************************************************************/
/*  menu_item.mm                                                          */

#import "menu_item.h"

@implementation OrblitMenuItem

- (id)init {
	self = [super init];

	self->callback = Callable();
	self->key_callback = Callable();
	self->checkable_type = GlobalMenuCheckType::CHECKABLE_TYPE_NONE;
	self->enabled = true;
	self->checked = false;
	self->indeterminate = false;
	self->max_states = 0;
	self->state = 0;
	self->indent = 0;
	self->accel = Key::NONE;

	return self;
}

@end
