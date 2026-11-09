/**************************************************************************/
/*  audio_listener_planar.h                                                   */

#pragma once

#include "scene/planar/entity_planar.h"

class AudioListener2D : public Entity2D {
	ORBLIT_CLASS(AudioListener2D, Entity2D);

private:
	bool current = false;

	friend class Viewport;

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;
	void _notification(int p_what);

	static void register_members();

public:
	void make_current();
	void clear_current();
	bool is_current() const;

	AudioListener2D();
};
