/**************************************************************************/
/*  camera_system.h                                                       */

#pragma once

#include "core/object/object.h"
#include "core/object/ref_counted.h"
#include "core/os/thread_safe.h"
#include "core/containers/rid.h"
#include "core/variant/type_info.h"

/**
	The camera server is a singleton object that gives access to the various
	camera feeds that can be used as the background for our environment.
**/

class CameraFeed;
template <typename T>
class TypedArray;

class CameraSystem : public Object {
	ORBLIT_CLASS(CameraSystem, Object);
	_THREAD_SAFE_CLASS_

public:
	enum FeedImage {
		FEED_RGBA_IMAGE = 0,
		FEED_YCBCR_IMAGE = 0,
		FEED_Y_IMAGE = 0,
		FEED_CBCR_IMAGE = 1,
		FEED_IMAGES = 2
	};

	typedef CameraSystem *(*CreateFunc)();
	static inline constexpr const char feeds_updated_signal_name[] = "camera_feeds_updated";

private:
protected:
	static CreateFunc create_func;

	bool monitoring_feeds = false;
	Vector<Ref<CameraFeed>> feeds;

	static CameraSystem *singleton;

	static void register_members();

	template <typename T>
	static CameraSystem *_create_builtin() {
		return memnew(T);
	}

public:
	static CameraSystem *get_singleton();

	template <typename T>
	static void make_default() {
		create_func = _create_builtin<T>;
	}

	static CameraSystem *create() {
		CameraSystem *server = create_func ? create_func() : memnew(CameraSystem);
		return server;
	}

	virtual void set_monitoring_feeds(bool p_monitoring_feeds);
	_FORCE_INLINE_ bool is_monitoring_feeds() const { return monitoring_feeds; }

	// Right now we identify our feed by it's ID when it's used in the background.
	// May see if we can change this to purely relying on CameraFeed objects or by name.
	int get_free_id();
	int get_feed_index(int p_id);
	Ref<CameraFeed> get_feed_by_id(int p_id);

	// Add and remove feeds.
	void add_feed(const Ref<CameraFeed> &p_feed);
	void remove_feed(const Ref<CameraFeed> &p_feed);

	// Get our feeds.
	Ref<CameraFeed> get_feed(int p_index);
	int get_feed_count();
	TypedArray<CameraFeed> get_feeds();

	// Intended for use with custom CameraSystem implementation.
	RID feed_texture(int p_id, FeedImage p_texture);

	// Platform lifecycle callbacks (virtual, default empty implementation).
	virtual void handle_application_pause() {}
	virtual void handle_application_resume() {}
	virtual void handle_display_rotation_change(int p_orientation) { (void)p_orientation; }

	CameraSystem();
	~CameraSystem();
};

VARIANT_ENUM_CAST(CameraSystem::FeedImage);
