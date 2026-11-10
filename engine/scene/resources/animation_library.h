/**************************************************************************/
/*  animation_library.h                                                   */

#pragma once

#include "core/containers/rb_map.h"
#include "core/variant/typed_array.h"
#include "scene/resources/animation.h"

class AnimationLibrary : public Resource {
	ORBLIT_CLASS(AnimationLibrary, Resource)

	void _set_data(const Dictionary &p_data);
	Dictionary _get_data() const;

	TypedArray<StringName> _get_animation_list() const;

	void _animation_changed(const StringName &p_name);

	friend class AnimationMixer; // For faster access.
	RBMap<StringName, Ref<Animation>, StringName::AlphCompare> animations;

protected:
	static void register_members();

public:
	static bool is_valid_animation_name(const String &p_name);
	static bool is_valid_library_name(const String &p_name);
	static String validate_library_name(const String &p_name);

	Error add_animation(const StringName &p_name, const Ref<Animation> &p_animation);
	void remove_animation(const StringName &p_name);
	void rename_animation(const StringName &p_name, const StringName &p_new_name);
	bool has_animation(const StringName &p_name) const;
	Ref<Animation> get_animation(const StringName &p_name) const;
	void get_animation_list(LocalVector<StringName> *p_animations) const;
	int get_animation_list_size() const;

#ifdef TOOLS_ENABLED
	virtual void get_argument_options(const StringName &p_function, int p_idx, List<String> *r_options) const override;
#endif

	AnimationLibrary();
};
