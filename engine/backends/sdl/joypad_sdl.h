/**************************************************************************/
/*  joypad_sdl.h                                                          */

#pragma once

#include "core/input/input.h"

typedef uint32_t SDL_JoystickID;
typedef struct SDL_Joystick SDL_Joystick;
typedef struct SDL_Gamepad SDL_Gamepad;

class JoypadSDL {
public:
	~JoypadSDL();

	Error initialize();
	void process_events();

private:
	class Joypad : public Input::JoypadFeatures {
	public:
		bool attached = false;
		StringName guid;

		SDL_JoystickID sdl_instance_idx;

		bool supports_force_feedback = false;
		bool supports_motion_sensors = false;
		uint64_t ff_effect_timestamp = 0;

		virtual bool has_joy_light() const override;
		virtual void set_joy_light(const Color &p_color) override;

		virtual bool has_joy_motion_sensors() const override;
		virtual void set_joy_motion_sensors_enabled(bool p_enable) override;

		virtual bool has_joy_vibration() const override;

		virtual int get_joy_num_touchpads() const override;

		SDL_Joystick *get_sdl_joystick() const;
		SDL_Gamepad *get_sdl_gamepad() const;
	};

	Joypad joypads[Input::JOYPADS_MAX];
	HashMap<SDL_JoystickID, int> sdl_instance_id_to_joypad_id;

	void close_joypad(int p_pad_idx);
};
