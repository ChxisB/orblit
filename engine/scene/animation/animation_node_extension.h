/**************************************************************************/
/*  animation_node_extension.h                                            */

#pragma once

#include "scene/animation/animation_tree.h"

class AnimationNodeExtension : public AnimationNode {
	ORBLIT_CLASS(AnimationNodeExtension, AnimationNode);

public:
	virtual NodeTimeInfo _process(ProcessState &p_process_state, AnimationNodeInstance &p_instance, const AnimationMixer::PlaybackInfo &p_playback_info, bool p_test_only = false) override;

	static bool is_looping(const PackedFloat32Array &p_node_info);
	static double get_remaining_time(const PackedFloat32Array &p_node_info, bool p_break_loop = false);

protected:
	static void register_members();

	ORBLIT_VIRTUAL2R_REQUIRED(PackedFloat32Array, _process_animation_node, PackedFloat64Array, bool);

private:
	static AnimationNode::NodeTimeInfo _array_to_node_time_info(const PackedFloat32Array &p_array);
	static PackedFloat64Array _playback_info_to_array(const AnimationMixer::PlaybackInfo &p_playback_info);
};
