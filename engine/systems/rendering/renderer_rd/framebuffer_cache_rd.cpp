/**************************************************************************/
/*  framebuffer_cache_rd.cpp                                              */

#include "framebuffer_cache_rd.h"

#include "core/object/class_registry.h"
#include "systems/rendering/rendering_device_binds.h"

FramebufferCacheRD *FramebufferCacheRD::singleton = nullptr;

void FramebufferCacheRD::register_members() {
	ClassRegistry::bind_static_method("FramebufferCacheRD", D_METHOD("get_cache_multipass", "textures", "passes", "views"), &FramebufferCacheRD::get_cache_multipass_array);
}

void FramebufferCacheRD::_invalidate(Cache *p_cache) {
	if (p_cache->prev) {
		p_cache->prev->next = p_cache->next;
	} else {
		// At beginning of table
		uint32_t table_idx = p_cache->hash % HASH_TABLE_SIZE;
		hash_table[table_idx] = p_cache->next;
	}

	if (p_cache->next) {
		p_cache->next->prev = p_cache->prev;
	}

	cache_allocator.free(p_cache);
	cache_instances_used--;
}
void FramebufferCacheRD::_framebuffer_invalidation_callback(void *p_userdata) {
	singleton->_invalidate(reinterpret_cast<Cache *>(p_userdata));
}

RID FramebufferCacheRD::get_cache_multipass_array(const TypedArray<RID> &p_textures, const TypedArray<RDFramebufferPass> &p_passes, uint32_t p_views) {
	Vector<RID> textures;
	Vector<RD::FramebufferPass> passes;

	for (int i = 0; i < p_textures.size(); i++) {
		RID texture = p_textures[i];
		textures.push_back(texture); // store even if NULL
	}

	for (int i = 0; i < p_passes.size(); i++) {
		Ref<RDFramebufferPass> pass = p_passes[i];
		if (pass.is_valid()) {
			passes.push_back(pass->base);
		}
	}

	return FramebufferCacheRD::get_singleton()->get_cache_multipass(textures, passes, p_views);
}

FramebufferCacheRD::FramebufferCacheRD() {
	ERR_FAIL_COND(singleton != nullptr);
	singleton = this;
}

FramebufferCacheRD::~FramebufferCacheRD() {
	if (cache_instances_used > 0) {
		ERR_PRINT("At exit: " + itos(cache_instances_used) + " framebuffer cache instance(s) still in use.");
	}
}
