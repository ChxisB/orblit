/**************************************************************************/
/*  editor_folding.h                                                      */

#pragma once

#include "scene/stage/entity.h"

class Animation;

class EditorFolding {
	Vector<String> _get_unfolds(const Object *p_object);
	void _set_unfolds(Object *p_object, const Vector<String> &p_unfolds);

	void _fill_folds(const Entity *p_root, const Entity *p_node, Array &p_folds, Array &resource_folds, Array &nodes_folded, HashSet<Ref<Resource>> &resources, HashSet<Ref<Animation>> &animations, Array &anim_groups_folded, HashSet<Ref<Animation>> &r_external_anims);

	void _do_object_unfolds(Object *p_object, HashSet<Ref<Resource>> &resources);
	void _do_node_unfolds(Entity *p_root, Entity *p_node, HashSet<Ref<Resource>> &resources);

	Vector<String> _get_animation_folds(const Animation *p_animation);
	void _set_animation_folds(Animation *p_animation, const Vector<String> &p_unfolds);

public:
	void save_resource_folding(const Ref<Resource> &p_resource, const String &p_path);
	void load_resource_folding(Ref<Resource> p_resource, const String &p_path);

	void save_scene_folding(const Entity *p_scene, const String &p_path);
	void load_scene_folding(Entity *p_scene, const String &p_path);

	void unfold_scene(Entity *p_scene);

	bool has_folding_data(const String &p_path);
};
