#include "OrblitRendererInternal.h"

// The world's backdrop: the sky, and the mist, cloud and rain hung in
// front of it.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

void Renderer::buildGeometry() {
  // Filament wants tangent frames as quaternions, so the flat face normals are
  // converted rather than handed over directly.
  quatf quats[24];
  auto *orientation = geometry::SurfaceOrientation::Builder()
                          .vertexCount(24)
                          .normals(kNormals)
                          .build();
  orientation->getQuats(quats, 24);
  delete orientation;

  // Heap, not stack, and freed by the descriptor's callback. Filament does not
  // copy vertex data — it holds the pointer until its driver thread performs
  // the upload, which happens after this method has returned. A stack array
  // here is read back as whatever later occupied the frame: the cube arrives
  // with garbage positions and garbage tangent frames, so it renders as an
  // unlit wedge rather than a lit cube.
  auto *vertices = new Vertex[24];
  for (int i = 0; i < 24; i++) {
    // Box mapping, taken from the face's own normal: whichever axis the face
    // points along is the one left out, and the other two become the corner's
    // place on it. Six faces, each covering the whole image once.
    const float3 at = kPositions[i];
    const float3 normal = kNormals[i];
    float2 uv;
    if (std::fabs(normal.y) > 0.5f) {
      uv = {at.x, at.z};
    } else if (std::fabs(normal.x) > 0.5f) {
      uv = {at.z, at.y};
    } else {
      uv = {at.x, at.y};
    }
    vertices[i] = {at, quats[i], {uv.x * 0.5f + 0.5f, uv.y * 0.5f + 0.5f}};
  }

  _vertexBuffer =
      VertexBuffer::Builder()
          .vertexCount(24)
          .bufferCount(1)
          .attribute(VertexAttribute::POSITION, 0,
                     VertexBuffer::AttributeType::FLOAT3,
                     offsetof(Vertex, position), sizeof(Vertex))
          .attribute(VertexAttribute::TANGENTS, 0,
                     VertexBuffer::AttributeType::FLOAT4,
                     offsetof(Vertex, tangents), sizeof(Vertex))
          .attribute(VertexAttribute::UV0, 0,
                     VertexBuffer::AttributeType::FLOAT2,
                     offsetof(Vertex, uv), sizeof(Vertex))
          .build(*_engine);
  _vertexBuffer->setBufferAt(
      *_engine, 0,
      VertexBuffer::BufferDescriptor(
          vertices, sizeof(Vertex) * 24,
          [](void *buffer, size_t, void *) {
            delete[] static_cast<Vertex *>(buffer);
          }));

  _indexBuffer = IndexBuffer::Builder()
                     .indexCount(36)
                     .bufferType(IndexBuffer::IndexType::USHORT)
                     .build(*_engine);
  // kIndices is a namespace-scope constant, so it outlives the upload without
  // a callback — unlike the vertices above.
  _indexBuffer->setBuffer(
      *_engine,
      IndexBuffer::BufferDescriptor(kIndices, sizeof(kIndices), nullptr));

}

/// Builds the sheets, once, the first time a scene asks for weather.
///
/// Lazily because most scenes have none, and a scene with none should not pay
/// for a material, two buffers and ten renderables it never draws.
void Renderer::buildQuad() {
  if (_quadVertices != nullptr) return;

  // Heap and freed by the callback, for the same reason the cube's vertices
  // are: Filament holds the pointer until its own thread performs the upload,
  // which is after this method has returned.
  auto *corners = new MistVertex[4];
  for (int i = 0; i < 4; i++) corners[i] = kMistCorners[i];

  _quadVertices =
      VertexBuffer::Builder()
          .vertexCount(4)
          .bufferCount(1)
          .attribute(VertexAttribute::POSITION, 0,
                     VertexBuffer::AttributeType::FLOAT3,
                     offsetof(MistVertex, position), sizeof(MistVertex))
          .attribute(VertexAttribute::UV0, 0,
                     VertexBuffer::AttributeType::FLOAT2,
                     offsetof(MistVertex, uv), sizeof(MistVertex))
          .build(*_engine);
  _quadVertices->setBufferAt(
      *_engine, 0,
      VertexBuffer::BufferDescriptor(
          corners, sizeof(MistVertex) * 4,
          [](void *buffer, size_t, void *) {
            delete[] static_cast<MistVertex *>(buffer);
          }));

  _quadIndices = IndexBuffer::Builder()
                     .indexCount(6)
                     .bufferType(IndexBuffer::IndexType::USHORT)
                     .build(*_engine);
  _quadIndices->setBuffer(
      *_engine,
      IndexBuffer::BufferDescriptor(kMistIndices, sizeof(kMistIndices),
                                    nullptr));
}

