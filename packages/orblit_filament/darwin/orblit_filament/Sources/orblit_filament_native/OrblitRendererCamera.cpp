#include "OrblitRendererInternal.h"

// Post-processing, grading, fog, and where the camera stands.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

void Renderer::setPostProcess(const float *params, size_t count) {
  if (_disposed || params == nullptr) return;

  // The same numbers as last frame mean the same view, and setting an option
  // struct makes Filament rebuild internal state. Comparing forty floats is
  // cheaper than doing that sixty times a second to say nothing changed.
  if (count == _postCount &&
      memcmp(params, _postParams, count * sizeof(float)) == 0) {
    return;
  }
  if (count > kMaxPostParams) count = kMaxPostParams;
  memcpy(_postParams, params, count * sizeof(float));
  _postCount = count;

  size_t at = 0;
  auto next = [&]() -> float { return at < count ? params[at++] : 0.0f; };
  auto flag = [&]() -> bool { return next() > 0.5f; };

  const bool on = flag();
  const int antiAliasing = (int)next();
  const bool dither = flag();

  _view->setPostProcessingEnabled(on);
  // Everything below still runs when post-processing is off; Filament simply
  // ignores it. Reading the whole array either way keeps the offsets in one
  // place rather than in two.

  BloomOptions bloom;
  bloom.enabled = flag() && on;
  bloom.strength = next();
  bloom.levels = (uint8_t)std::clamp((int)next(), 1, 11);
  bloom.threshold = flag();
  bloom.lensFlare = flag();
  const float flareStrength = next();
  // Filament folds the flare into the bloom chain, so its own strength is the
  // ghost spacing and chromatic split rather than a separate amount. A flare
  // asked for at nothing is a flare turned off.
  if (flareStrength <= 0.0f) bloom.lensFlare = false;
  _view->setBloomOptions(bloom);

  DepthOfFieldOptions dof;
  dof.enabled = flag() && on;
  const float focus = next();
  dof.cocScale = next();
  dof.maxForegroundCOC = next();
  dof.maxBackgroundCOC = next();
  _view->setDepthOfFieldOptions(dof);
  // Where the sharp plane is belongs to the camera rather than to the effect:
  // it is the lens focusing, and the same distance means the same shot
  // whether or not the blur is switched on.
  if (dof.enabled && focus > 0.0f) {
    _camera->setFocusDistance(focus);
  }

  VignetteOptions vignette;
  vignette.enabled = flag() && on;
  vignette.midPoint = next();
  vignette.roundness = next();
  vignette.feather = next();
  const float vr = next();
  const float vg = next();
  const float vb = next();
  vignette.color = LinearColorA{vr, vg, vb, 1.0f};
  _view->setVignetteOptions(vignette);

  AmbientOcclusionOptions occlusion;
  occlusion.enabled = flag() && on;
  occlusion.radius = next();
  occlusion.intensity = next();
  occlusion.bias = next();
  const int aoQuality = std::clamp((int)next(), 0, 3);
  occlusion.quality = (QualityLevel)aoQuality;
  occlusion.bentNormals = flag();
  _view->setAmbientOcclusionOptions(occlusion);

  ScreenSpaceReflectionsOptions reflections;
  reflections.enabled = flag() && on;
  reflections.thickness = next();
  reflections.bias = next();
  reflections.maxDistance = next();
  reflections.stride = std::max(1.0f, next());
  _view->setScreenSpaceReflectionsOptions(reflections);

  const bool grading = flag();
  const int toneMapping = (int)next();
  const float exposure = next();
  const float contrast = next();
  const float saturation = next();
  const float vibrance = next();
  const float temperature = next();
  const float tint = next();
  float shadows[3], midtones[3], highlights[3];
  for (int i = 0; i < 3; ++i) shadows[i] = next();
  for (int i = 0; i < 3; ++i) midtones[i] = next();
  for (int i = 0; i < 3; ++i) highlights[i] = next();

  // Only when the grading numbers themselves have moved. A ColorGrading is a
  // baked lookup table rather than a struct of numbers, so rebuilding one
  // because somebody nudged the bloom would be a 32-cubed texture built to say
  // the colour did not change.
  const size_t gradingFrom = at - 8 - 9;
  const bool gradingMoved =
      _colorGrading == nullptr ||
      memcmp(params + gradingFrom, _gradingParams,
             (8 + 9) * sizeof(float)) != 0;
  if (gradingMoved) {
    memcpy(_gradingParams, params + gradingFrom, (8 + 9) * sizeof(float));
  }

  if (gradingMoved)
    applyGrading(grading, toneMapping, exposure, contrast, saturation, vibrance, temperature, tint, shadows, midtones, highlights);

  // Temporal sampling needs the history buffer that only its own option turns
  // on, so the two settings have to agree.
  TemporalAntiAliasingOptions taa;
  taa.enabled = on && antiAliasing == 2;
  _view->setTemporalAntiAliasingOptions(taa);
  _view->setAntiAliasing(on && antiAliasing == 1 ? AntiAliasing::FXAA
                                                 : AntiAliasing::NONE);
  _view->setDithering(dither ? Dithering::TEMPORAL : Dithering::NONE);
}

