/**************************************************************************/
/*  audio_stream_player_planar.cpp                                            */

#include "audio_stream_player_planar.h"

#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/audio/audio_stream_player_internal.h"
#include "scene/planar/audio_listener_planar.h"
#include "scene/resources/audio/audio_stream.h"
#include "scene/resources/world_planar.h"
#include "scene/stage/viewport.h"
#include "systems/audio/audio_system.h"

#ifndef PHYSICS_PLANAR_DISABLED
#include "scene/planar/physics/area_planar.h"
#include "systems/physics_planar/direct_states/physics_direct_space_state_planar.h"
#include "systems/physics_planar/physics_system_planar.h"
#endif // PHYSICS_PLANAR_DISABLED

void AudioStreamPlayer2D::_notification(int p_what) {
	internal->notification(p_what);

	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			AudioSystem::get_singleton()->add_listener_changed_callback(_listener_changed_cb, this);
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			AudioSystem::get_singleton()->remove_listener_changed_callback(_listener_changed_cb, this);
		} break;

		case NOTIFICATION_INTERNAL_STEP: {
			// Update anything related to position first, if possible of course.
			if (setplay.get() > 0 || (internal->active.is_set() && last_mix_count != AudioSystem::get_singleton()->get_mix_count()) || force_update_panning) {
				force_update_panning = false;
				_update_panning();
			}

			if (setplayback.is_valid() && setplay.get() >= 0) {
				internal->active.set();
				AudioSystem::get_singleton()->start_playback_stream(setplayback, _get_actual_bus(), volume_vector, setplay.get(), internal->pitch_scale);
				setplayback.unref();
				setplay.set(-1);
			}

			if (!internal->stream_playbacks.is_empty() && internal->active.is_set()) {
				internal->process();
			}
			internal->ensure_playback_limit();
		} break;
	}
}

// Interacts with PhysicsSystem2D, so can only be called during _step.
StringName AudioStreamPlayer2D::_get_actual_bus() {
#ifndef PHYSICS_PLANAR_DISABLED
	if (area_mask == 0) {
		return internal->bus;
	}

	Vector2 global_pos = get_global_position();

	//check if any area is diverting sound into a bus
	Ref<World2D> world_2d = get_world_2d();
	ERR_FAIL_COND_V(world_2d.is_null(), SceneStringName(Master));

	PhysicsDirectSpaceState2D *space_state = PhysicsSystem2D::get_singleton()->space_get_direct_state(world_2d->get_space());
	ERR_FAIL_NULL_V(space_state, SceneStringName(Master));
	PS2DT::ShapeResult sr[MAX_INTERSECT_AREAS];

	PS2DT::PointParameters point_params;
	point_params.position = global_pos;
	point_params.collision_mask = area_mask;
	point_params.collide_with_bodies = false;
	point_params.collide_with_areas = true;

	int areas = space_state->intersect_point(point_params, sr, MAX_INTERSECT_AREAS);
	for (int i = 0; i < areas; i++) {
		if (!sr[i].collider_id.is_valid()) {
			continue;
		}
		Area2D *area2d = ObjectRegistry::get_instance<Area2D>(sr[i].collider_id);
		if (!area2d) {
			continue;
		}

		if (!area2d->is_overriding_audio_bus()) {
			continue;
		}

		return area2d->get_audio_bus_name();
	}
#endif // PHYSICS_PLANAR_DISABLED

	return internal->bus;
}

