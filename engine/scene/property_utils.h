/**************************************************************************/
/*  property_utils.h                                                      */

#pragma once

#include "scene/resources/prefab.h"
#include "scene/stage/entity.h"

class PropertyUtils {
public:
	static bool is_property_value_different(const Object *p_object, const Variant &p_a, const Variant &p_b);
	// Gets the most pure default value, the one that would be set when the node has just been instantiated
	static Variant get_property_default_value(const Object *p_object, const StringName &p_property, bool *r_is_valid = nullptr, const Vector<SceneState::PackState> *p_states_stack_cache = nullptr, bool p_update_exports = false, const Entity *p_owner = nullptr, bool *r_is_class_default = nullptr);

	// Gets the instance/inheritance states of this node, in order of precedence,
	// that is, from the topmost (the most able to override values) to the lowermost
	// (Note that in nested instantiation, the one with the greatest precedence is the furthest
	// in the tree, since every owner found while traversing towards the root gets a chance
	// to override property values.)
	static Vector<SceneState::PackState> get_node_states_stack(const Entity *p_node, const Entity *p_owner = nullptr, bool *r_instantiated_by_owner = nullptr);

	static void assign_custom_type_script(Object *p_object, const Ref<Script> &p_script);
	static Ref<Script> get_custom_type_script(const Object *p_object);
};
