/**************************************************************************/
/*  button_view.mm                                                        */

#import "button_view.h"

@implementation OrblitButtonView

- (id)initWithFrame:(NSRect)frame {
	self = [super initWithFrame:frame];

	tracking_area = nil;
	offset = NSMakePoint(8, 8);
	spacing = 20;
	mouse_in_group = false;
	rtl = false;
	close_button = nullptr;
	miniaturize_button = nullptr;
	zoom_button = nullptr;

	return self;
}

- (void)initButtons:(CGFloat)button_spacing offset:(NSPoint)button_offset rtl:(bool)is_rtl {
	spacing = button_spacing;
	rtl = is_rtl;

	close_button = [NSWindow standardWindowButton:NSWindowCloseButton forStyleMask:NSWindowStyleMaskTitled];
	[close_button setFrameOrigin:NSMakePoint(rtl ? spacing * 2 : 0, 0)];
	[self addSubview:close_button];

	miniaturize_button = [NSWindow standardWindowButton:NSWindowMiniaturizeButton forStyleMask:NSWindowStyleMaskTitled];
	[miniaturize_button setFrameOrigin:NSMakePoint(spacing, 0)];
	[self addSubview:miniaturize_button];

	zoom_button = [NSWindow standardWindowButton:NSWindowZoomButton forStyleMask:NSWindowStyleMaskTitled];
	[zoom_button setFrameOrigin:NSMakePoint(rtl ? 0 : spacing * 2, 0)];
	[self addSubview:zoom_button];

	offset.y = button_offset.y - zoom_button.frame.size.height / 2;
	offset.x = button_offset.x - zoom_button.frame.size.width / 2;

	if (rtl) {
		[self setFrameSize:NSMakeSize(close_button.frame.origin.x + close_button.frame.size.width, close_button.frame.size.height)];
	} else {
		[self setFrameSize:NSMakeSize(zoom_button.frame.origin.x + zoom_button.frame.size.width, zoom_button.frame.size.height)];
	}
	[self displayButtons];
}

- (void)setOffset:(NSPoint)button_offset {
	if (zoom_button) {
		offset.y = button_offset.y - zoom_button.frame.size.height / 2;
		offset.x = button_offset.x - zoom_button.frame.size.width / 2;

		[self viewDidMoveToWindow];
	}
}

- (NSPoint)getOffset {
	return offset;
}

- (void)viewDidMoveToWindow {
	if (!self.window) {
		return;
	}

	if (rtl) {
		[self setAutoresizingMask:NSViewMinXMargin | NSViewMinYMargin];
		[self setFrameOrigin:NSMakePoint(self.window.frame.size.width - self.frame.size.width - offset.x, self.window.frame.size.height - self.frame.size.height - offset.y)];
	} else {
		[self setAutoresizingMask:NSViewMaxXMargin | NSViewMinYMargin];
		[self setFrameOrigin:NSMakePoint(offset.x, self.window.frame.size.height - self.frame.size.height - offset.y)];
	}
}

- (BOOL)_mouseInGroup:(NSButton *)button {
	return mouse_in_group;
}

- (void)updateTrackingAreas {
	if (tracking_area != nil) {
		[self removeTrackingArea:tracking_area];
	}

	NSTrackingAreaOptions options = NSTrackingMouseEnteredAndExited | NSTrackingActiveAlways | NSTrackingInVisibleRect;
	tracking_area = [[NSTrackingArea alloc] initWithRect:NSZeroRect options:options owner:self userInfo:nil];

	[self addTrackingArea:tracking_area];
}

- (void)mouseEntered:(NSEvent *)event {
	[super mouseEntered:event];

	mouse_in_group = true;
	[self displayButtons];
}

- (void)mouseExited:(NSEvent *)event {
	[super mouseExited:event];

	mouse_in_group = false;
	[self displayButtons];
}

- (void)displayButtons {
	for (NSView *subview in self.subviews) {
		[subview setNeedsDisplay:YES];
	}
}

@end
