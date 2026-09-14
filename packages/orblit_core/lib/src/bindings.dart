// Raw FFI declarations for the core's C ABI. Nothing here interprets a result
// or owns a lifetime — see world.dart for that.

import 'dart:ffi';

const String kOrblitCoreAsset = 'package:orblit_core/orblit_core';

final class OrblitWorldStruct extends Opaque {}

final class OrblitQueryStruct extends Opaque {}

@Native<Pointer<OrblitWorldStruct> Function()>(
  symbol: 'orblit_world_create',
  assetId: kOrblitCoreAsset,
)
external Pointer<OrblitWorldStruct> worldCreate();

@Native<Void Function(Pointer<OrblitWorldStruct>)>(
  symbol: 'orblit_world_destroy',
  assetId: kOrblitCoreAsset,
)
external void worldDestroy(Pointer<OrblitWorldStruct> world);

@Native<Uint64 Function(Pointer<OrblitWorldStruct>)>(
  symbol: 'orblit_world_version',
  assetId: kOrblitCoreAsset,
)
external int worldVersion(Pointer<OrblitWorldStruct> world);

@Native<
  Uint32 Function(Pointer<OrblitWorldStruct>, Pointer<Char>, Uint32, Uint32)
>(symbol: 'orblit_component_register', assetId: kOrblitCoreAsset)
external int componentRegister(
  Pointer<OrblitWorldStruct> world,
  Pointer<Char> name,
  int size,
  int alignment,
);

@Native<Uint32 Function(Pointer<OrblitWorldStruct>, Pointer<Char>)>(
  symbol: 'orblit_component_lookup',
  assetId: kOrblitCoreAsset,
)
external int componentLookup(
  Pointer<OrblitWorldStruct> world,
  Pointer<Char> name,
);

@Native<Uint32 Function(Pointer<OrblitWorldStruct>, Uint32)>(
  symbol: 'orblit_component_size',
  assetId: kOrblitCoreAsset,
)
external int componentSize(Pointer<OrblitWorldStruct> world, int component);

@Native<Uint32 Function(Pointer<OrblitWorldStruct>)>(
  symbol: 'orblit_component_count',
  assetId: kOrblitCoreAsset,
)
external int componentCount(Pointer<OrblitWorldStruct> world);

@Native<Uint64 Function(Pointer<OrblitWorldStruct>)>(
  symbol: 'orblit_entity_create',
  assetId: kOrblitCoreAsset,
)
external int entityCreate(Pointer<OrblitWorldStruct> world);

@Native<Void Function(Pointer<OrblitWorldStruct>, Uint64)>(
  symbol: 'orblit_entity_destroy',
  assetId: kOrblitCoreAsset,
)
external void entityDestroy(Pointer<OrblitWorldStruct> world, int entity);

@Native<Bool Function(Pointer<OrblitWorldStruct>, Uint64)>(
  symbol: 'orblit_entity_alive',
  assetId: kOrblitCoreAsset,
)
external bool entityAlive(Pointer<OrblitWorldStruct> world, int entity);

@Native<Uint32 Function(Pointer<OrblitWorldStruct>)>(
  symbol: 'orblit_entity_count',
  assetId: kOrblitCoreAsset,
)
external int entityCount(Pointer<OrblitWorldStruct> world);

@Native<
  Bool Function(Pointer<OrblitWorldStruct>, Uint64, Uint32, Pointer<Void>)
>(symbol: 'orblit_entity_add', assetId: kOrblitCoreAsset)
external bool entityAdd(
  Pointer<OrblitWorldStruct> world,
  int entity,
  int component,
  Pointer<Void> value,
);

@Native<Bool Function(Pointer<OrblitWorldStruct>, Uint64, Uint32)>(
  symbol: 'orblit_entity_remove',
  assetId: kOrblitCoreAsset,
)
external bool entityRemove(
  Pointer<OrblitWorldStruct> world,
  int entity,
  int component,
);

@Native<Bool Function(Pointer<OrblitWorldStruct>, Uint64, Uint32)>(
  symbol: 'orblit_entity_has',
  assetId: kOrblitCoreAsset,
)
external bool entityHas(
  Pointer<OrblitWorldStruct> world,
  int entity,
  int component,
);

