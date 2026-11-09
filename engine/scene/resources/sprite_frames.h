/**************************************************************************/
/*  sprite_frames.h                                                       */

#pragma once

#include "scene/resources/texture.h"

static const float SPRITE_FRAME_MINIMUM_DURATION = 0.01;

class SpriteFrames : public Resource {
	ORBLIT_CLASS(SpriteFrames, Resource);

public:
	enum LoopMode {
		LOOP_NONE,
		LOOP_LINEAR,
		LOOP_PINGPONG,
	};

private:
	struct Frame {
		Ref<Texture2D> texture;
		float duration = 1.0;
	};

	struct Anim {
		double speed = 5.0;
		LoopMode loop = LoopMode::LOOP_LINEAR;
		Vector<Frame> frames;
	};

	HashMap<StringName, Anim> animations;

	Array _get_animations() const;
	void _set_animations(const Array &p_animations);

protected:
	static void register_members();

public:
	void add_animation(const StringName &p_anim);
	bool has_animation(const StringName &p_anim) const;
	void duplicate_animation(const StringName &p_from, const StringName &p_to);
	void remove_animation(const StringName &p_anim);
	void rename_animation(const StringName &p_prev, const StringName &p_next);

	void get_animation_list(List<StringName> *r_animations) const;
	Vector<String> get_animation_names() const;

	void set_animation_speed(const StringName &p_anim, double p_fps);
	double get_animation_speed(const StringName &p_anim) const;

	void set_animation_loop_mode(const StringName &p_anim, LoopMode p_loop_mode);
	LoopMode get_animation_loop_mode(const StringName &p_anim) const;

	void add_frame(const StringName &p_anim, const Ref<Texture2D> &p_texture, float p_duration = 1.0, int p_at_pos = -1);
	void set_frame(const StringName &p_anim, int p_idx, const Ref<Texture2D> &p_texture, float p_duration = 1.0);
	void remove_frame(const StringName &p_anim, int p_idx);

	int get_frame_count(const StringName &p_anim) const;

	_FORCE_INLINE_ Ref<Texture2D> get_frame_texture(const StringName &p_anim, int p_idx) const {
		HashMap<StringName, Anim>::ConstIterator E = animations.find(p_anim);
		ERR_FAIL_COND_V_MSG(!E, Ref<Texture2D>(), "Animation '" + String(p_anim) + "' doesn't exist.");
		ERR_FAIL_COND_V(p_idx < 0, Ref<Texture2D>());
		if (p_idx >= E->value.frames.size()) {
			return Ref<Texture2D>();
		}

		return E->value.frames[p_idx].texture;
	}

	_FORCE_INLINE_ float get_frame_duration(const StringName &p_anim, int p_idx) const {
		HashMap<StringName, Anim>::ConstIterator E = animations.find(p_anim);
		ERR_FAIL_COND_V_MSG(!E, 1.0, "Animation '" + String(p_anim) + "' doesn't exist.");
		ERR_FAIL_COND_V(p_idx < 0, 1.0);
		if (p_idx >= E->value.frames.size()) {
			return 1.0;
		}

		return E->value.frames[p_idx].duration;
	}

	void clear(const StringName &p_anim);
	void clear_all();

#ifdef TOOLS_ENABLED
	virtual void get_argument_options(const StringName &p_function, int p_idx, List<String> *r_options) const override;
#endif

	SpriteFrames();
};

VARIANT_ENUM_CAST(SpriteFrames::LoopMode);
