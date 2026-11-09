/**************************************************************************/
/*  audio_effect_eq.cpp                                                   */

#include "audio_effect_eq.h"

#include "core/object/class_registry.h"
#include "systems/audio/audio_system.h"

void AudioEffectEQInstance::process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) {
	int band_count = bands[0].size();
	EQ::BandProcess *proc_l = bands[0].ptrw();
	EQ::BandProcess *proc_r = bands[1].ptrw();
	float *bgain = gains.ptrw();
	for (int i = 0; i < band_count; i++) {
		bgain[i] = Math::db_to_linear(base->gain[i]);
	}

	for (int i = 0; i < p_frame_count; i++) {
		AudioFrame src = p_src_frames[i];
		AudioFrame dst = AudioFrame(0, 0);

		for (int j = 0; j < band_count; j++) {
			float l = src.left;
			float r = src.right;

			proc_l[j].process_one(l);
			proc_r[j].process_one(r);

			dst.left += l * bgain[j];
			dst.right += r * bgain[j];
		}

		p_dst_frames[i] = dst;
	}
}

Ref<AudioEffectInstance> AudioEffectEQ::instantiate() {
	Ref<AudioEffectEQInstance> ins;
	ins.instantiate();
	ins->base = Ref<AudioEffectEQ>(this);
	ins->gains.resize(eq.get_band_count());
	for (int i = 0; i < 2; i++) {
		ins->bands[i].resize(eq.get_band_count());
		for (int j = 0; j < ins->bands[i].size(); j++) {
			ins->bands[i].write[j] = eq.get_band_processor(j);
		}
	}

	return ins;
}

void AudioEffectEQ::set_band_gain_db(int p_band, float p_volume) {
	ERR_FAIL_INDEX(p_band, gain.size());
	gain.write[p_band] = p_volume;
}

float AudioEffectEQ::get_band_gain_db(int p_band) const {
	ERR_FAIL_INDEX_V(p_band, gain.size(), 0);

	return gain[p_band];
}

int AudioEffectEQ::get_band_count() const {
	return gain.size();
}

bool AudioEffectEQ::_set(const StringName &p_name, const Variant &p_value) {
	HashMap<StringName, int>::ConstIterator E = prop_band_map.find(p_name);
	if (E) {
		set_band_gain_db(E->value, p_value);
		return true;
	}

	return false;
}

bool AudioEffectEQ::_get(const StringName &p_name, Variant &r_ret) const {
	HashMap<StringName, int>::ConstIterator E = prop_band_map.find(p_name);
	if (E) {
		r_ret = get_band_gain_db(E->value);
		return true;
	}

	return false;
}

void AudioEffectEQ::_get_property_list(List<PropertyInfo> *p_list) const {
	for (int i = 0; i < band_names.size(); i++) {
		p_list->push_back(PropertyInfo(Variant::FLOAT, band_names[i], PROPERTY_HINT_RANGE, "-60,24,0.1,suffix:dB"));
	}
}

void AudioEffectEQ::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_band_gain_db", "band_idx", "volume_db"), &AudioEffectEQ::set_band_gain_db);
	ClassRegistry::bind_method(D_METHOD("get_band_gain_db", "band_idx"), &AudioEffectEQ::get_band_gain_db);
	ClassRegistry::bind_method(D_METHOD("get_band_count"), &AudioEffectEQ::get_band_count);
}

AudioEffectEQ::AudioEffectEQ(EQ::Preset p_preset) {
	eq.set_mix_rate(AudioSystem::get_singleton()->get_mix_rate());
	eq.set_preset_band_mode(p_preset);
	gain.resize(eq.get_band_count());
	for (int i = 0; i < gain.size(); i++) {
		gain.write[i] = 0.0;
		String band_name = "band_db/" + itos(eq.get_band_frequency(i)) + "_hz";
		prop_band_map[band_name] = i;
		band_names.push_back(band_name);
	}
}