/// Builds the sheets a bank of mist is drawn with.
void Renderer::buildMist() {
  if (_mistMaterial != nullptr) return;
  buildQuad();

  _mistMaterial = materialFrom(Package::mist);

  auto &entities = utils::EntityManager::get();
  for (int sheet = 0; sheet < kMistSheets; sheet++) {
    MaterialInstance *instance = _mistMaterial->createInstance();

    // One at the middle of the bank, tailing off at the top and the bottom,
    // so a bank thins into the air rather than ending at a surface.
    const float across =
        kMistSheets == 1 ? 0.0f
                         : (float(sheet) / float(kMistSheets - 1)) * 2 - 1;
    instance->setParameter("fade", 1.0f - std::abs(across) * std::abs(across));

    utils::Entity entity = entities.create();
    RenderableManager::Builder(1)
        // Filament's Box is {centre, half-extent}; the quad spans -1..1
        // around the origin and is only two hundredths deep.
        .boundingBox({{0, 0, 0}, {1, 0.02f, 1}})
        .material(0, instance)
        .geometry(0, RenderableManager::PrimitiveType::TRIANGLES,
                  _quadVertices, _quadIndices, 0, 6)
        // Mist does not take part in shadows either way: a sheet that cast
        // one would drop a hard rectangle across the ground.
        .receiveShadows(false)
        .castShadows(false)
        .build(*_engine, entity);

    _mistInstances.push_back(instance);
    _mistEntities.push_back(entity);
  }
}

/// Puts the bank around the camera and moves its noise along.
///
/// The sheets follow whoever is looking so the weather is always around them,
/// while the noise is sampled in world space so it does not swim as they walk
/// through it — the bank moves, the clouds in it stay where they are.
void Renderer::updateMistAtTime(double time) {
  if (!_mistShowing || _mistEntities.empty()) return;

  auto &transforms = _engine->getTransformManager();
  const float3 eye = _camera->getPosition();

  for (size_t sheet = 0; sheet < _mistEntities.size(); sheet++) {
    const float across =
        _mistEntities.size() == 1
            ? 0.0f
            : (float(sheet) / float(_mistEntities.size() - 1)) * 2 - 1;

    const mat4f placement =
        mat4f::translation(float3{eye.x, _mistHeight + across * _mistThickness,
                                  eye.z}) *
        mat4f::scaling(float3{kMistReach, 1.0f, kMistReach});

    transforms.setTransform(transforms.getInstance(_mistEntities[sheet]),
                            placement);
    _mistInstances[sheet]->setParameter("time", float(time));
    _mistInstances[sheet]->setParameter("eye", eye);
  }
}

