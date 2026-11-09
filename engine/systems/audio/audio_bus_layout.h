/**************************************************************************/
/*  audio_bus_layout.h                                                    */

#pragma once

#include "core/io/resource.h"

class AudioEffect;

class AudioBusLayout : public Resource {
	ORBLIT_CLASS(AudioBusLayout, Resource);

	friend class AudioSystem;

	struct Bus {
		StringName name;
		bool solo = false;
		bool mute = false;
		bool bypass = false;

		struct Effect {
			Ref<AudioEffect> effect;
			bool enabled = false;
		};

		Vector<Effect> effects;

		float volume_db = 0.0f;
		StringName send;

		Bus() {}
	};

	Vector<Bus> buses;

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	AudioBusLayout();
};
