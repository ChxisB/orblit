#include "OrblitRendererInternal.h"

// The environment: a cubemap, the light it casts, and the sky it shows.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

/// States how much of the frame's work actually happens.
///
/// One pipeline with dials, not a choice of pipelines. Everything here either
/// configures the view or is remembered for the lights, which read it when
/// they set their own shadow options — a cascade count is a property of the
/// light that casts, but nobody wants to set it per light.
/// Reads a cubemap `cmgen` baked, and the harmonics it wrote beside it.
///
/// Returns null and says why rather than throwing: an environment is an asset
/// somebody typed a path to, and a scene that refuses to draw because the
/// path was wrong is worse than one drawn by its lights alone.
Texture *Renderer::cubemapAtPath(const std::string &path, float3 *harmonics,
                                 bool *hasThose, const std::string &note) {
  *hasThose = false;

  const orblit::SharedBytes data = orblit::readResource(path);
  if (!data) {
    _assetNotes[note] = orblit::format("%s could not be read.",
                                      orblit::lastPathComponent(path).c_str());
    return nullptr;
  }

  // The bundle owns the pixels and has to outlive the upload, so it is handed
  // to createTexture along with the callback that frees it once the driver has
  // taken a copy. Freeing it here would be a race with the render thread.
  auto *bundle = new image::Ktx1Bundle(data->data(),
                                       static_cast<uint32_t>(data->size()));

  if (!bundle->isCubemap()) {
    _assetNotes[note] = orblit::format(
        "%s is not a cubemap. cmgen writes one; a flat image will not do.",
        orblit::lastPathComponent(path).c_str());
    delete bundle;
    return nullptr;
  }

  *hasThose = bundle->getSphericalHarmonics(harmonics);

  Texture *texture = ktxreader::Ktx1Reader::createTexture(
      _engine, *bundle, false,
      [](void *userdata) {
        delete static_cast<image::Ktx1Bundle *>(userdata);
      },
      bundle);
  if (texture == nullptr) {
    _assetNotes[note] =
        orblit::format("%s is not a KTX this build can read.",
                      orblit::lastPathComponent(path).c_str());
    delete bundle;
  }
  return texture;
}

void Renderer::setEnvironmentRadiance(const std::string &radiance, const std::string &skybox, const float *params) {
  if (_disposed || _engine == nullptr) return;

  const std::string wantedRadiance(radiance);
  const std::string wantedSkybox(skybox);
  // The fourth number is the size a picture is filtered at, so a change to it
  // is a change to what is built, not a number moving on what is there.
  const bool sameFiles = wantedRadiance == _environmentRadiancePath &&
                         wantedSkybox == _environmentSkyboxPath &&
                         params[3] == _environmentParams[3];

  // The numbers can move without the files changing — an environment being
  // turned, or brought up and down — and rebuilding a cubemap for that would
  // be reading a file off disk on every frame of a drag.
  if (sameFiles &&
      memcmp(params, _environmentParams, sizeof(_environmentParams)) == 0) {
    return;
  }

  // A picture still being filtered counts as there: when it arrives it is
  // built from the numbers as they are by then.
  const bool onlyNumbersMoved =
      sameFiles && (_environmentRadiance != nullptr || !_environmentWork.empty());
  memcpy(_environmentParams, params, sizeof(_environmentParams));

  if (onlyNumbersMoved) {
    rebuildEnvironmentLight();
    if (_environmentSkybox != nullptr) {
      _showingEnvironmentSkybox = params[2] != 0.0f;
      _scene->setSkybox(_showingEnvironmentSkybox ? _environmentSkybox
                                                  : _skybox);
    }
    return;
  }

  releaseEnvironment();
  _environmentRadiancePath = wantedRadiance;
  _environmentSkyboxPath = wantedSkybox;
  // What was said about the last files is not about these.
  _assetNotes.erase("environment");
  _assetNotes.erase("skybox");

  // An .hdr or .exr is a picture to filter here (OrblitEnvironment.cpp);
  // anything else is a cubemap cmgen baked, read as it always was.
  const bool radianceIsPicture =
      orblit::hdrFormatOfName(wantedRadiance) != orblit::HdrFormat::unknown;
  const bool skyboxIsPicture =
      orblit::hdrFormatOfName(wantedSkybox) != orblit::HdrFormat::unknown;

  if (!wantedRadiance.empty() && !radianceIsPicture) {
    float3 harmonics[9];
    bool hasHarmonics = false;
    _environmentRadiance = cubemapAtPath(radiance, harmonics, &hasHarmonics, "environment");
    if (_environmentRadiance != nullptr) {
      auto builder = IndirectLight::Builder();
      builder.reflections(_environmentRadiance);
      _environmentHasHarmonics = hasHarmonics;
      if (hasHarmonics) {
        for (int i = 0; i < 9; i++) _environmentHarmonics[i] = harmonics[i];
        // Three bands, which is what cmgen writes and what a diffuse
        // response actually needs: nine coefficients describe every low
        // frequency a matte surface can tell apart.
        builder.irradiance(3, harmonics);
      } else {
        _assetNotes["environment"] =
            "This cubemap has no baked harmonics, so nothing matte is lit by "
            "it. Bake it with cmgen rather than converting it by hand.";
      }
      _environmentLight = builder.intensity(_environmentParams[0])
                              .rotation(mat3f::rotation(_environmentParams[1],
                                                        float3{0, 1, 0}))
                              .build(*_engine);
    }
  }

  if (!wantedSkybox.empty() && !skyboxIsPicture) {
    float3 unused[9];
    bool ignored = false;
    _environmentSkyTexture = cubemapAtPath(skybox, unused, &ignored, "skybox");
    if (_environmentSkyTexture != nullptr) {
      _environmentSkybox = Skybox::Builder()
                               .environment(_environmentSkyTexture)
                               .showSun(false)
                               .build(*_engine);
      if (_environmentSkybox == nullptr) {
        _assetNotes["skybox"] =
            "The cubemap loaded but no backdrop could be built from it.";
      }
    }

  }

  if (radianceIsPicture || skyboxIsPicture) {
    // Lit at once if these pictures were filtered before; otherwise started,
    // and lit a few frames from now by pollEnvironment.
    requestEnvironmentImages(radianceIsPicture ? wantedRadiance : std::string(),
                             skyboxIsPicture ? wantedSkybox : std::string(),
                             _environmentParams[3]);
  }

  showEnvironment();
}