// Interacts with PhysicsSystem2D, so can only be called during _step
void AudioStreamPlayer2D::_update_panning() {
	if (!internal->active.is_set() || internal->stream.is_null()) {
		return;
	}

	Ref<World2D> world_2d = get_world_2d();
	ERR_FAIL_COND(world_2d.is_null());

	Vector2 global_pos = get_global_position();

	HashSet<Viewport *> viewports(world_2d->get_viewports());

	volume_vector.resize(4);
	volume_vector.write[0] = AudioFrame(0, 0);
	volume_vector.write[1] = AudioFrame(0, 0);
	volume_vector.write[2] = AudioFrame(0, 0);
	volume_vector.write[3] = AudioFrame(0, 0);

	StringName actual_bus = _get_actual_bus();

	for (Viewport *vp : viewports) {
		if (!vp->is_audio_listener_2d()) {
			continue;
		}
		//compute matrix to convert to screen
		Vector2 screen_size = vp->get_visible_rect().size;
		Vector2 listener_in_global;
		Vector2 relative_to_listener;

		//screen in global is used for attenuation
		AudioListener2D *listener = vp->get_audio_listener_2d();
		Transform2D full_canvas_transform = vp->get_global_canvas_transform() * vp->get_canvas_transform();
		if (listener) {
			listener_in_global = listener->get_global_position();
			relative_to_listener = (global_pos - listener_in_global).rotated(-listener->get_global_rotation());
			relative_to_listener *= full_canvas_transform.get_scale(); // Default listener scales with canvas size, do the same here.
		} else {
			listener_in_global = full_canvas_transform.affine_inverse().xform(screen_size * 0.5);
			relative_to_listener = full_canvas_transform.xform(global_pos) - screen_size * 0.5;
		}

		float dist = global_pos.distance_to(listener_in_global); // Distance to listener, or screen if none.

		if (dist > max_distance) {
			continue; // Can't hear this sound in this viewport.
		}

		float multiplier = Math::pow(1.0f - dist / max_distance, attenuation);
		multiplier *= Math::db_to_linear(internal->volume_db); // Also apply player volume!

		float pan = relative_to_listener.x / screen_size.x;
		// Don't let the panning effect extend (too far) beyond the screen.
		pan = CLAMP(pan, -1, 1);

		// Bake in a constant factor here to allow the project setting defaults for 2d and 3d to be normalized to 1.0.
		pan *= panning_strength * cached_global_panning_strength * 0.5f;

		pan = CLAMP(pan + 0.5, 0.0, 1.0);

		float l = 1.0 - pan;
		float r = pan;

		const AudioFrame &prev_sample = volume_vector[0];
		AudioFrame new_sample = AudioFrame(l, r) * multiplier;

		volume_vector.write[0] = AudioFrame(MAX(prev_sample[0], new_sample[0]), MAX(prev_sample[1], new_sample[1]));
	}

	for (const Ref<AudioStreamPlayback> &playback : internal->stream_playbacks) {
		AudioSystem::get_singleton()->set_playback_bus_exclusive(playback, actual_bus, volume_vector);
	}

	for (const Ref<AudioStreamPlayback> &playback : internal->stream_playbacks) {
		AudioSystem::get_singleton()->set_playback_pitch_scale(playback, internal->pitch_scale);
		if (playback->get_is_sample() && playback->get_sample_playback().is_valid()) {
			Ref<AudioSamplePlayback> sample_playback = playback->get_sample_playback();
			AudioSystem::get_singleton()->update_sample_playback_pitch_scale(sample_playback, internal->pitch_scale);
		}
	}

	last_mix_count = AudioSystem::get_singleton()->get_mix_count();
}

void AudioStreamPlayer2D::set_stream(Ref<AudioStream> p_stream) {
	internal->set_stream(p_stream);
}

Ref<AudioStream> AudioStreamPlayer2D::get_stream() const {
	return internal->stream;
}

void AudioStreamPlayer2D::set_volume_db(float p_volume) {
	ERR_FAIL_COND_MSG(Math::is_nan(p_volume), "Volume can't be set to NaN.");
	internal->volume_db = p_volume;
}

float AudioStreamPlayer2D::get_volume_db() const {
	return internal->volume_db;
}

void AudioStreamPlayer2D::set_volume_linear(float p_volume) {
	set_volume_db(Math::linear_to_db(p_volume));
}