/// Builds the colour grading, and keeps the one it built.
///
/// A ColorGrading is an engine resource with a lookup table baked into it, not
/// a struct of numbers — building one per frame would be a 32-cubed texture
/// per frame. This makes a new one only when the numbers have moved.
void Renderer::applyGrading(bool enabled, int toneMapper, float exposure, float contrast, float saturation, float vibrance, float temperature, float tint, const float *shadows, const float *midtones, const float *highlights) {
  ColorGrading::Builder builder;

  // Tone mapping happens whether or not the rest of the grading is on:
  // something has to decide how light becomes pixels, and a clip at one is a
  // worse answer than a curve.
  switch (toneMapper) {
    case 1: builder.toneMapping(ColorGrading::ToneMapping::ACES); break;
    case 2: builder.toneMapping(ColorGrading::ToneMapping::ACES_LEGACY); break;
    case 3: builder.toneMapping(ColorGrading::ToneMapping::LINEAR); break;
    case 4: builder.toneMapping(ColorGrading::ToneMapping::LINEAR); break;
    default: builder.toneMapping(ColorGrading::ToneMapping::FILMIC); break;
  }

  if (enabled) {
    builder.exposure(exposure)
        .contrast(contrast)
        .saturation(saturation)
        .vibrance(vibrance)
        .whiteBalance(temperature, tint)
        // One call for all three, which is how Filament has it: the three
        // ranges overlap and the fourth argument is where they meet, so
        // setting one without the others would be setting half a decision.
        .shadowsMidtonesHighlights(
            {shadows[0], shadows[1], shadows[2], 0.0f},
            {midtones[0], midtones[1], midtones[2], 0.0f},
            {highlights[0], highlights[1], highlights[2], 0.0f},
            // The defaults: shadows fade out by a fifth of the range and
            // highlights come in at two thirds.
            {0.0f, 0.333f, 0.550f, 1.0f});
  }

  ColorGrading *built = builder.build(*_engine);
  if (built == nullptr) return;

  _view->setColorGrading(built);
  // Destroyed after the new one is in place, since the view was still holding
  // it a line ago and Filament reads it on the driver thread.
  if (_colorGrading != nullptr) _engine->destroy(_colorGrading);
  _colorGrading = built;
}

void Renderer::setFogEnabled(bool enabled, const float *params) {
  if (_disposed) return;

  FogOptions fog;
  fog.enabled = enabled;
  fog.color = LinearColor{params[0], params[1], params[2]};
  fog.density = params[3];
  fog.distance = params[4];
  // Never past the sky.
  //
  // Filament fogs everything in the view, and the sky is geometry like
  // anything else: a dome at nine hundred metres inside fog thick enough to
  // hide a valley is a dome nobody can see. Fog that reaches it turns the
  // whole frame into one flat grey, which is exactly what it did.
  //
  // Clamped here rather than asked of every caller, because a caller who
  // forgets does not get a subtly wrong sky, they get no sky at all.
  fog.cutOffDistance = std::min(params[5], kSkyRadius - 40.0f);
  fog.maximumOpacity = params[6];
  fog.height = params[7];
  fog.heightFalloff = params[8];
  _view->setFogOptions(fog);

  // Two things that are one thing. Filament's fog is the air between here and
  // the horizon — even, and right for distance. What it cannot do is have
  // shape: no amount of it looks like a bank of cloud lying in a valley,
  // because every cubic metre of it is the same as every other. The sheets
  // are that shape, and they sit inside the same haze rather than instead of
  // it.
  const float structure = params[9];
  const bool showing = enabled && structure > 0 && params[3] > 0;

  if (showing) {
    buildMist();

    // Ten sheets, each mostly transparent. What is seen is what they add up
    // to — one minus the light that gets through all of them — so each one has
    // to be far thinner than the bank as a whole. Sheets thick enough to read
    // on their own are sheets you can count.
    const float alpha = std::min(params[3] * 1.6f, 0.35f) * structure;

    // Wind arrives in metres a second and the noise is sampled in turns per
    // metre, so the rate the pattern scrolls at is the product of the two.
    // Negative because moving where the noise is read from backwards is what
    // moves the cloud forwards.
    const float2 wind = float2{params[10], params[11]};
    const float2 drift = -wind * params[12];

    for (MaterialInstance *instance : _mistInstances) {
      instance->setParameter("colour",
                             float3{params[0], params[1], params[2]});
      instance->setParameter("density", alpha);
      instance->setParameter("scale", params[12]);
      instance->setParameter("drift", drift);
      // Smooth haze at one end and torn wisps at the other, which is the
      // difference between weather and a filter over the lens.
      instance->setParameter("contrast", 1.5f + structure * 5.0f);
    }

    _mistHeight = params[7];
    _mistThickness = params[13];

    if (!_mistShowing) {
      for (utils::Entity entity : _mistEntities) _scene->addEntity(entity);
    }
  } else if (_mistShowing) {
    // Taken out of the scene rather than destroyed: turning the weather off
    // and on again is a slider, and rebuilding ten renderables under a
    // dragging finger would stutter.
    for (utils::Entity entity : _mistEntities) _scene->remove(entity);
  }

  _mistShowing = showing;
}

