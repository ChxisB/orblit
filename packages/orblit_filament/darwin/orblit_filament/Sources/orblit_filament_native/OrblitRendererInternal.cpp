#include "OrblitRendererInternal.h"

#include <filament/RenderableManager.h>

namespace orblit {

using filament::Engine;
using filament::IndexBuffer;
using filament::MaterialInstance;
using filament::RenderableManager;
using filament::VertexAttribute;
using filament::VertexBuffer;

namespace {

const float kCorners[] = {
    -1.0f, -1.0f, 0.0f, 0.0f,  //
     3.0f, -1.0f, 2.0f, 0.0f,  //
    -1.0f,  3.0f, 0.0f, 2.0f,  //
};
const uint16_t kOrder[] = {0, 1, 2};

}  // namespace

ScreenTriangle makeScreenTriangle(Engine &engine) {
  ScreenTriangle triangle;
  triangle.vertices = VertexBuffer::Builder()
                          .vertexCount(3)
                          .bufferCount(1)
                          .attribute(VertexAttribute::POSITION, 0,
                                     VertexBuffer::AttributeType::FLOAT2, 0,
                                     sizeof(float) * 4)
                          .attribute(VertexAttribute::UV0, 0,
                                     VertexBuffer::AttributeType::FLOAT2,
                                     sizeof(float) * 2, sizeof(float) * 4)
                          .build(engine);
  // Namespace-scope constants outlive the upload, so no callback is needed —
  // a stack array here would be freed before the driver read it.
  triangle.vertices->setBufferAt(
      engine, 0,
      VertexBuffer::BufferDescriptor(kCorners, sizeof(kCorners), nullptr));

  triangle.indices = IndexBuffer::Builder()
                         .indexCount(3)
                         .bufferType(IndexBuffer::IndexType::USHORT)
                         .build(engine);
  triangle.indices->setBuffer(
      engine, IndexBuffer::BufferDescriptor(kOrder, sizeof(kOrder), nullptr));
  return triangle;
}

void buildScreenRenderable(Engine &engine, utils::Entity entity,
                           MaterialInstance *material,
                           const ScreenTriangle &triangle) {
  RenderableManager::Builder(1)
      // Never culled: it is the screen, so a box that decides otherwise is a
      // box that is wrong.
      // The device-space triangle covers -1..3 in x and y, at z zero.
      .boundingBox({{1, 1, 0}, {2, 2, 1}})
      .culling(false)
      .material(0, material)
      .geometry(0, RenderableManager::PrimitiveType::TRIANGLES,
                triangle.vertices, triangle.indices, 0, 3)
      .castShadows(false)
      .receiveShadows(false)
      .build(engine, entity);
}

}  // namespace orblit
