/**************************************************************************/
/*  audio_stream_preview.h                                                */

#pragma once

#include "core/containers/safe_refcount.h"
#include "core/object/worker_thread_pool.h"
#include "scene/resources/audio/audio_stream.h"
#include "scene/stage/entity.h"

class AudioStreamPreview : public RefCounted {
	ORBLIT_CLASS(AudioStreamPreview, RefCounted);
	friend class AudioStream;
	LocalVector<uint8_t> preview;
	float length;

	friend class AudioStreamPreviewGenerator;
	uint64_t version = 1;

public:
	uint64_t get_version() const { return version; }
	float get_length() const;
	float get_max(float p_time, float p_time_next) const;
	float get_min(float p_time, float p_time_next) const;

	AudioStreamPreview();
};

class AudioStreamPreviewGenerator : public Entity {
	ORBLIT_CLASS(AudioStreamPreviewGenerator, Entity);

	static AudioStreamPreviewGenerator *singleton;

	struct Preview {
		Ref<AudioStreamPreview> preview;
		Ref<AudioStream> base_stream;
		Ref<AudioStreamPlayback> playback;
		SafeFlag generating;
		ObjectID id;
		WorkerThreadPool::TaskID task_id = WorkerThreadPool::INVALID_TASK_ID;

		// Needed for the bookkeeping of the Map
		void operator=(const Preview &p_rhs) {
			preview = p_rhs.preview;
			base_stream = p_rhs.base_stream;
			playback = p_rhs.playback;
			generating.set_to(generating.is_set());
			id = p_rhs.id;
			task_id = p_rhs.task_id;
		}
		Preview(const Preview &p_rhs) {
			preview = p_rhs.preview;
			base_stream = p_rhs.base_stream;
			playback = p_rhs.playback;
			generating.set_to(generating.is_set());
			id = p_rhs.id;
			task_id = p_rhs.task_id;
		}
		Preview() {}
	};

	HashMap<ObjectID, Preview> previews;

	static void _preview_thread(void *p_preview);

	void _update_emit(ObjectID p_id);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	static AudioStreamPreviewGenerator *get_singleton() { return singleton; }

	Ref<AudioStreamPreview> generate_preview(const Ref<AudioStream> &p_stream);

	AudioStreamPreviewGenerator();
	~AudioStreamPreviewGenerator();
};