float AudioStreamPlayer2D::get_volume_linear() const {
	return Math::db_to_linear(get_volume_db());
}

void AudioStreamPlayer2D::set_pitch_scale(float p_pitch_scale) {
	internal->set_pitch_scale(p_pitch_scale);
}

float AudioStreamPlayer2D::get_pitch_scale() const {
	return internal->pitch_scale;
}

void AudioStreamPlayer2D::play(float p_from_pos) {
	Ref<AudioStreamPlayback> stream_playback = internal->play_basic();
	if (stream_playback.is_null()) {
		return;
	}
	setplayback = stream_playback;
	setplay.set(p_from_pos);

	// Sample handling.
	if (stream_playback->get_is_sample() && stream_playback->get_sample_playback().is_valid()) {
		Ref<AudioSamplePlayback> sample_playback = stream_playback->get_sample_playback();
		sample_playback->offset = p_from_pos;
		sample_playback->bus = _get_actual_bus();

		AudioSystem::get_singleton()->start_sample_playback(sample_playback);
	}
}

void AudioStreamPlayer2D::seek(float p_seconds) {
	internal->seek(p_seconds);
}

void AudioStreamPlayer2D::stop() {
	setplay.set(-1);
	internal->stop_basic();
}

bool AudioStreamPlayer2D::is_playing() const {
	if (setplay.get() >= 0) {
		return true; // play() has been called this frame, but no playback exists just yet.
	}
	return internal->is_playing();
}

float AudioStreamPlayer2D::get_playback_position() {
	if (setplay.get() >= 0) {
		return setplay.get(); // play() has been called this frame, but no playback exists just yet.
	}
	return internal->get_playback_position();
}

void AudioStreamPlayer2D::set_bus(const StringName &p_bus) {
	internal->bus = p_bus; // This will be pushed to the audio server during the next physics timestep, which is fast enough.
}

StringName AudioStreamPlayer2D::get_bus() const {
	return internal->get_bus();
}

void AudioStreamPlayer2D::set_autoplay(bool p_enable) {
	internal->autoplay = p_enable;
}

bool AudioStreamPlayer2D::is_autoplay_enabled() const {
	return internal->autoplay;
}

void AudioStreamPlayer2D::_set_playing(bool p_enable) {
	internal->set_playing(p_enable);
}

void AudioStreamPlayer2D::set_max_distance(float p_pixels) {
	ERR_FAIL_COND(p_pixels <= 0.0);
	max_distance = p_pixels;
}

float AudioStreamPlayer2D::get_max_distance() const {
	return max_distance;
}

void AudioStreamPlayer2D::set_attenuation(float p_curve) {
	attenuation = p_curve;
}

float AudioStreamPlayer2D::get_attenuation() const {
	return attenuation;
}

void AudioStreamPlayer2D::set_area_mask(uint32_t p_mask) {
	area_mask = p_mask;
}

uint32_t AudioStreamPlayer2D::get_area_mask() const {
	return area_mask;
}

void AudioStreamPlayer2D::set_stream_paused(bool p_pause) {
	internal->set_stream_paused(p_pause);
}

bool AudioStreamPlayer2D::get_stream_paused() const {
	return internal->get_stream_paused();
}

bool AudioStreamPlayer2D::has_stream_playback() {
	return internal->has_stream_playback();
}

Ref<AudioStreamPlayback> AudioStreamPlayer2D::get_stream_playback() {
	return internal->get_stream_playback();
}

void AudioStreamPlayer2D::set_max_polyphony(int p_max_polyphony) {
	internal->set_max_polyphony(p_max_polyphony);
}

int AudioStreamPlayer2D::get_max_polyphony() const {
	return internal->max_polyphony;
}

void AudioStreamPlayer2D::set_panning_strength(float p_panning_strength) {
	ERR_FAIL_COND_MSG(p_panning_strength < 0, "Panning strength must be a positive number.");
	panning_strength = p_panning_strength;
}

