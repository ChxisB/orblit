#include "OrblitRendererInternal.h"

// Drawing one frame: the selection outline over it, the passes that make
// it, and what each of them cost.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

/// Draws the selection outline over the finished frame.
///
/// After every pass, whatever the graph was, because the outline belongs on
/// the picture as it will be seen: after tone mapping, so its colour is the
/// colour asked for, and after anti-aliasing, so it never enters a history
/// and never crawls. Nothing at all happens while nothing is highlighted.
void Renderer::drawOutline() {
  if (_outlineKeys.empty()) {
    if (_outline) _outline->setEntities({}, {});
    return;
  }

  std::vector<utils::Entity> primary;
  std::vector<utils::Entity> others;
  for (size_t i = 0; i < _outlineKeys.size(); i++) {
    auto found = _drawn.find(_outlineKeys[i]);
    if (found == _drawn.end()) continue;
    const Drawn &drawn = found->second;
    std::vector<utils::Entity> &into = i < _outlinePrimaryCount ? primary : others;
    // Every entity of a model, not only its root: the root of a glTF is a
    // transform and the geometry hangs below it.
    if (drawn.instance != nullptr) {
      const utils::Entity *entities = drawn.instance->getEntities();
      into.insert(into.end(), entities,
                  entities + drawn.instance->getEntityCount());
    } else if (drawn.entity) {
      into.push_back(drawn.entity);
    }
  }

  if (!_outline) _outline = std::make_unique<orblit::Outline>(*_engine);
  _outline->setStyle(_outlineStyle);
  _outline->setEntities(primary, others);
  _outline->render(*_renderer, *_scene, *_camera, _width, _height,
                   kAllLayers);
}

/// Draws every pass of the frame, in the order the graph put them in.
///
/// A graph nobody set is one pass, every layer, into the picture — which is
/// the frame this drew before there were passes at all, and is why a host
/// that has never heard of a graph pays nothing for one.
void Renderer::renderPasses() {
  if (_passes.empty()) {
    _view->setVisibleLayers(0xFF, kAllLayers);
    _renderer->render(_view);
    return;
  }

  prepareTargets();

  for (GraphPass &pass : _passes) {
    const double began = orblit::now();

    if (pass.kind == kPassEffect) {
      runEffect(pass, pass.into < 0 ? nullptr : &_targets[pass.into]);
    } else if (pass.into < 0) {
      _view->setVisibleLayers(0xFF, pass.layers);
      _renderer->render(_view);
    } else {
      GraphTarget &into = _targets[pass.into];
      // A target that could not be built is a pass that does not run. The
      // frame still draws, which is the difference between one broken
      // reflection and a black window.
      if (into.target != nullptr) {
        View *view = viewForPass(pass);
        view->setScene(_scene);
        view->setRenderTarget(into.target);
        view->setViewport({0, 0, into.builtWidth, into.builtHeight});
        view->setVisibleLayers(0xFF, pass.layers);
        aimPass(pass, into.builtWidth, into.builtHeight);
        _renderer->render(view);
      }
    }

    pass.milliseconds = (orblit::now() - began) * 1000.0;
  }
}

/// What each pass of the last frame cost, and how much it drew.
///
/// Read off the frame that has already happened rather than measured on
/// demand: asking a renderer to time itself when somebody looks changes what
/// is being timed.
std::vector<PassTiming> Renderer::passTimings() {
  std::vector<PassTiming> out;
  out.reserve(_passes.size());
  for (const GraphPass &pass : _passes) {
    out.push_back({pass.milliseconds, pass.drawn});
  }
  return out;
}