void Renderer::setSkyColour(const float *colour, float ambient, bool showBody) {
  if (_disposed) return;

  const float3 sky = {colour[0], colour[1], colour[2]};
  const bool sameColour = _skyBuilt && sky.x == _skyColour.x &&
                          sky.y == _skyColour.y && sky.z == _skyColour.z;

  // Whether the sun's disk is drawn is fixed when a skybox is built, so only
  // that forces a new one. A colour is a setter, and a day cycle changing the
  // sky on every frame should cost one.
  if (!_skyBuilt || showBody != _skyShowsBody) {
    // Taken out of the scene only if it is the one the scene is showing. This
    // runs after the environment, so the scene's backdrop is often the
    // photographed sky, and clearing it unconditionally took that away with
    // nothing put back: the flat sky is not installed over an environment
    // below, and the dome stands aside for one. Bistro going from night to day
    // was that — the photograph back, the sun's disk back on, and a sky that
    // no longer drew at all, so every frame of the walk stayed printed on it.
    if (_skybox) {
      if (_scene->getSkybox() == _skybox) _scene->setSkybox(nullptr);
      _engine->destroy(_skybox);
    }
    _skybox = Skybox::Builder()
                  .color({sky.x, sky.y, sky.z, 1.0f})
                  .showSun(showBody)
                  .build(*_engine);
    // Kept, but not shown over an environment that is already the backdrop.
    // This runs after the environment on every publish, so installing it
    // unconditionally is a photographed sky replaced by a flat colour on the
    // frame after it loads — with nothing in the notes to say why.
    if (!_showingEnvironmentSkybox) _scene->setSkybox(_skybox);
    _skyShowsBody = showBody;
  } else if (!sameColour) {
    _skybox->setColor({sky.x, sky.y, sky.z, 1.0f});
  }

  // Lit by the sky it stands under, which is what makes the two read as one
  // environment rather than a backdrop behind an unrelated scene.
  //
  // The irradiance is fixed when an indirect light is built, so a change of
  // colour is a new one; a change of only its strength is a setter. Under a
  // day cycle both move together, and this object holds nine floats — the
  // rebuild is the cheap kind.
  if (!_skyBuilt || !sameColour) {
    setAmbientColour(sky, ambient);
  } else if (ambient != _skyAmbient && _ambient) {
    _ambient->setIntensity(ambient);
  }

  _skyColour = sky;
  _skyAmbient = ambient;
  _skyBuilt = true;
}

