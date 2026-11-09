/**************************************************************************/
/*  animation_node_extension.cpp                                          */

#include "animation_node_extension.h"

#include "core/object/class_registry.h"

AnimationNode::NodeTimeInfo AnimationNodeExtension::_process(ProcessState &p_process_state, AnimationNodeInstance &p_instance, const AnimationMixer::PlaybackInfo &p_playback_info, bool p_test_only) {
	PackedFloat32Array r_ret;

	ORBLIT_VIRTUAL_CALL(
			_process_animation_node,
			_playback_info_to_array(p_playback_info),
			p_test_only,
			r_ret);

	return _array_to_node_time_info(r_ret);
}

bool AnimationNodeExtension::is_looping(const PackedFloat32Array &p_node_info) {
	return _array_to_node_time_info(p_node_info).is_looping();
}

double AnimationNodeExtension::get_remaining_time(const PackedFloat32Array &p_node_info, bool p_break_loop) {
	return _array_to_node_time_info(p_node_info).get_remain(p_break_loop);
}

void AnimationNodeExtension::register_members() {
	ClassRegistry::bind_static_method("AnimationNodeExtension", D_METHOD("is_looping", "node_info"), &AnimationNodeExtension::is_looping);
	ClassRegistry::bind_static_method("AnimationNodeExtension", D_METHOD("get_remaining_time", "node_info", "break_loop"), &AnimationNodeExtension::get_remaining_time);
	ORBLIT_VIRTUAL_BIND(_process_animation_node, "playback_info", "test_only");
}

AnimationNode::NodeTimeInfo AnimationNodeExtension::_array_to_node_time_info(const PackedFloat32Array &p_node_info) {
	ERR_FAIL_COND_V_MSG(p_node_info.size() != 6, AnimationNode::NodeTimeInfo(), "Invalid node info.");
	AnimationNode::NodeTimeInfo ret_val;
	ret_val.length = p_node_info[0];
	ret_val.position = p_node_info[1];
	ret_val.delta = p_node_info[2];
	ret_val.loop_mode = static_cast<Animation::LoopMode>(p_node_info[3]);
	ret_val.will_end = p_node_info[4] > 0.0;
	ret_val.is_infinity = p_node_info[5] > 0.0;
	return ret_val;
}

PackedFloat64Array AnimationNodeExtension::_playback_info_to_array(const AnimationMixer::PlaybackInfo &p_playback_info) {
	PackedFloat64Array playback_info_array;
	playback_info_array.push_back(p_playback_info.time);
	playback_info_array.push_back(p_playback_info.delta);
	playback_info_array.push_back(p_playback_info.start);
	playback_info_array.push_back(p_playback_info.end);
	playback_info_array.push_back(p_playback_info.seeked);
	playback_info_array.push_back(p_playback_info.is_external_seeking);
	playback_info_array.push_back(p_playback_info.looped_flag);
	playback_info_array.push_back(p_playback_info.weight);

	return playback_info_array;
}
