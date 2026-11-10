/**************************************************************************/
/*  compositor.cpp                                                        */

#include "compositor.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "systems/rendering/rendering_system.h"

/* Compositor Effect */

void CompositorEffect::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_enabled", "enabled"), &CompositorEffect::set_enabled);
	ClassRegistry::bind_method(D_METHOD("get_enabled"), &CompositorEffect::get_enabled);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "enabled"), "set_enabled", "get_enabled");

	ClassRegistry::bind_method(D_METHOD("set_effect_callback_type", "effect_callback_type"), &CompositorEffect::set_effect_callback_type);
	ClassRegistry::bind_method(D_METHOD("get_effect_callback_type"), &CompositorEffect::get_effect_callback_type);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "effect_callback_type", PROPERTY_HINT_ENUM, "Pre Opaque,Post Opaque,Post Sky,Pre Transparent,Post Transparent"), "set_effect_callback_type", "get_effect_callback_type");

	ClassRegistry::bind_method(D_METHOD("set_access_resolved_color", "enable"), &CompositorEffect::set_access_resolved_color);
	ClassRegistry::bind_method(D_METHOD("get_access_resolved_color"), &CompositorEffect::get_access_resolved_color);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "access_resolved_color"), "set_access_resolved_color", "get_access_resolved_color");

	ClassRegistry::bind_method(D_METHOD("set_access_resolved_depth", "enable"), &CompositorEffect::set_access_resolved_depth);
	ClassRegistry::bind_method(D_METHOD("get_access_resolved_depth"), &CompositorEffect::get_access_resolved_depth);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "access_resolved_depth"), "set_access_resolved_depth", "get_access_resolved_depth");

	ClassRegistry::bind_method(D_METHOD("set_needs_motion_vectors", "enable"), &CompositorEffect::set_needs_motion_vectors);
	ClassRegistry::bind_method(D_METHOD("get_needs_motion_vectors"), &CompositorEffect::get_needs_motion_vectors);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "needs_motion_vectors"), "set_needs_motion_vectors", "get_needs_motion_vectors");

	ClassRegistry::bind_method(D_METHOD("set_needs_normal_roughness", "enable"), &CompositorEffect::set_needs_normal_roughness);
	ClassRegistry::bind_method(D_METHOD("get_needs_normal_roughness"), &CompositorEffect::get_needs_normal_roughness);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "needs_normal_roughness"), "set_needs_normal_roughness", "get_needs_normal_roughness");

	ClassRegistry::bind_method(D_METHOD("set_needs_separate_specular", "enable"), &CompositorEffect::set_needs_separate_specular);
	ClassRegistry::bind_method(D_METHOD("get_needs_separate_specular"), &CompositorEffect::get_needs_separate_specular);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "needs_separate_specular"), "set_needs_separate_specular", "get_needs_separate_specular");

	BIND_ENUM_CONSTANT(EFFECT_CALLBACK_TYPE_PRE_OPAQUE)
	BIND_ENUM_CONSTANT(EFFECT_CALLBACK_TYPE_POST_OPAQUE)
	BIND_ENUM_CONSTANT(EFFECT_CALLBACK_TYPE_POST_SKY)
	BIND_ENUM_CONSTANT(EFFECT_CALLBACK_TYPE_PRE_TRANSPARENT)
	BIND_ENUM_CONSTANT(EFFECT_CALLBACK_TYPE_POST_TRANSPARENT)
	BIND_ENUM_CONSTANT(EFFECT_CALLBACK_TYPE_MAX)

	ORBLIT_VIRTUAL_BIND(_render_callback, "effect_callback_type", "render_data");
}

void CompositorEffect::_validate_property(PropertyInfo &p_property) const {
	if (p_property.name == "access_resolved_color") {
		if (effect_callback_type == EFFECT_CALLBACK_TYPE_POST_TRANSPARENT) {
			p_property.usage = PROPERTY_USAGE_NONE;
		}
	} else if (p_property.name == "access_resolved_depth") {
		if (effect_callback_type == EFFECT_CALLBACK_TYPE_POST_TRANSPARENT) {
			p_property.usage = PROPERTY_USAGE_NONE;
		}
	} else if (p_property.name == "needs_separate_specular") {
		if (effect_callback_type != EFFECT_CALLBACK_TYPE_POST_SKY) {
			p_property.usage = PROPERTY_USAGE_NONE;
		}
	}
}

void CompositorEffect::_call_render_callback(int p_effect_callback_type, const RenderData *p_render_data) {
	ORBLIT_VIRTUAL_CALL(_render_callback, p_effect_callback_type, p_render_data);
}

void CompositorEffect::set_enabled(bool p_enabled) {
	enabled = p_enabled;
	if (rid.is_valid()) {
		RenderingSystem *rs = RenderingSystem::get_singleton();
		ERR_FAIL_NULL(rs);
		rs->compositor_effect_set_enabled(rid, enabled);
	}
}

bool CompositorEffect::get_enabled() const {
	return enabled;
}

void CompositorEffect::set_effect_callback_type(EffectCallbackType p_callback_type) {
	effect_callback_type = p_callback_type;
	notify_property_list_changed();

	if (rid.is_valid()) {
		RenderingSystem *rs = RenderingSystem::get_singleton();
		ERR_FAIL_NULL(rs);
		rs->compositor_effect_set_callback(rid, RSE::CompositorEffectCallbackType(effect_callback_type), callable_mp(this, &CompositorEffect::_call_render_callback));
	}
}

CompositorEffect::EffectCallbackType CompositorEffect::get_effect_callback_type() const {
	return effect_callback_type;
}