/// Puts whatever environment is built in charge of the scene's light and
/// backdrop, or the flat ambient and procedural sky back where there is none.
/// After the files change, and again when a picture finishes filtering.
void Renderer::showEnvironment() {
  if (_environmentLight != nullptr) {
    // The flat ambient steps aside rather than being blended with: a scene
    // lit by a photograph of a room and by an even wash is lit twice.
    if (_ambient != nullptr) {
      _engine->destroy(_ambient);
      _ambient = nullptr;
    }
    // A probe the camera is standing in stays in charge; chooseProbe hands
    // the scene back to this light when the camera leaves it.
    if (_activeProbe == 0) _scene->setIndirectLight(_environmentLight);
  } else {
    // Nothing loaded, so the sky the day cycle has been writing goes back.
    setAmbientColour(_ambientColour, _ambientIntensity);
  }

  _showingEnvironmentSkybox =
      _environmentSkybox != nullptr && _environmentParams[2] != 0.0f;

  if (_showingEnvironmentSkybox) {
    _scene->setSkybox(_environmentSkybox);
  } else if (_skybox != nullptr) {
    _scene->setSkybox(_skybox);
  }
}

/// Builds the indirect light again for a change of brightness or rotation.
///
/// Rather than mutated: an IndirectLight's intensity and rotation are fixed
/// when it is built. The cubemap behind it is not rebuilt, which is the
/// expensive half.
void Renderer::rebuildEnvironmentLight() {
  if (_environmentRadiance == nullptr) return;

  IndirectLight *previous = _environmentLight;

  auto builder = IndirectLight::Builder();
  builder.reflections(_environmentRadiance);
  // From the copy kept when the file was read. The bundle is long gone and
  // Filament does not hand harmonics back, so this is the only place they
  // survive.
  if (_environmentHasHarmonics) {
    builder.irradiance(3, _environmentHarmonics);
  }

  IndirectLight *rebuilt =
      builder.intensity(_environmentParams[0])
          .rotation(mat3f::rotation(_environmentParams[1], float3{0, 1, 0}))
          .build(*_engine);

  _scene->setIndirectLight(rebuilt);
  if (previous != nullptr) _engine->destroy(previous);
  _environmentLight = rebuilt;
}

/// Gives back everything an environment was holding.
void Renderer::releaseEnvironment() {
  if (_engine == nullptr) return;

  if (_environmentLight != nullptr) {
    _scene->setIndirectLight(nullptr);
    _engine->destroy(_environmentLight);
    _environmentLight = nullptr;
  }
  if (_environmentSkybox != nullptr) {
    if (_skybox != nullptr) _scene->setSkybox(_skybox);
    _engine->destroy(_environmentSkybox);
    _environmentSkybox = nullptr;
  }
  _showingEnvironmentSkybox = false;
  // A texture filtered from a picture belongs to the cache, which keeps it
  // for the next scene that names the same picture.
  if (_environmentRadiance != nullptr) {
    if (!_environmentRadianceCached) _engine->destroy(_environmentRadiance);
    _environmentRadiance = nullptr;
  }
  if (_environmentSkyTexture != nullptr) {
    if (!_environmentSkyCached) _engine->destroy(_environmentSkyTexture);
    _environmentSkyTexture = nullptr;
  }
  _environmentRadianceCached = false;
  _environmentSkyCached = false;
  _environmentRadiancePath.clear();
  _environmentSkyboxPath.clear();
  _environmentHasHarmonics = false;
}
}  // namespace orblit
