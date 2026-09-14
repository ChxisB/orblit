#pragma once

// One Filament engine drawing into one Flutter texture -- the Windows
// counterpart to ../linux/orblit_viewport.h, OrblitViewport.kt on Android and
// Viewport in OrblitFilamentPlugin.swift.
//
// ## How a frame reaches Flutter
//
// Through a flutter::PixelBufferTexture: the renderer draws into an offscreen,
// readable swap chain (ORBLIT_SURFACE_HEADLESS), the frame is read back with
// the ABI's own capture calls, and Flutter copies those bytes into a texture
// of its own. That is a full copy of the frame every time, which macOS, iOS
// and Android all avoid -- Apple hands over an IOSurface, Android a
// SurfaceProducer's buffer -- so it is worth saying why it is the right
// answer here rather than a placeholder.
//
// Flutter's Windows embedder offers two texture kinds. The copy-free one is a
// GPU surface, and it comes in two flavours, neither of which this can use
// today:
//
//   - kFlutterDesktopGpuSurfaceTypeD3d11Texture2D hands the embedder an
//     ID3D11Texture2D directly, but the embedder renders through ANGLE and
//     ANGLE will only accept a texture created on *its own* D3D11 device --
//     which it does not expose. Filament creates its own device, so the two
//     are always different and this flavour is closed to any plugin that
//     brings its own renderer.
//   - kFlutterDesktopGpuSurfaceTypeDxgiSharedHandle does cross devices: a
//     texture made with D3D11_RESOURCE_MISC_SHARED, handed over as the HANDLE
//     from IDXGIResource::GetSharedHandle. It is the route this should
//     eventually take. It is not taken now on the strength of a reported
//     crash when such textures are presented under Impeller, which is the
//     rasteriser this package's Flutter (>= 3.47) is understood to default
//     to on Windows.
//
//     That last claim is worth stating precisely, because it is the weakest
//     link in this argument and it has not been checked on a running
//     machine. What *is* checked: 3.47's flutter_windows.h carries
//     FlutterDesktopEngineProperties::impeller_switch, whose values are
//     DefaultImpeller/EnabledImpeller/DisabledImpeller, so the choice is the
//     embedder's and the default lives inside flutter_windows.dll rather
//     than anywhere the tooling or this build can read; the runner reaches
//     the engine through flutter::DartProject and never sets that switch, so
//     it takes whatever that default is; and the SDK's own changelog records
//     neither Impeller becoming the Windows default nor any fix to
//     shared-handle presentation. So the premise is unrefuted rather than
//     confirmed, and anyone picking this up should verify it on a real
//     machine before spending money on the shared-handle path.
//
//     Note that it is not load-bearing on its own. The D3d11Texture2D
//     flavour above is shut by ANGLE's device ownership whatever Impeller
//     does, which leaves shared handles the only copy-free route; so even if
//     the crash were fixed tomorrow, taking it still means writing the D3D11
//     device, the shared texture and the blit that this pixel-buffer path
//     does not need.
//
// A pixel buffer depends on none of that: it works under Impeller and the
// older rasteriser alike, on whichever backend Filament actually started, and
// it is what makes the gallery draw on Windows at all. The copy is the price,
// and it is the same price ../linux/ pays for its own (different) reason.
//
// Getting off it means, in order: a D3D11 device of Filament's own to make
// the shared texture on, a blit from the swap chain into it, and evidence
// that the Impeller crash above is fixed -- at which point only this file and
// its .cpp change, because nothing above the texture knows which kind it is.
//
// ## Threading
//
// The frame loop runs on the platform thread, where every method call also
// arrives, so the renderer is driven from one thread exactly as Android's and
// Linux's are -- which is what the C ABI asks for (see orblit_renderer.h:
// one thread drives a renderer). There is no GLib main loop here to hang a
// timeout on, so the Win32 equivalent is used: a message-only window owned by
// this viewport, with a WM_TIMER on it. The runner's own GetMessage/
// DispatchMessage loop pumps it, so the timer fires on the platform thread
// and nothing needs a lock to talk to the renderer.
//
// The copy callback is the exception: Flutter calls it on its own render
// thread. So the pixels are published under a lock into one of three slots
// and handed out by pointer -- three rather than two because the embedder
// takes the buffer's contents *after* the callback returns and needs it
// intact until its next tick, so the slot Flutter last saw must not be the
// one the next frame is written into.
//
// That callback outliving this object is a real possibility and not a
// theoretical one: UnregisterTexture is asynchronous on this embedder, so a
// copy already in flight can land after the viewport has gone. Rather than
// block the platform thread waiting for it, the pixels and their lock live in
// a Shared block held by std::shared_ptr, which the callback captures by
// value. A late callback then finds a live object that tells it there is
// nothing to show, instead of a dangling `this`.