void Renderer::setCameraPosition(const float *position, const float *target, float fieldOfView, bool orthographic, float viewHeight, double at) {
  if (_disposed) return;

  // Recorded, not applied.
  //
  // Two reasons, and the second one is a bug rather than a preference. The
  // first: this arrives on whatever clock the application runs on, and the
  // picture is drawn on the display's — two loops at similar but unequal
  // rates, so some frames were drawn twice with the same camera and some
  // skipped a whole word. Measured on a camera following a moving subject,
  // that is frames where the camera did not move at all next to frames where
  // it moved eight times as far, which is exactly what a judder is. The
  // picture now works out where the camera is at the moment it is drawn.
  //
  // The second: Filament's camera is not safe to touch from two threads, and
  // this is the platform thread while the engine's own thread is reading it.
  Aimed aimed;
  aimed.position = {position[0], position[1], position[2]};
  aimed.target = {target[0], target[1], target[2]};
  aimed.fieldOfView = fieldOfView;
  aimed.orthographic = orthographic;
  aimed.viewHeight = viewHeight;
  aimed.at = at;
  aimed.arrived = orblit::now();
  aimed.valid = true;

  _aimLock.lock();
  _aimedWas = _aimedNow;
  _aimedNow = aimed;
  _aimLock.unlock();

  // Motion blur hook: the moment this publish describes, on the host's own
  // clock. It commits the objects the publish placed, and is what turns
  // their two positions into a speed.
  if (_motionBlur) _motionBlur->stamp(at);

  // What the application itself is producing, before anything here touches
  // it. If its own motion is uneven then no amount of sampling will be even,
  // and the fault is on the other side of the message.
  if (_pacing && _toldAt > 0) {
    const double over = at - _toldAt;
    if (over > 1e-5 && over < 0.25) {
      const float speed = float(length(aimed.position - _toldFrom) / over);
      if (_toldCount > 0) _toldJerkTotal += std::abs(speed - _toldSpeedWas);
      _toldSpeedTotal += speed;
      _toldSpeedWas = speed;
      _toldCount++;
    }
  }
  _toldFrom = aimed.position;
  _toldAt = at;

  _cameraUpdates++;
}

