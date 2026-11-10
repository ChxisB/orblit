/**************************************************************************/
/*  core_cursor.h                                                         */

#pragma once

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>

constexpr int32_t GDCoreCursorWindowMove = 39;

// Expose private interface for CoreCursor.
@interface OrblitCoreCursor : NSCursor

@property(readonly, nonatomic) int32_t _coreCursorType;

- (id)initWithType:(int32_t)type;

@end
