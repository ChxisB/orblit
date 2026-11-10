/**************************************************************************/
/*  gltf_animation.h                                                      */

#pragma once

#include "scene/resources/animation.h"

class GLTFAnimation : public Resource {
	ORBLIT_CLASS(GLTFAnimation, Resource);

protected:
	static void register_members();

public:
	enum Interpolation {
		INTERP_LINEAR,
		INTERP_STEP,
		INTERP_CATMULLROMSPLINE,
		INTERP_CUBIC_SPLINE,
	};

	template <typename T>
	struct Channel {
		Interpolation interpolation = INTERP_LINEAR;
		Vector<double> times;
		Vector<T> values;
	};

	struct NodeTrack {
		Channel<Vector3> position_track;
		Channel<Quaternion> rotation_track;
		Channel<Vector3> scale_track;
		Vector<Channel<real_t>> weight_tracks;
	};

	String original_name;
	bool loop = false;
	HashMap<int, NodeTrack> node_tracks;
	HashMap<String, Channel<Variant>> pointer_tracks;
	Dictionary additional_data;

public:
	static Interpolation orblit_to_gltf_interpolation(const Ref<Animation> &p_orblit_animation, int32_t p_orblit_anim_track_index);
	static Animation::InterpolationType gltf_to_orblit_interpolation(Interpolation p_gltf_interpolation);

	String get_original_name();
	void set_original_name(const String &p_name);

	bool get_loop() const;
	void set_loop(bool p_val);

	HashMap<int, GLTFAnimation::NodeTrack> &get_node_tracks();
	HashMap<String, GLTFAnimation::Channel<Variant>> &get_pointer_tracks();
	bool is_empty_of_tracks() const;

	Variant get_additional_data(const StringName &p_extension_name);
	void set_additional_data(const StringName &p_extension_name, Variant p_additional_data);

	GLTFAnimation();
};