void Renderer::renderAtTime(double time) {
  if (_disposed) return;

  const double frameFrom = orblit::now();
  const uint64_t frameNumber = _frameCount;
  // Texture levels that have been decoded go up first, within this frame's
  // budget, so anything that finishes arriving is popped below this frame.
  pumpTextures();
  const double pumped = orblit::now();

  // Textures still arriving. Filament decodes them off this thread and hands
  // them over here, so this has to be called until it says it is done —
  // stopping early leaves an asset permanently half-textured.
  if (_loadingResources) {
    _resourceLoader->asyncUpdateLoad();
    if (_resourceLoader->asyncGetLoadProgress() >= 1.0f) {
      _loadingResources = false;
      if (_loadingFrom > 0) {
        orblit::log("[orblit] %s: %zu files decoded in %.0f ms",
              orblit::lastPathComponent(_loadingName).c_str(), _loadingResourceCount,
              (orblit::now() - _loadingFrom) * 1000);
        _loadingFrom = 0;
      }
    }
  }

  const double loaded = orblit::now();

  try {
    drawAtTime(time);
  } catch (const std::exception &error) {
    orblit::log("[orblit] render failed, stopping this viewport: %s", error.what());
    _disposed = true;
  } catch (...) {
    orblit::log("[orblit] render failed for an unknown reason.");
    _disposed = true;
  }

  if (_textureQueue) _textureQueue->frameTook(orblit::now() - frameFrom);
  if (_loadTrace && _textureQueue) {
    const double drawn = orblit::now();
    const double total = drawn - frameFrom;
    if (total > 0.025 || getenv("ORBLIT_LOAD_TRACE_ALL")) {
      const orblit::TextureQueue::Frames frames = _textureQueue->frames();
      orblit::log("[orblit] trace: frame %llu took %.1f ms: textures %.1f ms "
                  "(%u uploads, %llu KB), resource loader %.1f ms, draw %.1f "
                  "ms of which the backend %.1f ms",
                  (unsigned long long)frameNumber, total * 1000.0,
                  (pumped - frameFrom) * 1000.0, frames.lastUploads,
                  (unsigned long long)(frames.lastBytes / 1024),
                  (loaded - pumped) * 1000.0, (drawn - loaded) * 1000.0,
                  _lastFlushSeconds * 1000.0);
    }
  }
}

