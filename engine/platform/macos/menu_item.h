/**************************************************************************/
/*  menu_item.h                                                           */

#pragma once

#include "core/io/image.h"
#include "core/object/ref_counted.h"
#include "core/os/keyboard.h"
#include "core/variant/callable.h"
#include "core/variant/variant.h"

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>

#define MENU_TAG_START 0x00004447
#define MENU_TAG_END 0xFFFF4447

enum GlobalMenuCheckType {
	CHECKABLE_TYPE_NONE,
	CHECKABLE_TYPE_CHECK_BOX,
	CHECKABLE_TYPE_RADIO_BUTTON,
};

@interface OrblitMenuItem : NSObject {
@public
	Callable callback;
	Callable key_callback;
	Callable hover_callback;
	Variant meta;
	Key accel;
	GlobalMenuCheckType checkable_type;
	bool checked;
	bool enabled;
	bool indeterminate;
	int max_states;
	int state;
	int indent;
	Ref<Image> img;
}

@end
