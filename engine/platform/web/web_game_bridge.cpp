/**************************************************************************/
/*  web_game_bridge.cpp                                                   */

#include "web_game_bridge.h"

#include "js.h"
#include "web_script_proxy.h"

#include "core/config/engine.h"
#include "core/containers/local_vector.h"
#include "core/object/callable_mp.h"
#include "core/object/object.h"
#include "core/string/entity_path.h"
#include "scene/spatial/entity_spatial.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h" // Stage only forward declares it.

#include <emscripten/emscripten.h>

// A C# game runs in the page as a second wasm module, so it cannot hold an
// engine pointer. Entities cross as small integer handles. Calls carry many
// entities at once, because a crossing costs more than the work it carries.

// Layout shared with EntityPosition in Orblit.Web.
struct EntityPosition {
	int32_t handle;
	float x;
	float y;
	float z;
};

// A handle packs a slot index plus one in the low 20 bits and the slot's
// generation in the next 11. Zero means no entity. A released slot gains a
// generation, so a stale handle misses the entity that took its place. Both
// fields fit a positive int32, which is what the page passes around.
static constexpr int32_t SLOT_BITS = 20;
static constexpr int32_t SLOT_MASK = (1 << SLOT_BITS) - 1;
static constexpr uint32_t GENERATION_MASK = 0x7FF;

struct HandleSlot {
	ObjectID object_id;
	uint32_t generation = 0;
};

static LocalVector<HandleSlot> handle_slots;
static LocalVector<uint32_t> free_slots;

// Returns the slot a live handle names, or null for zero, a released handle
// and a handle from an older generation.
static HandleSlot *_find_slot(int32_t p_handle) {
	const uint32_t index = (uint32_t)(p_handle & SLOT_MASK);
	if (index == 0 || index > handle_slots.size()) {
		return nullptr;
	}
	HandleSlot &slot = handle_slots[index - 1];
	if (slot.object_id.is_null() || slot.generation != ((uint32_t)p_handle >> SLOT_BITS)) {
		return nullptr;
	}
	return &slot;
}

int32_t web_game_acquire_handle(const Object *p_object) {
	uint32_t index;
	if (!free_slots.is_empty()) {
		index = free_slots[free_slots.size() - 1];
		free_slots.resize(free_slots.size() - 1);
	} else if (handle_slots.size() < (uint32_t)SLOT_MASK) {
		index = handle_slots.size();
		handle_slots.push_back(HandleSlot());
	} else {
		return 0;
	}
	HandleSlot &slot = handle_slots[index];
	slot.object_id = p_object->get_instance_id();
	return (int32_t)((slot.generation << SLOT_BITS) | (index + 1));
}

void web_game_release_handle(int32_t p_handle) {
	HandleSlot *slot = _find_slot(p_handle);
	if (slot == nullptr) {
		return;
	}
	slot->object_id = ObjectID();
	slot->generation = (slot->generation + 1) & GENERATION_MASK;
	free_slots.push_back((uint32_t)(p_handle & SLOT_MASK) - 1);
}

static Entity3D *_get_entity_3d(int32_t p_handle) {
	const HandleSlot *slot = _find_slot(p_handle);
	return slot == nullptr ? nullptr : ObjectRegistry::get_instance<Entity3D>(slot->object_id);
}

static void _on_process_frame() {
	Engine *engine = Engine::get_singleton();
	orblit_js_game_frame(engine->get_process_step(), (int)engine->get_process_frames());
}

// Calls the page's frame hook once per process frame, inside the engine tick,
// so entity changes from the hook draw in the same frame.
extern "C" EMSCRIPTEN_KEEPALIVE void orblit_game_start() {
	Stage::get_singleton()->connect(SNAME("process_frame"), callable_mp_static(&_on_process_frame));
	web_script_proxy_start();
}

// The path starts below the stage root, such as "Main/Cube". Returns a handle
// for an Entity3D, or 0 when the path names no Entity3D.
extern "C" EMSCRIPTEN_KEEPALIVE int32_t orblit_game_bind_entity(const char *p_path) {
	Entity *root = Stage::get_singleton()->get_root();
	Entity3D *entity = Object::cast_to<Entity3D>(root->get_entity_or_null(EntityPath(String::utf8(p_path))));
	if (entity == nullptr) {
		return 0;
	}
	return web_game_acquire_handle(entity);
}

// Frees the handle. The entity stays in the stage.
extern "C" EMSCRIPTEN_KEEPALIVE void orblit_game_release_entity(int32_t p_handle) {
	web_game_release_handle(p_handle);
}

// Moves each entity in the records. Returns how many handles were still valid.
extern "C" EMSCRIPTEN_KEEPALIVE int32_t orblit_game_set_positions(const EntityPosition *p_records, int32_t p_count) {
	int32_t applied = 0;
	for (int32_t i = 0; i < p_count; i++) {
		Entity3D *entity = _get_entity_3d(p_records[i].handle);
		if (entity != nullptr) {
			entity->set_position(Vector3(p_records[i].x, p_records[i].y, p_records[i].z));
			applied++;
		}
	}
	return applied;
}

// Fills x, y and z for each handle in the records. An invalid handle keeps
// the values it came with.
extern "C" EMSCRIPTEN_KEEPALIVE void orblit_game_get_positions(EntityPosition *r_records, int32_t p_count) {
	for (int32_t i = 0; i < p_count; i++) {
		const Entity3D *entity = _get_entity_3d(r_records[i].handle);
		if (entity != nullptr) {
			const Vector3 position = entity->get_position();
			r_records[i].x = position.x;
			r_records[i].y = position.y;
			r_records[i].z = position.z;
		}
	}
}
