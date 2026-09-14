// The C ABI, over the archetype store in world.h.
//
// Thin on purpose: the interesting decisions are in the World, and this file's
// job is to keep C++ types from leaking across a boundary that Dart, QuickJS
// and a native console shell all have to speak.

#include "orblit_core.h"

#include <string>
#include <vector>

#include "world.h"

using orblit::Archetype;
using orblit::World;

namespace {

World *world_of(OrblitWorld *handle) {
  return reinterpret_cast<World *>(handle);
}

const World *world_of(const OrblitWorld *handle) {
  return reinterpret_cast<const World *>(handle);
}

OrblitWorld *handle_of(World *world) {
  return reinterpret_cast<OrblitWorld *>(world);
}

}  // namespace

/// A cached view of which archetypes match, refreshed when the world moves on.
///
/// Holding the resolved column index per slot means iterating costs a lookup
/// per archetype per tick rather than per entity.
struct OrblitQuery {
  World *world = nullptr;
  std::vector<OrblitComponent> components;
  uint64_t version = 0;
  std::vector<int> archetypes;
  std::vector<std::vector<int>> columns;

  void refresh() {
    if (version == world->version() && version != 0) return;
    version = world->version();
    archetypes = world->matchingArchetypes(components);
    columns.clear();
    columns.reserve(archetypes.size());
    for (int index : archetypes) {
      std::vector<int> slots;
      slots.reserve(components.size());
      for (OrblitComponent component : components) {
        slots.push_back(world->archetype(index).columnOf(component));
      }
      columns.push_back(std::move(slots));
    }
  }
};

// ---------------------------------------------------------------- world ----

OrblitWorld *orblit_world_create(void) { return handle_of(new World()); }

void orblit_world_destroy(OrblitWorld *world) { delete world_of(world); }

uint64_t orblit_world_version(const OrblitWorld *world) {
  return world_of(world)->version();
}

// ------------------------------------------------------------ components ----

OrblitComponent orblit_component_register(OrblitWorld *world, const char *name,
                                        uint32_t size, uint32_t alignment) {
  if (!name) return 0;
  return world_of(world)->registerComponent(name, size, alignment);
}

OrblitComponent orblit_component_lookup(const OrblitWorld *world,
                                      const char *name) {
  if (!name) return 0;
  return world_of(world)->lookupComponent(name);
}

uint32_t orblit_component_size(const OrblitWorld *world,
                              OrblitComponent component) {
  return world_of(world)->componentSize(component);
}

uint32_t orblit_component_count(const OrblitWorld *world) {
  return world_of(world)->componentCount();
}

// -------------------------------------------------------------- entities ----

OrblitEntity orblit_entity_create(OrblitWorld *world) {
  return world_of(world)->createEntity();
}

void orblit_entity_destroy(OrblitWorld *world, OrblitEntity entity) {
  world_of(world)->destroyEntity(entity);
}

bool orblit_entity_alive(const OrblitWorld *world, OrblitEntity entity) {
  return world_of(world)->alive(entity);
}

uint32_t orblit_entity_count(const OrblitWorld *world) {
  return world_of(world)->entityCount();
}

bool orblit_entity_add(OrblitWorld *world, OrblitEntity entity,
                      OrblitComponent component, const void *value) {
  return world_of(world)->addComponent(entity, component, value);
}

bool orblit_entity_remove(OrblitWorld *world, OrblitEntity entity,
                         OrblitComponent component) {
  return world_of(world)->removeComponent(entity, component);
}

bool orblit_entity_has(const OrblitWorld *world, OrblitEntity entity,
                      OrblitComponent component) {
  return world_of(world)->hasComponent(entity, component);
}

void *orblit_entity_get(OrblitWorld *world, OrblitEntity entity,
                       OrblitComponent component) {
  return world_of(world)->getComponent(entity, component);
}

// --------------------------------------------------------------- queries ----

OrblitQuery *orblit_query_create(OrblitWorld *world,
                               const OrblitComponent *components,
                               uint32_t count) {
  auto *query = new OrblitQuery();
  query->world = world_of(world);
  query->components.assign(components, components + count);
  return query;
}

void orblit_query_destroy(OrblitQuery *query) { delete query; }

uint32_t orblit_query_chunk_count(OrblitQuery *query) {
  query->refresh();
  return static_cast<uint32_t>(query->archetypes.size());
}

uint32_t orblit_query_chunk_length(OrblitQuery *query, uint32_t chunk) {
  if (chunk >= query->archetypes.size()) return 0;
  return query->world->archetype(query->archetypes[chunk]).length();
}

void *orblit_query_chunk_column(OrblitQuery *query, uint32_t chunk,
                               uint32_t slot) {
  if (chunk >= query->archetypes.size()) return nullptr;
  if (slot >= query->components.size()) return nullptr;
  const int column = query->columns[chunk][slot];
  if (column < 0) return nullptr;
  return query->world->archetype(query->archetypes[chunk]).columnData(column);
}

const OrblitEntity *orblit_query_chunk_entities(OrblitQuery *query,
                                              uint32_t chunk) {
  if (chunk >= query->archetypes.size()) return nullptr;
  return query->world->archetype(query->archetypes[chunk]).entities().data();
}

uint32_t orblit_query_chunk_components(OrblitQuery *query, uint32_t chunk,
                                      OrblitComponent *out, uint32_t capacity) {
  if (chunk >= query->archetypes.size()) return 0;
  const auto &components =
      query->world->archetype(query->archetypes[chunk]).components();
  if (out != nullptr) {
    const uint32_t writable =
        capacity < components.size() ? capacity
                                     : static_cast<uint32_t>(components.size());
    for (uint32_t i = 0; i < writable; i++) out[i] = components[i];
  }
  return static_cast<uint32_t>(components.size());
}

void *orblit_query_chunk_component_column(OrblitQuery *query, uint32_t chunk,
                                         OrblitComponent component) {
  if (chunk >= query->archetypes.size()) return nullptr;
  Archetype &archetype = query->world->archetype(query->archetypes[chunk]);
  const int column = archetype.columnOf(component);
  if (column < 0) return nullptr;
  return archetype.columnData(column);
}

// --------------------------------------------------------------- systems ----

uint32_t orblit_system_register(OrblitWorld *world, const char *name,
                               OrblitSystemFn function, void *user) {
  return world_of(world)->registerSystem(name ? name : "", function, user);
}

void orblit_world_tick(OrblitWorld *world, double delta) {
  world_of(world)->tick(delta);
}

double orblit_world_elapsed(const OrblitWorld *world) {
  return world_of(world)->elapsed();
}