float AudioStreamPlayer2D::get_panning_strength() const {
	return panning_strength;
}

AuSE::PlaybackType AudioStreamPlayer2D::get_playback_type() const {
	return internal->get_playback_type();
}

void AudioStreamPlayer2D::set_playback_type(AuSE::PlaybackType p_playback_type) {
	internal->set_playback_type(p_playback_type);
}

bool AudioStreamPlayer2D::_set(const StringName &p_name, const Variant &p_value) {
	return internal->set(p_name, p_value);
}

bool AudioStreamPlayer2D::_get(const StringName &p_name, Variant &r_ret) const {
	return internal->get(p_name, r_ret);
}

void AudioStreamPlayer2D::_get_property_list(List<PropertyInfo> *p_list) const {
	internal->get_property_list(p_list);
}

void AudioStreamPlayer2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_stream", "stream"), &AudioStreamPlayer2D::set_stream);
	ClassRegistry::bind_method(D_METHOD("get_stream"), &AudioStreamPlayer2D::get_stream);

	ClassRegistry::bind_method(D_METHOD("set_volume_db", "volume_db"), &AudioStreamPlayer2D::set_volume_db);
	ClassRegistry::bind_method(D_METHOD("get_volume_db"), &AudioStreamPlayer2D::get_volume_db);

	ClassRegistry::bind_method(D_METHOD("set_volume_linear", "volume_linear"), &AudioStreamPlayer2D::set_volume_linear);
	ClassRegistry::bind_method(D_METHOD("get_volume_linear"), &AudioStreamPlayer2D::get_volume_linear);

	ClassRegistry::bind_method(D_METHOD("set_pitch_scale", "pitch_scale"), &AudioStreamPlayer2D::set_pitch_scale);
	ClassRegistry::bind_method(D_METHOD("get_pitch_scale"), &AudioStreamPlayer2D::get_pitch_scale);

	ClassRegistry::bind_method(D_METHOD("play", "from_position"), &AudioStreamPlayer2D::play, DEFVAL(0.0));
	ClassRegistry::bind_method(D_METHOD("seek", "to_position"), &AudioStreamPlayer2D::seek);
	ClassRegistry::bind_method(D_METHOD("stop"), &AudioStreamPlayer2D::stop);

	ClassRegistry::bind_method(D_METHOD("is_playing"), &AudioStreamPlayer2D::is_playing);
	ClassRegistry::bind_method(D_METHOD("get_playback_position"), &AudioStreamPlayer2D::get_playback_position);

	ClassRegistry::bind_method(D_METHOD("set_bus", "bus"), &AudioStreamPlayer2D::set_bus);
	ClassRegistry::bind_method(D_METHOD("get_bus"), &AudioStreamPlayer2D::get_bus);

	ClassRegistry::bind_method(D_METHOD("set_autoplay", "enable"), &AudioStreamPlayer2D::set_autoplay);
	ClassRegistry::bind_method(D_METHOD("is_autoplay_enabled"), &AudioStreamPlayer2D::is_autoplay_enabled);

	ClassRegistry::bind_method(D_METHOD("set_playing", "enable"), &AudioStreamPlayer2D::_set_playing);

	ClassRegistry::bind_method(D_METHOD("set_max_distance", "pixels"), &AudioStreamPlayer2D::set_max_distance);
	ClassRegistry::bind_method(D_METHOD("get_max_distance"), &AudioStreamPlayer2D::get_max_distance);

	ClassRegistry::bind_method(D_METHOD("set_attenuation", "curve"), &AudioStreamPlayer2D::set_attenuation);
	ClassRegistry::bind_method(D_METHOD("get_attenuation"), &AudioStreamPlayer2D::get_attenuation);

	ClassRegistry::bind_method(D_METHOD("set_area_mask", "mask"), &AudioStreamPlayer2D::set_area_mask);
	ClassRegistry::bind_method(D_METHOD("get_area_mask"), &AudioStreamPlayer2D::get_area_mask);

	ClassRegistry::bind_method(D_METHOD("set_stream_paused", "pause"), &AudioStreamPlayer2D::set_stream_paused);
	ClassRegistry::bind_method(D_METHOD("get_stream_paused"), &AudioStreamPlayer2D::get_stream_paused);

	ClassRegistry::bind_method(D_METHOD("set_max_polyphony", "max_polyphony"), &AudioStreamPlayer2D::set_max_polyphony);
	ClassRegistry::bind_method(D_METHOD("get_max_polyphony"), &AudioStreamPlayer2D::get_max_polyphony);

	ClassRegistry::bind_method(D_METHOD("set_panning_strength", "panning_strength"), &AudioStreamPlayer2D::set_panning_strength);
	ClassRegistry::bind_method(D_METHOD("get_panning_strength"), &AudioStreamPlayer2D::get_panning_strength);

	ClassRegistry::bind_method(D_METHOD("has_stream_playback"), &AudioStreamPlayer2D::has_stream_playback);
	ClassRegistry::bind_method(D_METHOD("get_stream_playback"), &AudioStreamPlayer2D::get_stream_playback);

	ClassRegistry::bind_method(D_METHOD("set_playback_type", "playback_type"), &AudioStreamPlayer2D::set_playback_type);
	ClassRegistry::bind_method(D_METHOD("get_playback_type"), &AudioStreamPlayer2D::get_playback_type);

	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "stream", PROPERTY_HINT_RESOURCE_TYPE, AudioStream::get_static_class_name()), "set_stream", "get_stream");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "volume_db", PROPERTY_HINT_RANGE, "-80,24,or_greater,suffix:dB"), "set_volume_db", "get_volume_db");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "volume_linear", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_volume_linear", "get_volume_linear");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "pitch_scale", PROPERTY_HINT_RANGE, "0.01,4,0.01,or_greater"), "set_pitch_scale", "get_pitch_scale");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "playing", PROPERTY_HINT_ONESHOT, "", PROPERTY_USAGE_EDITOR), "set_playing", "is_playing");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "autoplay"), "set_autoplay", "is_autoplay_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "stream_paused", PROPERTY_HINT_NONE, ""), "set_stream_paused", "get_stream_paused");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "max_distance", PROPERTY_HINT_RANGE, "1,4096,1,or_greater,exp,suffix:px"), "set_max_distance", "get_max_distance");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "attenuation", PROPERTY_HINT_EXP_EASING, "attenuation"), "set_attenuation", "get_attenuation");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "max_polyphony", PROPERTY_HINT_NONE, ""), "set_max_polyphony", "get_max_polyphony");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "panning_strength", PROPERTY_HINT_RANGE, "0,3,0.01,or_greater"), "set_panning_strength", "get_panning_strength");
	ADD_PROPERTY(PropertyInfo(Variant::STRING_NAME, "bus", PROPERTY_HINT_AUDIO_BUS), "set_bus", "get_bus");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "area_mask", PROPERTY_HINT_LAYERS_2D_PHYSICS), "set_area_mask", "get_area_mask");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "playback_type", PROPERTY_HINT_ENUM, "Default,Stream,Sample"), "set_playback_type", "get_playback_type");

	ADD_SIGNAL(MethodInfo("finished"));
}

AudioStreamPlayer2D::AudioStreamPlayer2D() {
	internal = memnew(AudioStreamPlayerInternal(this, callable_mp(this, &AudioStreamPlayer2D::play), callable_mp(this, &AudioStreamPlayer2D::stop), true));
	cached_global_panning_strength = GLOBAL_GET_CACHED(float, "audio/general/planar_panning_strength");
	set_hide_clip_children(true);
}

AudioStreamPlayer2D::~AudioStreamPlayer2D() {
	memdelete(internal);
}