/// Builds the dome the sky's cloud is drawn on.
///
/// A hemisphere with a skirt below the horizon, so there is no seam where it
/// meets the ground, and enough rings to interpolate a direction without
/// faceting. Nothing about the cloud is in this mesh — it is somewhere to put
/// pixels and nothing else.
void Renderer::buildClouds() {
  if (_cloudMaterial != nullptr) return;


  _cloudMaterial = materialFrom(Package::sky);

  const int rings = kSkyRings;
  const int segments = kSkySegments;
  const int count = (rings + 1) * (segments + 1);

  auto *vertices = new MistVertex[count];
  for (int ring = 0; ring <= rings; ring++) {
    // Well below the horizon to straight up.
    //
    // A shallow skirt leaves a band between where the dome stops and where
    // the ground starts, and what shows through it is the flat skybox — a
    // dark ring around the whole scene. Reaching a good way down costs two
    // rings of triangles and closes it.
    const float t = float(ring) / float(rings);
    const float elevation = (-0.45f + 1.45f * t) * float(M_PI) * 0.5f;

    for (int segment = 0; segment <= segments; segment++) {
      const float azimuth =
          float(segment) / float(segments) * 2.0f * float(M_PI);
      const int index = ring * (segments + 1) + segment;

      vertices[index] = {
          float3{std::cos(elevation) * std::sin(azimuth), std::sin(elevation),
                 std::cos(elevation) * std::cos(azimuth)},
          float2{float(segment) / float(segments), t},
      };
    }
  }

  auto *indices = new uint16_t[rings * segments * 6];
  int at = 0;
  for (int ring = 0; ring < rings; ring++) {
    for (int segment = 0; segment < segments; segment++) {
      const uint16_t a = uint16_t(ring * (segments + 1) + segment);
      const uint16_t b = uint16_t(a + 1);
      const uint16_t c = uint16_t(a + segments + 1);
      const uint16_t d = uint16_t(c + 1);

      indices[at++] = a;
      indices[at++] = c;
      indices[at++] = b;
      indices[at++] = b;
      indices[at++] = c;
      indices[at++] = d;
    }
  }

  _skyVertices =
      VertexBuffer::Builder()
          .vertexCount(count)
          .bufferCount(1)
          .attribute(VertexAttribute::POSITION, 0,
                     VertexBuffer::AttributeType::FLOAT3,
                     offsetof(MistVertex, position), sizeof(MistVertex))
          .attribute(VertexAttribute::UV0, 0,
                     VertexBuffer::AttributeType::FLOAT2,
                     offsetof(MistVertex, uv), sizeof(MistVertex))
          .build(*_engine);
  _skyVertices->setBufferAt(
      *_engine, 0,
      VertexBuffer::BufferDescriptor(
          vertices, sizeof(MistVertex) * count,
          [](void *buffer, size_t, void *) {
            delete[] static_cast<MistVertex *>(buffer);
          }));

  _skyIndices = IndexBuffer::Builder()
                    .indexCount(rings * segments * 6)
                    .bufferType(IndexBuffer::IndexType::USHORT)
                    .build(*_engine);
  _skyIndices->setBuffer(
      *_engine,
      IndexBuffer::BufferDescriptor(
          indices, sizeof(uint16_t) * rings * segments * 6,
          [](void *buffer, size_t, void *) {
            delete[] static_cast<uint16_t *>(buffer);
          }));

  _cloudInstance = _cloudMaterial->createInstance();


  _cloudEntity = utils::EntityManager::get().create();
  RenderableManager::Builder(1)
      // The vertices are a unit dome and the transform supplies the radius.
      .boundingBox({{0, 0, 0}, {1, 1, 1}})
      .material(0, _cloudInstance)
      .geometry(0, RenderableManager::PrimitiveType::TRIANGLES, _skyVertices,
                _skyIndices, 0, rings * segments * 6)
      .receiveShadows(false)
      .castShadows(false)
      // Behind everything else transparent: the sky is behind the weather,
      // and cloud a hundred metres up is behind the rain in front of the lens.
      .priority(0)
      .culling(false)
      .build(*_engine, _cloudEntity);
}

/// Keeps the dome around the camera and lets the wind carry the weather.
///
/// The dome moves with the viewer and the cloud does not: what a pixel shows
/// is worked out from where its ray crosses the layer in world space, so
/// walking a kilometre walks under different cloud.
void Renderer::updateCloudsAtTime(double time) {
  if (!_cloudsShowing || !_cloudEntity) return;

  auto &transforms = _engine->getTransformManager();
  const float3 eye = _camera->getPosition();

  transforms.setTransform(
      transforms.getInstance(_cloudEntity),
      mat4f::translation(eye) * mat4f::scaling(float3{kSkyRadius}));

  _cloudInstance->setParameter("time", float(time));
  _cloudInstance->setParameter("eye", eye);
}

