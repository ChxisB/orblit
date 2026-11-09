/**************************************************************************/
/*  light_storage.cpp                                                     */

#include "light_storage.h"

using namespace RendererDummy;

LightStorage *LightStorage::singleton = nullptr;

LightStorage *LightStorage::get_singleton() {
	return singleton;
}

LightStorage::LightStorage() {
	singleton = this;
}

LightStorage::~LightStorage() {
	singleton = nullptr;
}

bool LightStorage::free(RID p_rid) {
	if (owns_lightmap(p_rid)) {
		lightmap_free(p_rid);
		return true;
	} else if (owns_lightmap_instance(p_rid)) {
		lightmap_instance_free(p_rid);
		return true;
	}

	return false;
}

/* LIGHTMAP API */

RID LightStorage::lightmap_allocate() {
	return lightmap_owner.allocate_rid();
}

void LightStorage::lightmap_initialize(RID p_lightmap) {
	lightmap_owner.initialize_rid(p_lightmap, Lightmap());
}

void LightStorage::lightmap_free(RID p_rid) {
	lightmap_set_textures(p_rid, RID(), false);
	lightmap_owner.free(p_rid);
}

/* LIGHTMAP INSTANCE */

RID LightStorage::lightmap_instance_create(RID p_lightmap) {
	LightmapInstance li;
	li.lightmap = p_lightmap;
	return lightmap_instance_owner.make_rid(li);
}

void LightStorage::lightmap_instance_free(RID p_lightmap) {
	lightmap_instance_owner.free(p_lightmap);
}
