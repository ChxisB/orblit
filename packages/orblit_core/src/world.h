// The archetype store behind the C ABI.
//
// Entities carrying the same set of components live together in one archetype,
// and within an archetype each component type occupies one contiguous column.
// Adding or removing a component therefore moves an entity between archetypes
// rather than leaving holes, which is what keeps a query's runs dense and makes
// a whole system's work one pointer and a length.

#ifndef ORBLIT_WORLD_H
#define ORBLIT_WORLD_H

#include <cstdint>
#include <map>
#include <string>
#include <unordered_map>
#include <vector>

#include "orblit_core.h"

namespace orblit {

struct ComponentType {
  std::string name;
  uint32_t size = 0;
  uint32_t alignment = 0;
};

class World;

/// One set of component types, and the entities that carry exactly that set.
class Archetype {
 public:
  explicit Archetype(std::vector<OrblitComponent> components,
                     const std::vector<ComponentType> &types);

  /// Sorted, so a component set has one canonical spelling and archetypes can
  /// be looked up by it.
  const std::vector<OrblitComponent> &components() const { return components_; }

  bool has(OrblitComponent component) const;

  /// Position of `component` among this archetype's columns, or -1.
  int columnOf(OrblitComponent component) const;

  uint32_t length() const { return static_cast<uint32_t>(entities_.size()); }
  const std::vector<OrblitEntity> &entities() const { return entities_; }

  /// The start of a column's storage. Moves when the archetype grows.
  void *columnData(int column) { return columns_[column].data(); }

  /// Appends a row for `entity` with every component zeroed, returning its
  /// index.
  uint32_t appendRow(OrblitEntity entity);

  /// Removes `row` by moving the last row into its place, so rows stay dense.
  /// The entity that moved is returned so the caller can fix its record; zero
  /// when the removed row was already last.
  OrblitEntity removeRow(uint32_t row);

  void *cell(uint32_t row, int column);

 private:
  std::vector<OrblitComponent> components_;
  std::vector<uint32_t> sizes_;
  std::vector<std::vector<uint8_t>> columns_;
  std::vector<OrblitEntity> entities_;
};

/// Where one entity's data currently lives.
struct EntityRecord {
  uint32_t generation = 0;
  bool alive = false;
  int archetype = -1;
  uint32_t row = 0;
};

struct System {
  std::string name;
  OrblitSystemFn function = nullptr;
  void *user = nullptr;
};

class World {
 public:
  World();

  uint64_t version() const { return version_; }
  double elapsed() const { return elapsed_; }

  // components
  OrblitComponent registerComponent(const std::string &name, uint32_t size,
                                   uint32_t alignment);
  OrblitComponent lookupComponent(const std::string &name) const;
  uint32_t componentSize(OrblitComponent component) const;
  uint32_t componentCount() const {
    return static_cast<uint32_t>(types_.size());
  }
  const std::vector<ComponentType> &types() const { return types_; }

  // entities
  OrblitEntity createEntity();
  void destroyEntity(OrblitEntity entity);
  bool alive(OrblitEntity entity) const;
  uint32_t entityCount() const { return liveCount_; }

  bool addComponent(OrblitEntity entity, OrblitComponent component,
                    const void *value);
  bool removeComponent(OrblitEntity entity, OrblitComponent component);
  bool hasComponent(OrblitEntity entity, OrblitComponent component) const;
  void *getComponent(OrblitEntity entity, OrblitComponent component);

  // queries
  std::vector<int> matchingArchetypes(
      const std::vector<OrblitComponent> &components) const;
  Archetype &archetype(int index) { return archetypes_[index]; }

  // systems
  uint32_t registerSystem(const std::string &name, OrblitSystemFn function,
                          void *user);
  void tick(double delta);

  static uint32_t indexOf(OrblitEntity entity) {
    return static_cast<uint32_t>(entity & 0xFFFFFFFFu);
  }
  static uint32_t generationOf(OrblitEntity entity) {
    return static_cast<uint32_t>(entity >> 32);
  }
  static OrblitEntity handle(uint32_t index, uint32_t generation) {
    return (static_cast<OrblitEntity>(generation) << 32) | index;
  }

 private:
  /// The archetype for a component set, created if it does not exist.
  int archetypeFor(const std::vector<OrblitComponent> &components);

  /// Moves an entity to the archetype for `components`, carrying across every
  /// component the two sets share.
  void moveEntity(uint32_t index, const std::vector<OrblitComponent> &components);

  const EntityRecord *recordFor(OrblitEntity entity) const;
  EntityRecord *recordFor(OrblitEntity entity);

  std::vector<ComponentType> types_;
  std::unordered_map<std::string, OrblitComponent> typesByName_;

  std::vector<Archetype> archetypes_;
  std::map<std::vector<OrblitComponent>, int> archetypesByComponents_;

  std::vector<EntityRecord> records_;
  std::vector<uint32_t> freeSlots_;
  uint32_t liveCount_ = 0;

  std::vector<System> systems_;
  uint64_t version_ = 1;
  double elapsed_ = 0.0;
};

}  // namespace orblit

#endif  // ORBLIT_WORLD_H
