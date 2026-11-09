/**************************************************************************/
/*  compositor_storage.h                                                  */

#pragma once

#include "core/containers/rid_owner.h"
#include "systems/rendering/rendering_system_enums.h"

class RendererCompositorStorage {
private:
	static RendererCompositorStorage *singleton;
	int num_compositor_effects_with_motion_vectors = 0;

	// Compositor effect
	struct CompositorEffect {
		bool is_enabled = true;
		RSE::CompositorEffectCallbackType callback_type;
		Callable callback;

		BitField<RSE::CompositorEffectFlags> flags = {};
	};

	mutable RID_Owner<CompositorEffect, true> compositor_effects_owner;

	// Compositor
	struct Compositor {
		// Compositor effects
		Vector<RID> compositor_effects;
	};

	mutable RID_Owner<Compositor, true> compositor_owner;

public:
	static RendererCompositorStorage *get_singleton() { return singleton; }
	int get_num_compositor_effects_with_motion_vectors() const { return num_compositor_effects_with_motion_vectors; }

	RendererCompositorStorage();
	virtual ~RendererCompositorStorage();

	// Compositor effect
	RID compositor_effect_allocate();
	void compositor_effect_initialize(RID p_rid);
	void compositor_effect_free(RID p_rid);

	bool is_compositor_effect(RID p_effect) const {
		return compositor_effects_owner.owns(p_effect);
	}

	void compositor_effect_set_enabled(RID p_effect, bool p_enabled);
	bool compositor_effect_get_enabled(RID p_effect) const;

	void compositor_effect_set_callback(RID p_effect, RSE::CompositorEffectCallbackType p_callback_type, const Callable &p_callback);
	RSE::CompositorEffectCallbackType compositor_effect_get_callback_type(RID p_effect) const;
	Callable compositor_effect_get_callback(RID p_effect) const;

	void compositor_effect_set_flag(RID p_effect, RSE::CompositorEffectFlags p_flag, bool p_set);
	bool compositor_effect_get_flag(RID p_effect, RSE::CompositorEffectFlags p_flag) const;

	// Compositor
	RID compositor_allocate();
	void compositor_initialize(RID p_rid);
	void compositor_free(RID p_rid);

	bool is_compositor(RID p_compositor) const {
		return compositor_owner.owns(p_compositor);
	}

	void compositor_set_compositor_effects(RID p_compositor, const Vector<RID> &p_effects);
	Vector<RID> compositor_get_compositor_effects(RID p_compositor, RSE::CompositorEffectCallbackType p_callback_type = RSE::COMPOSITOR_EFFECT_CALLBACK_TYPE_ANY, bool p_enabled_only = true) const;
};