void CompositorEffect::set_access_resolved_color(bool p_enabled) {
	access_resolved_color = p_enabled;
	if (rid.is_valid()) {
		RenderingSystem *rs = RenderingSystem::get_singleton();
		ERR_FAIL_NULL(rs);
		rs->compositor_effect_set_flag(rid, RSE::CompositorEffectFlags::COMPOSITOR_EFFECT_FLAG_ACCESS_RESOLVED_COLOR, access_resolved_color);
	}
}

bool CompositorEffect::get_access_resolved_color() const {
	return access_resolved_color;
}

void CompositorEffect::set_access_resolved_depth(bool p_enabled) {
	access_resolved_depth = p_enabled;
	if (rid.is_valid()) {
		RenderingSystem *rs = RenderingSystem::get_singleton();
		ERR_FAIL_NULL(rs);
		rs->compositor_effect_set_flag(rid, RSE::CompositorEffectFlags::COMPOSITOR_EFFECT_FLAG_ACCESS_RESOLVED_DEPTH, access_resolved_depth);
	}
}

bool CompositorEffect::get_access_resolved_depth() const {
	return access_resolved_depth;
}

void CompositorEffect::set_needs_motion_vectors(bool p_enabled) {
	needs_motion_vectors = p_enabled;
	if (rid.is_valid()) {
		RenderingSystem *rs = RenderingSystem::get_singleton();
		ERR_FAIL_NULL(rs);
		rs->compositor_effect_set_flag(rid, RSE::CompositorEffectFlags::COMPOSITOR_EFFECT_FLAG_NEEDS_MOTION_VECTORS, needs_motion_vectors);
	}
}

bool CompositorEffect::get_needs_motion_vectors() const {
	return needs_motion_vectors;
}

void CompositorEffect::set_needs_normal_roughness(bool p_enabled) {
	needs_normal_roughness = p_enabled;
	if (rid.is_valid()) {
		RenderingSystem *rs = RenderingSystem::get_singleton();
		ERR_FAIL_NULL(rs);
		rs->compositor_effect_set_flag(rid, RSE::CompositorEffectFlags::COMPOSITOR_EFFECT_FLAG_NEEDS_ROUGHNESS, needs_normal_roughness);
	}
}

bool CompositorEffect::get_needs_normal_roughness() const {
	return needs_normal_roughness;
}

void CompositorEffect::set_needs_separate_specular(bool p_enabled) {
	needs_separate_specular = p_enabled;
	if (rid.is_valid()) {
		RenderingSystem *rs = RenderingSystem::get_singleton();
		ERR_FAIL_NULL(rs);
		rs->compositor_effect_set_flag(rid, RSE::CompositorEffectFlags::COMPOSITOR_EFFECT_FLAG_NEEDS_SEPARATE_SPECULAR, needs_separate_specular);
	}
}

bool CompositorEffect::get_needs_separate_specular() const {
	return needs_separate_specular;
}

CompositorEffect::CompositorEffect() {
	RenderingSystem *rs = RenderingSystem::get_singleton();
	if (rs != nullptr) {
		rid = rs->compositor_effect_create();
		rs->compositor_effect_set_callback(rid, RSE::CompositorEffectCallbackType(effect_callback_type), callable_mp(this, &CompositorEffect::_call_render_callback));
	}
}

CompositorEffect::~CompositorEffect() {
	RenderingSystem *rs = RenderingSystem::get_singleton();
	if (rs != nullptr && rid.is_valid()) {
		rs->free_rid(rid);
	}
}

/* Compositor */

void Compositor::register_members() {
	// compositor effects
	ClassRegistry::bind_method(D_METHOD("set_compositor_effects", "compositor_effects"), &Compositor::set_compositor_effects);
	ClassRegistry::bind_method(D_METHOD("get_compositor_effects"), &Compositor::get_compositor_effects);

	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "compositor_effects", PROPERTY_HINT_ARRAY_TYPE, MAKE_RESOURCE_TYPE_HINT("CompositorEffect")), "set_compositor_effects", "get_compositor_effects");
}

Compositor::Compositor() {
	RenderingSystem *rs = RenderingSystem::get_singleton();
	if (rs != nullptr) {
		compositor = rs->compositor_create();
	}
}

Compositor::~Compositor() {
	RenderingSystem *rs = RenderingSystem::get_singleton();
	if (rs != nullptr && compositor.is_valid()) {
		rs->free_rid(compositor);
	}
}

// Compositor effects
void Compositor::set_compositor_effects(const TypedArray<CompositorEffect> &p_compositor_effects) {
	Array effect_rids;
	effects.clear();

	for (int i = 0; i < p_compositor_effects.size(); i++) {
		// Cast to proper ref, if our object isn't a CompositorEffect resource this will be an empty Ref.
		Ref<CompositorEffect> compositor_effect = p_compositor_effects[i];

		// We add the effect even if this is an empty Ref, this allows the UI to add new entries.
		effects.push_back(compositor_effect);

		// But we only add a rid for valid Refs
		if (compositor_effect.is_valid()) {
			RID rid = compositor_effect->get_rid();
			effect_rids.push_back(rid);
		}
	}

	RenderingSystem::get_singleton()->compositor_set_compositor_effects(compositor, effect_rids);
}

TypedArray<CompositorEffect> Compositor::get_compositor_effects() const {
	TypedArray<CompositorEffect> arr;

	for (uint32_t i = 0; i < effects.size(); i++) {
		arr.push_back(effects[i]);
	}

	return arr;
}
