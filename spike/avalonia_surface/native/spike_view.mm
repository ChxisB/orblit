// A layer-backed native view whose layer is a CAMetalLayer, for the native
// child path. Throwaway.
#import <Cocoa/Cocoa.h>
#import <QuartzCore/CAMetalLayer.h>

@interface SpikeMetalView : NSView
@end

@implementation SpikeMetalView
- (BOOL)wantsUpdateLayer { return YES; }
- (CALayer *)makeBackingLayer { return [CAMetalLayer layer]; }
- (BOOL)isOpaque { return YES; }
@end

extern "C" void *spike_make_metal_view(void) {
  SpikeMetalView *view =
      [[SpikeMetalView alloc] initWithFrame:NSMakeRect(0, 0, 640, 360)];
  view.wantsLayer = YES;
  return (__bridge_retained void *)view;
}

extern "C" void *spike_view_layer(void *view) {
  return (__bridge void *)((__bridge NSView *)view).layer;
}

extern "C" int spike_view_pixels(void *view, unsigned *width, unsigned *height) {
  NSView *v = (__bridge NSView *)view;
  if (v.window == nil) return 0;
  const NSSize px = [v convertSizeToBacking:v.bounds.size];
  *width = (unsigned)px.width;
  *height = (unsigned)px.height;
  CAMetalLayer *layer = (CAMetalLayer *)v.layer;
  layer.contentsScale = v.window.backingScaleFactor;
  layer.drawableSize = px;
  return 1;
}

// A shared event for the compositor to wait on and signal, for a texture
// import that cannot sync by itself.
#import <Metal/Metal.h>

extern "C" void *spike_make_shared_event(void) {
  id<MTLDevice> device = MTLCreateSystemDefaultDevice();
  return (__bridge_retained void *)[device newSharedEvent];
}

extern "C" void spike_event_signal(void *event, unsigned long long value) {
  ((__bridge id<MTLSharedEvent>)event).signaledValue = value;
}

extern "C" unsigned long long spike_event_value(void *event) {
  return ((__bridge id<MTLSharedEvent>)event).signaledValue;
}