void Renderer::drawAtTime(double time) {


  // Resizing reallocates swap chains, which only the engine's own thread may
  // do, so a request from the UI thread is applied here instead of there.
  _presentLock.lock();
  bool needsResize = (_pendingWidth != _width || _pendingHeight != _height);
  uint32_t newWidth = _pendingWidth;
  uint32_t newHeight = _pendingHeight;
  _presentLock.unlock();

  if (needsResize) {
    _presentLock.lock();
    _presentedIndex = -1;  // nothing valid at the new size yet
    _presentLock.unlock();
    releaseBuffers();
    _width = newWidth;
    _height = newHeight;
    allocateBuffers();
    applyViewportSize();
  }

  placeCamera();
  // Straight after the camera, whose moment on the host's clock the clips are
  // sampled at, and before anything that reads where objects are.
  animate();
  // Motion blur hook: the camera this frame is drawn from, remembered
  // against the last frame's.
  if (_motionBlur) _motionBlur->frameBegan(*_camera);
  rangePopulations();
  // Each terrain's grids snap round the camera this frame sees from.
  if (_terrain != nullptr) _terrain->update(_camera->getPosition());
  // After the camera is placed, because the order depends on which way it
  // faces and what it can see. The sort itself is off this thread for any
  // cloud large enough to need it; this only asks for one and uploads
  // whichever has finished.
  if (_splats != nullptr) {
    const auto forward = _camera->getForwardVector();
    orblit::SplatCamera view;
    view.forward = float3{float(forward.x), float(forward.y), float(forward.z)};
    view.viewFromWorld = mat4f(_camera->getViewMatrix());
    view.clipFromView = mat4f(_camera->getProjectionMatrix());
    _splats->update(view);
  }

  SwapChain *target = _swapChains[_backIndex];
  if (!target) return;

  // The placeholder turns so an unconfigured viewport is visibly alive. A
  // scene sent by a host is left exactly where the host put it — a renderer
  // that quietly animates somebody's content is worse than a still one.
  if (!_sceneIsOwnedByHost) {
    auto found = _drawn.find(kPlaceholderKey);
    if (found != _drawn.end() && found->second.entity) {
      auto &transforms = _engine->getTransformManager();
      transforms.setTransform(
          transforms.getInstance(found->second.entity),
          mat4f::rotation(time * 0.7, float3{0, 1, 0}) *
              mat4f::rotation(time * 0.35, float3{1, 0, 0}));
    }
  }

  updateCloudsAtTime(time);
  updateMistAtTime(time);
  updateRainAtTime(time);
  pollTextures();
  // Before the frame begins: a stage of it renders standalone views of its
  // own, which Filament takes outside beginFrame and endFrame.
  pollEnvironment();
  pumpVideos();

  if (!_renderer->beginFrame(target)) return;
  // Inside the frame, and it has to be: a render outside begin/endFrame is
  // dropped without a word, which looks exactly like a capture that came back
  // black. Owed photographs first, then which probe the camera is standing
  // in, then the frame itself.
  captureOwedProbes();
  chooseProbe();
  // The surfaces read the atlas built up to last frame, so they are pointed
  // at it before anything is drawn.
  bindFieldEverywhere();
  // Before the scene, because the surfaces the scene draws read this. A map
  // rendered afterwards would be a frame behind, which for a light that moves
  // is a shadow that lags the thing casting it.
  renderAreaShadow();
  renderPasses();
  // After the scene, because what the field reads is the picture the scene
  // just made. The atlas it writes is therefore what next frame's surfaces
  // sample — one frame behind, which is what every temporal method trades.
  runField();
  // Last of all, over whatever the graph put on the screen.
  drawOutline();
  // Read back for a host that asked to see the frame. Inside the frame, and
  // it has to be: Filament reads a swap chain between the passes and
  // endFrame, and nowhere else.
  readBackIfAsked();
  _renderer->endFrame();

  // Flutter may sample the moment this returns, so the frame has to be on the
  // surface before it is advertised as presented.
  const double flushFrom = orblit::now();
  _engine->flushAndWait();
  _lastFlushSeconds = orblit::now() - flushFrom;

  // A frame read back arrives through Filament's callback queue, which is
  // only drained when somebody asks. Asking here makes it ready with the
  // frame rather than a frame later.
  bool pumping = false;
  {
    std::lock_guard<std::mutex> lock(_captureLock);
    pumping = _captureInFlight;
  }
  if (pumping) _engine->pumpMessageQueues();

  _presentLock.lock();
  _presentedIndex = _backIndex;
  _backIndex = (_backIndex + 1) % kOrblitBufferCount;
  _presentLock.unlock();

  // Debug aid: dumps exactly the buffer Flutter samples, which separates a
  // rendering fault from a handoff fault. Enabled by an environment variable
  // so it costs nothing when unset.
  if (_frameCount == 0) _startedAt = orblit::now();

  // How often the picture is drawn against how often it is told what to
  // draw. A camera that arrives at a different rate from the one it is drawn
  // at judders however smooth its own solution is.
  if (_pacing) {
    // What the frame actually cost the GPU, which is the number that matters:
    // how often it is presented is the display's business, and no amount of
    // headroom shows up there.
    const auto history = _renderer->getFrameInfoHistory(1);
    if (!history.empty() &&
        history[0].gpuFrameDuration > 0) {
      _gpuTotal += history[0].gpuFrameDuration / 1.0e6;
      _gpuCount++;
    }

    const double now = orblit::now();
    if (_pacedAt == 0) _pacedAt = now;
    // How far the camera moved between this frame and the last. Even motion
    // drawn evenly gives steps that are all the same size; a camera arriving
    // at a different rate from the one it is drawn at gives some frames two
    // steps and some none, which is what a judder is.
    // Not how big the steps are — a camera really does speed up and slow
    // down, and a figure of eight does it constantly. What judder is, is the
    // step changing from one frame to the next: real acceleration is smooth,
    // so consecutive steps differ by very little, while a camera arriving on
    // somebody else's clock gives one long step then a short one.
    // Per second, not per frame. Frames are not evenly spaced — the display
    // link wanders between sixty and eighty — so a camera moving perfectly
    // smoothly still covers different distances between them. Dividing by the
    // gap asks the only question that matters: was it going at an even speed.
    // Measured against when the camera was *sampled*, not when the frame was
    // presented. Those differ by however long the frame took to draw, and
    // dividing by the wrong one reports the renderer's own variation as if it
    // were the camera's.
    const float3 where = _camera->getPosition();
    const double gap = _placedAt - _placedWas;
    const float step = gap > 1e-6 ? float(length(where - _pacedFrom) / gap) : 0;
    _pacedFrom = where;
    if (_frameCount > 3 && gap > 1e-6) {
      _stepTotal += step;
      _jerkTotal += std::abs(step - _stepWas);
      _stepCount++;
    }
    _stepWas = step;

    if (_frameCount > 0 && _frameCount % 120 == 0) {
      const double over = now - _pacedAt;
      const float mean = _stepCount > 0 ? _stepTotal / _stepCount : 0;
      const float jerk = _stepCount > 0 ? _jerkTotal / _stepCount : 0;
      const float told =
          _toldCount > 1 ? _toldJerkTotal / (_toldCount - 1) : 0;
      const float toldMean = _toldCount > 0 ? _toldSpeedTotal / _toldCount : 0;
      orblit::log("[orblit] gpu %.2f ms (%.0f/s if unbound); %.1f drawn/s, "
            "%.1f camera/s; drawn unevenness %.0f%%, "
            "told unevenness %.0f%%, prediction saturated %.0f%% of frames",
            _gpuCount > 0 ? _gpuTotal / _gpuCount : 0,
            _gpuCount > 0 && _gpuTotal > 0 ? 1000.0 * _gpuCount / _gpuTotal : 0,
            120.0 / over, _cameraUpdates / over,
            mean > 0 ? 100.0 * jerk / mean : 0,
            toldMean > 0 ? 100.0 * told / toldMean : 0,
            _reachedCount > 0 ? 100.0 * _saturated / _reachedCount : 0);
      _gpuTotal = 0;
      _gpuCount = 0;
      _toldSpeedTotal = 0;
      _toldJerkTotal = 0;
      _toldCount = 0;
      _reachedTotal = 0;
      _reachedCount = 0;
      _saturated = 0;
      _pacedAt = now;
      _cameraUpdates = 0;
      _stepTotal = 0;
      _jerkTotal = 0;
      _stepCount = 0;
    }
  }

  // Which frame to catch. Sixty by default, because that is a second in and
  // everything has settled. A number picks that frame instead; the word
  // `flash` waits for a strike, which is the only way to catch one — a bolt
  // lasts a tenth of a second and lands on whichever frame it lands on.
  const char *dumpAt = getenv("ORBLIT_DUMP_FRAME");
  ++_frameCount;

  bool due = false;
  if (dumpAt) {
    if (strcmp(dumpAt, "flash") == 0) {
      due = _skyFlash > 0.5f && !_dumped;
    } else {
      const int wanted = atoi(dumpAt) > 1 ? atoi(dumpAt) : 60;
      due = _frameCount == wanted;
    }
  }

  if (due) {
    _dumped = true;
    // The steady-state cost of a frame, not the average since launch.
    //
    // This line used to divide the whole elapsed time by a hard-coded sixty,
    // which was wrong twice: it reported half the true cost whenever the dump
    // was asked for at frame thirty — which is what CI asks for — and even
    // with the right divisor it averaged in engine startup, the first frame's
    // shader compilation and the buffer allocation. An average polluted by
    // one-off costs cannot show a small regression, which is the only thing
    // anybody would use it for.
    //
    // What batching did rides on the same line rather than on one of its own,
    // because CI reads the first two "[orblit] frame" lines — this one and the
    // surface's "-> path" — and a third line in between would push the path
    // out of the log it prints. Renderables are what Filament culls and sorts
    // one at a time — every group's chunks among them — so with batching on
    // this is the count *after* merging, not before it: exact, because a
    // manually-instanced chunk is one renderable whether or not anything in
    // it is visible, unlike the old count-then-hope of automatic instancing.
    // Objects and groups say what was merged to get there: [batchedObjects]
    // renderables became [batchGroups] chunks, each of up to sixty-four.
    orblit::log("[orblit] frame %llu: cpu %.2f ms, gpu %.2f ms (median of recent), "
          "batching %s, prepass %s over %u, %zu renderables, "
          "%u objects in %u groups",
          static_cast<unsigned long long>(_frameCount), cpuMilliseconds(), gpuMilliseconds(),
          _batching ? "on" : "off", _depthPrepass ? "on" : "off",
          _prepassObjects, _scene->getRenderableCount(),
          _batchedObjects, _batchGroups);
    _surface->writeFrame(_presentedIndex);
  }
}
}  // namespace orblit
