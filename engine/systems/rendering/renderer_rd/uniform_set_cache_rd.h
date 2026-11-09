/**************************************************************************/
/*  uniform_set_cache_rd.h                                                */

#pragma once

#include "core/containers/local_vector.h"
#include "core/containers/paged_allocator.h"
#include "systems/rendering/rendering_device.h"

class UniformSetCacheRD : public Object {
	ORBLIT_CLASS(UniformSetCacheRD, Object)

	struct Cache {
		Cache *prev = nullptr;
		Cache *next = nullptr;
		uint32_t hash = 0;
		RID shader;
		uint32_t set = 0;
		RID cache;
		LocalVector<RD::Uniform> uniforms;
	};

	PagedAllocator<Cache> cache_allocator;

	enum {
		HASH_TABLE_SIZE = 16381 // Prime
	};

	Cache *hash_table[HASH_TABLE_SIZE] = {};

	static _FORCE_INLINE_ uint32_t _hash_uniform(const RD::Uniform &u, uint32_t h) {
		h = hash_murmur3_one_32(u.uniform_type, h);
		h = hash_murmur3_one_32(u.binding, h);
		uint32_t rsize = u.get_id_count();
		for (uint32_t j = 0; j < rsize; j++) {
			h = hash_murmur3_one_64(u.get_id(j).get_id(), h);
		}
		return hash_fmix32(h);
	}

	static _FORCE_INLINE_ bool _compare_uniform(const RD::Uniform &a, const RD::Uniform &b) {
		if (a.binding != b.binding) {
			return false;
		}
		if (a.uniform_type != b.uniform_type) {
			return false;
		}
		uint32_t rsize = a.get_id_count();
		if (rsize != b.get_id_count()) {
			return false;
		}
		for (uint32_t j = 0; j < rsize; j++) {
			if (a.get_id(j) != b.get_id(j)) {
				return false;
			}
		}
		return true;
	}

	_FORCE_INLINE_ uint32_t _hash_args(uint32_t h, const RD::Uniform &arg) {
		return _hash_uniform(arg, h);
	}

	template <typename... Args>
	uint32_t _hash_args(uint32_t h, const RD::Uniform &arg, Args... args) {
		h = _hash_uniform(arg, h);
		return _hash_args(h, args...);
	}

	_FORCE_INLINE_ bool _compare_args(uint32_t idx, const LocalVector<RD::Uniform> &uniforms, const RD::Uniform &arg) {
		return _compare_uniform(uniforms[idx], arg);
	}

	template <typename... Args>
	_FORCE_INLINE_ bool _compare_args(uint32_t idx, const LocalVector<RD::Uniform> &uniforms, const RD::Uniform &arg, Args... args) {
		if (!_compare_uniform(uniforms[idx], arg)) {
			return false;
		}
		return _compare_args(idx + 1, uniforms, args...);
	}

	static UniformSetCacheRD *singleton;

	uint32_t cache_instances_used = 0;

	void _invalidate(Cache *p_cache);
	static void _uniform_set_invalidation_callback(void *p_userdata);

	template <typename Collection>
	RID _allocate_from_uniforms(RID p_shader, uint32_t p_set, uint32_t p_hash, uint32_t p_table_idx, const Collection &p_uniforms) {
		RID rid = RD::get_singleton()->uniform_set_create(p_uniforms, p_shader, p_set);
		ERR_FAIL_COND_V(rid.is_null(), rid);

		Cache *c = cache_allocator.alloc();
		c->hash = p_hash;
		c->set = p_set;
		c->shader = p_shader;
		c->cache = rid;
		c->uniforms.resize(p_uniforms.size());
		for (uint32_t i = 0; i < c->uniforms.size(); i++) {
			c->uniforms[i] = p_uniforms[i];
		}
		c->prev = nullptr;
		c->next = hash_table[p_table_idx];
		if (hash_table[p_table_idx]) {
			hash_table[p_table_idx]->prev = c;
		}
		hash_table[p_table_idx] = c;

		RD::get_singleton()->uniform_set_set_invalidation_callback(rid, _uniform_set_invalidation_callback, c);

		cache_instances_used++;

		return rid;
	}

private:
	static void register_members();

public:
	template <typename... Args>
	RID get_cache(RID p_shader, uint32_t p_set, Args... args) {
		uint32_t h = hash_murmur3_one_64(p_shader.get_id());
		h = hash_murmur3_one_32(p_set, h);
		h = _hash_args(h, args...);

		uint32_t table_idx = h % HASH_TABLE_SIZE;
		{
			const Cache *c = hash_table[table_idx];

			while (c) {
				if (c->hash == h && c->set == p_set && c->shader == p_shader && sizeof...(Args) == c->uniforms.size() && _compare_args(0, c->uniforms, args...)) {
					return c->cache;
				}
				c = c->next;
			}
		}

		// Not in cache, create:

		return _allocate_from_uniforms(p_shader, p_set, h, table_idx, Vector<RD::Uniform>{ args... });
	}

	template <typename... Args>
	RID get_cache_vec(RID p_shader, uint32_t p_set, const LocalVector<RD::Uniform> &p_uniforms) {
		uint32_t h = hash_murmur3_one_64(p_shader.get_id());
		h = hash_murmur3_one_32(p_set, h);
		for (uint32_t i = 0; i < p_uniforms.size(); i++) {
			h = _hash_uniform(p_uniforms[i], h);
		}

		h = hash_fmix32(h);

		uint32_t table_idx = h % HASH_TABLE_SIZE;
		{
			const Cache *c = hash_table[table_idx];

			while (c) {
				if (c->hash == h && c->set == p_set && c->shader == p_shader && (uint32_t)p_uniforms.size() == c->uniforms.size()) {
					bool all_ok = true;
					for (uint32_t i = 0; i < p_uniforms.size(); i++) {
						if (!_compare_uniform(p_uniforms[i], c->uniforms[i])) {
							all_ok = false;
							break;
						}
					}

					if (all_ok) {
						return c->cache;
					}
				}
				c = c->next;
			}
		}

		// Not in cache, create:
		return _allocate_from_uniforms(p_shader, p_set, h, table_idx, p_uniforms);
	}

	static RID get_cache_array(RID p_shader, uint32_t p_set, const TypedArray<RDUniform> &p_uniforms);

	// Re-keys a cache entry after a texture RID was replaced in its uniforms.
	// Called by RenderingDevice::texture_replace_rid() to keep the cache consistent.
	// p_cache_userdata is the invalidated_callback_userdata from the UniformSet (the Cache*).
	void texture_replaced_in_uniform_set(void *p_cache_userdata, RID p_old_texture, RID p_new_texture);

	// Returns true if the given callback is the one used by this cache.
	bool is_cache_invalidation_callback(void (*p_callback)(void *)) const {
		return p_callback == _uniform_set_invalidation_callback;
	}

	static UniformSetCacheRD *get_singleton() { return singleton; }

	UniformSetCacheRD();
	~UniformSetCacheRD();
};