/// Puts the camera where it should be at this instant.
///
/// Called once a frame, on the thread that draws. Between two words the
/// camera carries on at the speed those two implied, which turns a set of
/// steps arriving on somebody else's clock into a continuous motion sampled
/// on this one.
void Renderer::placeCamera() {
  _aimLock.lock();
  const Aimed now = _aimedNow;
  const Aimed was = _aimedWas;
  _aimLock.unlock();

  if (!now.valid) return;

  const double span = now.at - was.at;

  // A gap that long is not a rate, it is a pause — the application was busy,
  // or has only just started. Starting from it would fling the camera.
  if (!was.valid || span <= 1e-5 || span >= 0.25) {
    _movingKnown = false;
    _spanUsual = 0;
    _carriedPosition = {0.0f, 0.0f, 0.0f};
    _carriedTarget = {0.0f, 0.0f, 0.0f};
    _spokeAt = now.at;
    _spokePosition = now.position;
    _spokeTarget = now.target;
    _fieldOfView = now.fieldOfView;
    _camera->lookAt(now.position, now.target, {0, 1, 0});
    projectWith(now.fieldOfView, now.orthographic, now.viewHeight);
    // Paused, or held: the moment drawn is the moment stated, exactly.
    _drawnHostSeconds = now.at;
    _drawnHostSecondsKnown = true;
    return;
  }

  // The usual gap between words, followed. Everything below is measured in
  // these rather than in the last gap, which is far too noisy to steer by.
  _spanUsual = _spanUsual <= 0 ? span : _spanUsual + (span - _spanUsual) * 0.1;

  // Where the two clocks stand relative to each other, followed slowly.
  //
  // Each word carries the application's own time and arrives at some moment
  // here, and the difference is how far apart the clocks read. That difference
  // is steady; no single measurement of it is, because messages do not arrive
  // evenly. Following it slowly gives a reading that moves smoothly, which is
  // the whole point — a jumpy answer here would put the judder straight back.
  const double reading = now.at - now.arrived;
  if (!_clocksAligned || std::abs(reading - _clockOffset) > 0.25) {
    _clockOffset = reading;
    _clocksAligned = true;
  } else {
    _clockOffset += (reading - _clockOffset) * 0.05;
  }

  const double appNow = orblit::now() + _clockOffset;

  // How far from a given word the moment being drawn is.
  //
  // One expression, used both to draw and to work out how wrong the last
  // prediction was. Two nearly-identical versions of this is how a correction
  // ends up adding error instead of removing it.
  const double behind = -std::max(span, _spanUsual);
  const double reach = kCarryOn * _spanUsual;
  const auto leadFrom = [&](double word) {
    return float(std::clamp(
        appNow - word - kDrawBehind * _spanUsual, behind, reach));
  };

  if (now.at != _spokeAt) {
    // What would have been drawn this instant on the strength of the last
    // word, so the difference can be carried rather than appearing as a jump.
    const float wasLead = leadFrom(_spokeAt);
    const float3 wouldBe = _spokePosition + _aimVelocity * wasLead;
    const float3 wouldLook = _spokeTarget + _lookVelocity * wasLead;

    // How fast it is going, followed rather than taken fresh each time.
    //
    // Two positions and the time between them is a speed, and a noisy one:
    // the application's frames are not evenly spaced either, so a gap that
    // happens to be half the usual makes the speed twice the truth. Following
    // the estimate settles in about three words — fast enough to keep up with
    // a camera that is genuinely accelerating, slow enough to ignore the
    // timing noise underneath it.
    const float over = float(span);
    const float3 aimStep = (now.position - was.position) / over;
    const float3 lookStep = (now.target - was.target) / over;
    const float lensStep = (now.fieldOfView - was.fieldOfView) / over;

    if (!_movingKnown) {
      _aimVelocity = aimStep;
      _lookVelocity = lookStep;
      _lensVelocity = lensStep;
      _movingKnown = true;
    } else {
      constexpr float follow = 0.35f;
      _aimVelocity += (aimStep - _aimVelocity) * follow;
      _lookVelocity += (lookStep - _lookVelocity) * follow;
      _lensVelocity += (lensStep - _lensVelocity) * follow;
    }

    const float nowLead = leadFrom(now.at);
    _carriedPosition += wouldBe - (now.position + _aimVelocity * nowLead);
    _carriedTarget += wouldLook - (now.target + _lookVelocity * nowLead);

    _spokeAt = now.at;
    _spokePosition = now.position;
    _spokeTarget = now.target;
  }

  // The carried difference fades over a few frames rather than all at once.
  // It is the difference that decays, not the position, so the camera still
  // arrives exactly where it was told rather than trailing behind.
  const double drawnAt = orblit::now();
  const double gap = _placedAt > 0 ? drawnAt - _placedAt : 0;
  _placedWas = _placedAt;
  _placedAt = drawnAt;
  const float keep = float(std::exp(-std::max(gap, 0.0) / kAbsorb));
  _carriedPosition *= keep;
  _carriedTarget *= keep;

  const float by = leadFrom(now.at);
  _drawnHostSeconds = now.at + double(by);
  _drawnHostSecondsKnown = true;

  if (_pacing) {
    _reachedCount++;
    if (appNow - now.at - kDrawBehind * _spanUsual >= reach) _saturated++;
  }

  // Read from the latest word at the speed the last few implied, rather than
  // by interpolating between the last two.
  //
  // Interpolating is the obvious thing and it is worse — measurably, by three
  // times. The two words either side are irregularly spaced, so dividing by
  // the gap between them turns their timing noise straight into speed, which
  // is the thing being got rid of. A followed speed has that noise taken out
  // of it already.
  const float3 position = now.position + _aimVelocity * by + _carriedPosition;
  const float3 target = now.target + _lookVelocity * by + _carriedTarget;
  const float fieldOfView = now.fieldOfView + _lensVelocity * by;

  _fieldOfView = fieldOfView;
  _orthographic = now.orthographic;
  _viewHeight = now.viewHeight;
  _camera->lookAt(position, target, {0, 1, 0});
  projectWith(fieldOfView, now.orthographic, now.viewHeight);
}

/// Sets how the camera turns the world into a picture.
///
/// The two kinds do not blend into one another — halfway between a flat view
/// and one with perspective is not a view of anything — so a camera that
/// changes kind changes it outright, and only the numbers move.
void Renderer::projectWith(float fieldOfView, bool orthographic, float tall) {
  const double aspect = double(_width) / double(_height);

  if (orthographic) {
    const double half = std::max(tall, 0.001f) * 0.5;
    const double wide = half * aspect;
    // The near plane still has to be in front of the camera. It is tempting to
    // put it behind — nothing gets larger as it approaches a flat view, so a
    // negative near is geometrically fine — but the depth buffer is not
    // geometry: a range spanning zero maps depths onto each other, and then
    // the sky wins against the ground and the whole frame is sky.
    _camera->setProjection(Camera::Projection::ORTHO, -wide, wide, -half, half,
                           0.1, 4000.0);
    return;
  }

  _camera->setProjection(fieldOfView > 0 ? fieldOfView : 50.0, aspect, 0.1,
                         1000.0, fovAxisFor(aspect));
}

void Renderer::setExposure(float aperture, float shutter, float sensitivity) {
  if (_disposed) return;
  // Filament asks for these in the units a photographer would state them in,
  // which is also how they arrive, so there is nothing to convert.
  _camera->setExposure(aperture, shutter, sensitivity);
}
}  // namespace orblit