@Native<Pointer<Void> Function(Pointer<OrblitWorldStruct>, Uint64, Uint32)>(
  symbol: 'orblit_entity_get',
  assetId: kOrblitCoreAsset,
)
external Pointer<Void> entityGet(
  Pointer<OrblitWorldStruct> world,
  int entity,
  int component,
);

@Native<
  Pointer<OrblitQueryStruct> Function(
    Pointer<OrblitWorldStruct>,
    Pointer<Uint32>,
    Uint32,
  )
>(symbol: 'orblit_query_create', assetId: kOrblitCoreAsset)
external Pointer<OrblitQueryStruct> queryCreate(
  Pointer<OrblitWorldStruct> world,
  Pointer<Uint32> components,
  int count,
);

@Native<Void Function(Pointer<OrblitQueryStruct>)>(
  symbol: 'orblit_query_destroy',
  assetId: kOrblitCoreAsset,
)
external void queryDestroy(Pointer<OrblitQueryStruct> query);

@Native<Uint32 Function(Pointer<OrblitQueryStruct>)>(
  symbol: 'orblit_query_chunk_count',
  assetId: kOrblitCoreAsset,
)
external int queryChunkCount(Pointer<OrblitQueryStruct> query);

@Native<Uint32 Function(Pointer<OrblitQueryStruct>, Uint32)>(
  symbol: 'orblit_query_chunk_length',
  assetId: kOrblitCoreAsset,
)
external int queryChunkLength(Pointer<OrblitQueryStruct> query, int chunk);

@Native<Pointer<Void> Function(Pointer<OrblitQueryStruct>, Uint32, Uint32)>(
  symbol: 'orblit_query_chunk_column',
  assetId: kOrblitCoreAsset,
)
external Pointer<Void> queryChunkColumn(
  Pointer<OrblitQueryStruct> query,
  int chunk,
  int slot,
);

@Native<Pointer<Uint64> Function(Pointer<OrblitQueryStruct>, Uint32)>(
  symbol: 'orblit_query_chunk_entities',
  assetId: kOrblitCoreAsset,
)
external Pointer<Uint64> queryChunkEntities(
  Pointer<OrblitQueryStruct> query,
  int chunk,
);

@Native<
  Uint32 Function(Pointer<OrblitQueryStruct>, Uint32, Pointer<Uint32>, Uint32)
>(symbol: 'orblit_query_chunk_components', assetId: kOrblitCoreAsset)
external int queryChunkComponents(
  Pointer<OrblitQueryStruct> query,
  int chunk,
  Pointer<Uint32> out,
  int capacity,
);

@Native<Pointer<Void> Function(Pointer<OrblitQueryStruct>, Uint32, Uint32)>(
  symbol: 'orblit_query_chunk_component_column',
  assetId: kOrblitCoreAsset,
)
external Pointer<Void> queryChunkComponentColumn(
  Pointer<OrblitQueryStruct> query,
  int chunk,
  int component,
);

/// The three component ids the core's transform support registers.
final class OrblitTransformsStruct extends Struct {
  @Uint32()
  external int local;

  @Uint32()
  external int world;

  @Uint32()
  external int parent;
}

@Native<OrblitTransformsStruct Function(Pointer<OrblitWorldStruct>)>(
  symbol: 'orblit_transform_register',
  assetId: kOrblitCoreAsset,
)
external OrblitTransformsStruct transformRegister(
  Pointer<OrblitWorldStruct> world,
);

@Native<Uint32 Function(Pointer<OrblitWorldStruct>)>(
  symbol: 'orblit_transform_propagate',
  assetId: kOrblitCoreAsset,
)
external int transformPropagate(Pointer<OrblitWorldStruct> world);

@Native<Void Function(Pointer<Float>, Pointer<Float>)>(
  symbol: 'orblit_transform_compose',
  assetId: kOrblitCoreAsset,
)
external void transformCompose(Pointer<Float> trs, Pointer<Float> out);

@Native<Void Function(Pointer<OrblitWorldStruct>, Double)>(
  symbol: 'orblit_world_tick',
  assetId: kOrblitCoreAsset,
)
external void worldTick(Pointer<OrblitWorldStruct> world, double delta);

@Native<Double Function(Pointer<OrblitWorldStruct>)>(
  symbol: 'orblit_world_elapsed',
  assetId: kOrblitCoreAsset,
)
external double worldElapsed(Pointer<OrblitWorldStruct> world);