void Renderer::setSkyEnabled(bool enabled, const float *params) {
  // The third thing that wants to be the backdrop. An environment's cubemap
  // is behind everything; this dome is geometry in front of it, so leaving it
  // on hides a photographed sky completely — and there is nothing on screen
  // to say which of the two is winning.
  if (_showingEnvironmentSkybox) enabled = false;

  if (_disposed) return;

  const bool showing = enabled;

  if (showing) {
    buildClouds();

    // The order here is the order `OrblitSky.packed` writes them. It is one
    // array rather than a dozen arguments because the sky is one thing.
    _cloudInstance->setParameter("zenith", float3{params[0], params[1], params[2]});
    _cloudInstance->setParameter("horizon", float3{params[3], params[4], params[5]});
    _cloudInstance->setParameter("bodyDirection",
                                 float3{params[6], params[7], params[8]});
    _cloudInstance->setParameter("bodyColour",
                                 float3{params[9], params[10], params[11]});
    _cloudInstance->setParameter("bodySize", std::max(params[12], 0.001f));
    _cloudInstance->setParameter("showBody", params[13]);

    _cloudInstance->setParameter("ambient",
                                 float3{params[14], params[15], params[16]});
    _cloudInstance->setParameter("cover", params[17]);
    _cloudInstance->setParameter("altitude", std::max(params[18], 1.0f));
    _cloudInstance->setParameter("thickness", std::max(params[19], 1.0f));
    _cloudInstance->setParameter("scale", params[20]);
    _cloudInstance->setParameter("density", params[21]);
    _cloudInstance->setParameter("billow", params[22]);
    _cloudInstance->setParameter("extinction", params[23]);

    // What the sky may cost. Clamped to what the shader was built to loop to:
    // a bound past that is quietly ignored, and one of zero draws no cloud at
    // all while costing almost nothing — which reads as a fast frame rather
    // than as a fault.
    _cloudInstance->setParameter(
        "marchSteps", int32_t(std::clamp(params[24], 1.0f, 18.0f)));
    _cloudInstance->setParameter(
        "lightSteps", int32_t(std::clamp(params[25], 1.0f, 3.0f)));
    _cloudInstance->setParameter("erosion", params[26]);

    _cloudInstance->setParameter("wind", float2{params[27], params[28]});

    _skyFlash = params[29];
    _cloudInstance->setParameter("flash", params[29]);
    _cloudInstance->setParameter("flashDirection",
                                 float3{params[30], params[31], params[32]});
    _cloudInstance->setParameter("flashSeed", params[33]);

    if (!_cloudsShowing) _scene->addEntity(_cloudEntity);
  } else if (_cloudsShowing) {
    _scene->remove(_cloudEntity);
  }

  _cloudsShowing = showing;
}

/// Builds the panes a curtain of rain or snow hangs on.
void Renderer::buildRain() {
  if (_rainMaterial != nullptr) return;
  buildQuad();

  _rainMaterial = materialFrom(Package::rain);

  auto &entities = utils::EntityManager::get();
  for (int pane = 0; pane < kRainCurtains; pane++) {
    MaterialInstance *instance = _rainMaterial->createInstance();

    utils::Entity entity = entities.create();
    RenderableManager::Builder(1)
        // Filament's Box is {centre, half-extent}; the quad spans -1..1
        // around the origin and is only two hundredths deep.
        .boundingBox({{0, 0, 0}, {1, 0.02f, 1}})
        .material(0, instance)
        .geometry(0, RenderableManager::PrimitiveType::TRIANGLES,
                  _quadVertices, _quadIndices, 0, 6)
        .receiveShadows(false)
        .castShadows(false)
        .build(*_engine, entity);

    _rainInstances.push_back(instance);
    _rainEntities.push_back(entity);
  }
}

namespace {

/// The frustum's half-extents one unit in front of the camera.
///
/// Anything sizing itself to cover the view has to agree with the projection
/// about which axis the angle belongs to, or it covers the wrong one. The
/// rain panes are the reason this is written down rather than open-coded a
/// second time: they were a tangent of the vertical half-angle times the
/// aspect ratio, which is the same arithmetic setProjection does and was
/// correct only while the two agreed.
void frustumHalfExtents(float fovDegrees, float aspect, float *halfWidth,
                        float *halfHeight) {
  const float stated =
      std::tan((fovDegrees > 0 ? fovDegrees : 50.0f) * float(M_PI) / 360.0f);
  const float safe = std::max(aspect, 0.0001f);
  if (safe < 1.0f) {
    *halfWidth = stated;
    *halfHeight = stated / safe;
  } else {
    *halfHeight = stated;
    *halfWidth = stated * safe;
  }
}

} // namespace

