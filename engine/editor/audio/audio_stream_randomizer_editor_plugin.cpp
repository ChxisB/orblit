/**************************************************************************/
/*  audio_stream_randomizer_editor_plugin.cpp                             */

#include "audio_stream_randomizer_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "editor/editor_node.h"
#include "editor/editor_undo_redo_manager.h"
#include "scene/resources/audio/audio_stream_randomizer.h"

void AudioStreamRandomizerEditorPlugin::edit(Object *p_object) {
}

bool AudioStreamRandomizerEditorPlugin::handles(Object *p_object) const {
	return false;
}

void AudioStreamRandomizerEditorPlugin::make_visible(bool p_visible) {
}

void AudioStreamRandomizerEditorPlugin::_move_stream_array_element(Object *p_undo_redo, Object *p_edited, const String &p_array_prefix, int p_from_index, int p_to_pos) {
	EditorUndoRedoManager *undo_redo_man = Object::cast_to<EditorUndoRedoManager>(p_undo_redo);
	ERR_FAIL_NULL(undo_redo_man);

	AudioStreamRandomizer *randomizer = Object::cast_to<AudioStreamRandomizer>(p_edited);
	if (!randomizer) {
		return;
	}

	// Compute the array indices to save.
	int begin = 0;
	int end;
	if (p_array_prefix == "stream_") {
		end = randomizer->get_streams_count();
	} else {
		ERR_FAIL_MSG("Invalid array prefix for AudioStreamRandomizer.");
	}
	if (p_from_index < 0) {
		// Adding new.
		if (p_to_pos >= 0) {
			begin = p_to_pos;
		} else {
			end = 0; // Nothing to save when adding at the end.
		}
	} else if (p_to_pos < 0) {
		// Removing.
		begin = p_from_index;
	} else {
		// Moving.
		begin = MIN(p_from_index, p_to_pos);
		end = MIN(MAX(p_from_index, p_to_pos) + 1, end);
	}

#define ADD_UNDO(obj, property) undo_redo_man->add_undo_property(obj, property, obj->get(property));
	// Save layers' properties.
	if (p_from_index < 0) {
		undo_redo_man->add_undo_method(randomizer, "remove_stream", p_to_pos < 0 ? randomizer->get_streams_count() : p_to_pos);
	} else if (p_to_pos < 0) {
		undo_redo_man->add_undo_method(randomizer, "add_stream", p_from_index, Ref<AudioStream>());
	}

	List<PropertyInfo> properties;
	randomizer->get_property_list(&properties);
	for (PropertyInfo pi : properties) {
		if (pi.name.begins_with(p_array_prefix)) {
			String str = pi.name.trim_prefix(p_array_prefix);
			int to_char_index = 0;
			while (to_char_index < str.length()) {
				if (str[to_char_index] < '0' || str[to_char_index] > '9') {
					break;
				}
				to_char_index++;
			}
			if (to_char_index > 0) {
				int array_index = str.left(to_char_index).to_int();
				if (array_index >= begin && array_index < end) {
					ADD_UNDO(randomizer, pi.name);
				}
			}
		}
	}
#undef ADD_UNDO

	if (p_from_index < 0) {
		undo_redo_man->add_do_method(randomizer, "add_stream", p_to_pos, Ref<AudioStream>());
	} else if (p_to_pos < 0) {
		undo_redo_man->add_do_method(randomizer, "remove_stream", p_from_index);
	} else {
		undo_redo_man->add_do_method(randomizer, "move_stream", p_from_index, p_to_pos);
	}
}

AudioStreamRandomizerEditorPlugin::AudioStreamRandomizerEditorPlugin() {
	EditorNode::get_editor_data().add_move_array_element_function(SNAME("AudioStreamRandomizer"), callable_mp(this, &AudioStreamRandomizerEditorPlugin::_move_stream_array_element));
}