#include <flutter/texture_registrar.h>
#include <windows.h>

#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <utility>
#include <vector>

#include "orblit_renderer.h"
#include "orblit_scene.h"

namespace orblit_windows {

class Viewport {
 public:
  // Null if Filament would not start -- no backend (Vulkan, then OpenGL) was
  // available -- with the reason already logged by the renderer.
  static std::unique_ptr<Viewport> Start(flutter::TextureRegistrar* registrar,
                                         uint32_t width, uint32_t height);

  ~Viewport();

  Viewport(const Viewport&) = delete;
  Viewport& operator=(const Viewport&) = delete;

  int64_t texture_id() const { return texture_id_; }

  void Resize(uint32_t width, uint32_t height);
  void ApplyScene(const Scene& scene);

  // What the scene asked for that could not be given, as about/saying pairs.
  std::vector<std::pair<std::string, std::string>> Notes() const;

  double GpuMilliseconds() const;
  // Interleaved [ms0, drawn0, ms1, drawn1, ...], one pair per pass, in the
  // order they ran -- the shape OrblitView.capture() already reads.
  std::vector<double> PassTimings() const;
  // [batchedObjects, batchGroups].
  std::vector<int32_t> Batching() const;

 private:
  Viewport() = default;

  // The pixels, and the only part of a viewport that Flutter's render thread
  // ever touches. Held by shared_ptr so a copy callback that arrives after
  // the viewport is gone still has something valid to ask -- see the header
  // comment above.
  struct Shared {
    std::mutex lock;
    // Three rotating slots: the bytes, and the descriptor handed to the
    // embedder for each. The descriptor is per-slot for the same reason the
    // bytes are -- the embedder reads it after the callback returns, so the
    // one it was given last must not be rewritten by the next frame.
    std::vector<uint8_t> pixels[3];
    FlutterDesktopPixelBuffer buffers[3] = {};
    int front = -1;
    // False once the viewport has been torn down; a callback then reports
    // that there is no frame rather than reading a slot being freed.
    bool alive = true;
  };

  // One frame: draw, ask for it back, publish whatever has arrived.
  void OnFrame();

  // ORBLIT_DUMP_FRAME's other half. The renderer prints the frame's cost and
  // then asks its surface to write the picture; a headless surface has no
  // file to write, and this is the only layer that holds the pixels, so the
  // picture is written here instead -- including the "-> path : written"
  // line tool/ci_draw_frame_windows.sh waits for.
  void WriteFrameIfAsked(const uint8_t* rgba, uint32_t width, uint32_t height);

  static LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wparam,
                                     LPARAM lparam);

  orblit_renderer* renderer_ = nullptr;
  flutter::TextureRegistrar* registrar_ = nullptr;  // borrowed
  std::unique_ptr<flutter::TextureVariant> texture_;
  int64_t texture_id_ = -1;

  // The message-only window the frame timer is hung on, and the timer itself.
  HWND timer_window_ = nullptr;
  UINT_PTR timer_ = 0;

  LARGE_INTEGER started_ = {};
  LARGE_INTEGER frequency_ = {};
  int frame_count_ = 0;
  bool dumped_ = false;

  int next_slot_ = 0;
  std::shared_ptr<Shared> shared_ = std::make_shared<Shared>();
};

}  // namespace orblit_windows
