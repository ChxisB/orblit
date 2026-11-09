/**************************************************************************/
/*  compositor.h                                                          */

#pragma once

#include "core/io/resource.h"
#include "core/object/orblit_virtual.gen.h"
#include "systems/rendering/storage/render_data.h"

/* Compositor Effect */

class CompositorEffect : public Resource {
	ORBLIT_CLASS(CompositorEffect, Resource);

public:
	enum EffectCallbackType {
		EFFECT_CALLBACK_TYPE_PRE_OPAQUE,
		EFFECT_CALLBACK_TYPE_POST_OPAQUE,
		EFFECT_CALLBACK_TYPE_POST_SKY,
		EFFECT_CALLBACK_TYPE_PRE_TRANSPARENT,
		EFFECT_CALLBACK_TYPE_POST_TRANSPARENT,
		EFFECT_CALLBACK_TYPE_MAX
	};

private:
	RID rid;
	bool enabled = true;
	EffectCallbackType effect_callback_type = EFFECT_CALLBACK_TYPE_POST_TRANSPARENT;

	bool access_resolved_color = false;
	bool access_resolved_depth = false;
	bool needs_motion_vectors = false;
	bool needs_normal_roughness = false;
	bool needs_separate_specular = false;

protected:
	static void register_members();
	void _validate_property(PropertyInfo &p_property) const;

	void _call_render_callback(int p_effect_callback_type, const RenderData *p_render_data);

	ORBLIT_VIRTUAL2(_render_callback, int, const RenderData *)

public:
	virtual RID get_rid() const override { return rid; }

	void set_enabled(bool p_enabled);
	bool get_enabled() const;

	void set_effect_callback_type(EffectCallbackType p_callback_type);
	EffectCallbackType get_effect_callback_type() const;

	void set_access_resolved_color(bool p_enabled);
	bool get_access_resolved_color() const;

	void set_access_resolved_depth(bool p_enabled);
	bool get_access_resolved_depth() const;

	void set_needs_motion_vectors(bool p_enabled);
	bool get_needs_motion_vectors() const;

	void set_needs_normal_roughness(bool p_enabled);
	bool get_needs_normal_roughness() const;

	void set_needs_separate_specular(bool p_enabled);
	bool get_needs_separate_specular() const;

	CompositorEffect();
	~CompositorEffect();
};

VARIANT_ENUM_CAST(CompositorEffect::EffectCallbackType)

/* Compositor */

class Compositor : public Resource {
	ORBLIT_CLASS(Compositor, Resource);

private:
	RID compositor;

	// Compositor effects
	LocalVector<Ref<CompositorEffect>> effects;

protected:
	static void register_members();

public:
	virtual RID get_rid() const override { return compositor; }

	Compositor();
	~Compositor();

	// Compositor effects
	void set_compositor_effects(const TypedArray<CompositorEffect> &p_compositor_effects);
	TypedArray<CompositorEffect> get_compositor_effects() const;
};