/// Hangs the panes in front of the camera and lets the weather fall past.
///
/// Turned to face the viewer every frame, and sampled in world space, so
/// looking around moves the panes through the weather instead of taking it
/// along. Three of them at three distances, because depth is read from things
/// passing each other at different rates, and one pane passes nothing.
void Renderer::updateRainAtTime(double time) {
  if (!_rainShowing || _rainEntities.empty()) return;

  auto &transforms = _engine->getTransformManager();

  const float3 eye = _camera->getPosition();
  const float3 forward = normalize(_camera->getForwardVector());
  // Right and up from the camera's own basis rather than the world's, so a
  // pane stays square to the view when it is pitched up at the sky.
  const float3 right = -normalize(_camera->getLeftVector());
  const float3 up = normalize(_camera->getUpVector());

  const float aspect = float(_width) / float(std::max(_height, 1u));
  float halfWidth = 0.0f;
  float halfHeight = 0.0f;
  frustumHalfExtents(_fieldOfView, aspect, &halfWidth, &halfHeight);

  for (size_t pane = 0; pane < _rainEntities.size(); pane++) {
    const float distance = kRainDistances[pane];
    // A quarter over the frustum, so the edges of a pane are never on screen.
    const float height = distance * halfHeight * 1.25f;
    const float width = distance * halfWidth * 1.25f;

    const mat4f placement{
        float4{right * width, 0},
        // The quad lies flat with its face along +Y, so that axis is the one
        // pointed back at the camera.
        float4{-forward, 0},
        float4{up * height, 0},
        float4{eye + forward * distance, 1},
    };

    transforms.setTransform(transforms.getInstance(_rainEntities[pane]),
                            placement);
    _rainInstances[pane]->setParameter("time", float(time));
    _rainInstances[pane]->setParameter("eye", eye);
  }
}

void Renderer::setPrecipitationEnabled(bool enabled, const float *params) {
  if (_disposed) return;

  const bool showing = enabled && params[3] > 0;

  if (showing) {
    buildRain();

    for (size_t pane = 0; pane < _rainInstances.size(); pane++) {
      MaterialInstance *instance = _rainInstances[pane];
      instance->setParameter("colour",
                             float3{params[0], params[1], params[2]});
      // The nearer panes carry less of it. All three at full strength is
      // three times the weather anybody asked for, and the far one is what
      // gives the view its depth.
      const float share = pane == 0 ? 0.5f : (pane == 1 ? 0.75f : 1.0f);
      instance->setParameter("amount", params[3] * share);
      instance->setParameter("fall", params[4]);
      instance->setParameter("wind", float2{params[5], params[6]});
      // Drops per metre, thinned with distance so the far pane does not turn
      // into a grey wall of specks too small to resolve.
      instance->setParameter("scale",
                             params[7] / (1.0f + float(pane) * 0.8f));
      instance->setParameter("stretch", params[8]);
      instance->setParameter("threshold", params[9]);
    }

    if (!_rainShowing) {
      for (utils::Entity entity : _rainEntities) _scene->addEntity(entity);
    }
  } else if (_rainShowing) {
    for (utils::Entity entity : _rainEntities) _scene->remove(entity);
  }

  _rainShowing = showing;
}

void Renderer::setAmbientColour(float3 colour, float intensity) {
  if (_disposed) return;

  // Recorded whatever happens, so clearing an environment puts back the sky
  // the day cycle has been writing all along rather than an unlit scene.
  _ambientColour = colour;
  _ambientIntensity = intensity;

  // An environment is already lighting this. Two indirect lights is one
  // scene lit twice, and the flat one is the half that flattens it.
  if (_environmentLight != nullptr) return;

  // Replaced rather than mutated: an IndirectLight's irradiance is fixed at
  // build time.
  if (_ambient) {
    _scene->setIndirectLight(nullptr);
    _engine->destroy(_ambient);
    _ambient = nullptr;
  }

  // One band, which is a constant term — light arriving equally from every
  // direction. A real environment map would vary with direction and is what
  // this becomes once there is an asset pipeline to bake one; until then the
  // choice is between flat ambient and none, and none means every shadow and
  // every surface facing away from the sun renders pure black.
  //
  // The band-0 basis function is 1/(2*sqrt(pi)), so dividing by it makes the
  // coefficient mean the irradiance somebody actually asked for.
  constexpr float kBand0 = 0.28209479177f;  // 1 / (2 * sqrt(pi))
  const float3 sh[1] = {colour / kBand0};

  _ambient = IndirectLight::Builder()
                 .irradiance(1, sh)
                 .intensity(intensity)
                 .build(*_engine);
  _scene->setIndirectLight(_ambient);
}
}  // namespace orblit
