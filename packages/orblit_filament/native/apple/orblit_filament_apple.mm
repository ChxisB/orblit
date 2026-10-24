// The Metal calls the Avalonia viewport needs and the renderer's C API does not
// have. Avalonia 12.1.4 cannot synchronise an imported IOSurface on Metal by
// itself, so the host hands the compositor two shared events: one says a frame
// is drawn, the other says the compositor has finished reading it. The Flutter
// plugins do not build this file.
#import <Metal/Metal.h>

// A shared event on the default device, retained once. Free it with release.
extern "C" void *orblit_apple_event_create(void) {
  id<MTLDevice> device = MTLCreateSystemDefaultDevice();
  if (device == nil) return nullptr;
  return (__bridge_retained void *)[device newSharedEvent];
}

extern "C" void orblit_apple_event_signal(void *event, uint64_t value) {
  ((__bridge id<MTLSharedEvent>)event).signaledValue = value;
}

extern "C" uint64_t orblit_apple_event_value(void *event) {
  return ((__bridge id<MTLSharedEvent>)event).signaledValue;
}

extern "C" void orblit_apple_event_release(void *event) {
  (void)(__bridge_transfer id<MTLSharedEvent>)event;
}
