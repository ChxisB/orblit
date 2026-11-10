/**************************************************************************/
/*  camera_system.cpp                                                     */

#include "camera_system.h"

#include "core/object/class_registry.h"
#include "core/variant/typed_array.h"
#include "systems/camera/camera_feed.h"

////////////////////////////////////////////////////////
// CameraSystem

CameraSystem::CreateFunc CameraSystem::create_func = nullptr;

void CameraSystem::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_monitoring_feeds", "is_monitoring_feeds"), &CameraSystem::set_monitoring_feeds);
	ClassRegistry::bind_method(D_METHOD("is_monitoring_feeds"), &CameraSystem::is_monitoring_feeds);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "monitoring_feeds"), "set_monitoring_feeds", "is_monitoring_feeds");
	ADD_PROPERTY_DEFAULT("monitoring_feeds", false);

	ClassRegistry::bind_method(D_METHOD("get_feed", "index"), &CameraSystem::get_feed);
	ClassRegistry::bind_method(D_METHOD("get_feed_count"), &CameraSystem::get_feed_count);
	ClassRegistry::bind_method(D_METHOD("feeds"), &CameraSystem::get_feeds);

	ClassRegistry::bind_method(D_METHOD("add_feed", "feed"), &CameraSystem::add_feed);
	ClassRegistry::bind_method(D_METHOD("remove_feed", "feed"), &CameraSystem::remove_feed);

	ADD_SIGNAL(MethodInfo("camera_feed_added", PropertyInfo(Variant::INT, "id")));
	ADD_SIGNAL(MethodInfo("camera_feed_removed", PropertyInfo(Variant::INT, "id")));
	ADD_SIGNAL(MethodInfo(feeds_updated_signal_name));

	BIND_ENUM_CONSTANT(FEED_RGBA_IMAGE);
	BIND_ENUM_CONSTANT(FEED_YCBCR_IMAGE);
	BIND_ENUM_CONSTANT(FEED_Y_IMAGE);
	BIND_ENUM_CONSTANT(FEED_CBCR_IMAGE);
}

CameraSystem *CameraSystem::singleton = nullptr;

CameraSystem *CameraSystem::get_singleton() {
	return singleton;
}

void CameraSystem::set_monitoring_feeds(bool p_monitoring_feeds) {
	monitoring_feeds = p_monitoring_feeds;
}

int CameraSystem::get_free_id() {
	bool id_exists = true;
	int newid = 0;

	// find a free id
	while (id_exists) {
		newid++;
		id_exists = false;
		for (int i = 0; i < feeds.size() && !id_exists; i++) {
			if (feeds[i]->get_id() == newid) {
				id_exists = true;
			};
		};
	};

	return newid;
}

int CameraSystem::get_feed_index(int p_id) {
	ERR_FAIL_COND_V_MSG(!monitoring_feeds, -1, "CameraSystem is not actively monitoring feeds; call set_monitoring_feeds(true) first.");

	for (int i = 0; i < feeds.size(); i++) {
		if (feeds[i]->get_id() == p_id) {
			return i;
		};
	};

	return -1;
}

Ref<CameraFeed> CameraSystem::get_feed_by_id(int p_id) {
	ERR_FAIL_COND_V_MSG(!monitoring_feeds, nullptr, "CameraSystem is not actively monitoring feeds; call set_monitoring_feeds(true) first.");

	int index = get_feed_index(p_id);

	if (index == -1) {
		return nullptr;
	} else {
		return feeds[index];
	}
}

void CameraSystem::add_feed(const Ref<CameraFeed> &p_feed) {
	ERR_FAIL_COND(p_feed.is_null());

	// add our feed
	feeds.push_back(p_feed);

	print_verbose("CameraSystem: Registered camera " + p_feed->get_name() + " with ID " + itos(p_feed->get_id()) + " and position " + itos(p_feed->get_position()) + " at index " + itos(feeds.size() - 1));

	// let whomever is interested know
	emit_signal(SNAME("camera_feed_added"), p_feed->get_id());
}

void CameraSystem::remove_feed(const Ref<CameraFeed> &p_feed) {
	for (int i = 0; i < feeds.size(); i++) {
		if (feeds[i] == p_feed) {
			int feed_id = p_feed->get_id();

			print_verbose("CameraSystem: Removed camera " + p_feed->get_name() + " with ID " + itos(feed_id) + " and position " + itos(p_feed->get_position()));

			// remove it from our array, if this results in our feed being unreferenced it will be destroyed
			feeds.remove_at(i);

			// let whomever is interested know
			emit_signal(SNAME("camera_feed_removed"), feed_id);
			return;
		};
	};
}

Ref<CameraFeed> CameraSystem::get_feed(int p_index) {
	ERR_FAIL_COND_V_MSG(!monitoring_feeds, nullptr, "CameraSystem is not actively monitoring feeds; call set_monitoring_feeds(true) first.");
	ERR_FAIL_INDEX_V(p_index, feeds.size(), nullptr);

	return feeds[p_index];
}

int CameraSystem::get_feed_count() {
	ERR_FAIL_COND_V_MSG(!monitoring_feeds, 0, "CameraSystem is not actively monitoring feeds; call set_monitoring_feeds(true) first.");
	return feeds.size();
}

TypedArray<CameraFeed> CameraSystem::get_feeds() {
	ERR_FAIL_COND_V_MSG(!monitoring_feeds, {}, "CameraSystem is not actively monitoring feeds; call set_monitoring_feeds(true) first.");
	TypedArray<CameraFeed> return_feeds;
	int cc = get_feed_count();
	return_feeds.resize(cc);

	for (int i = 0; i < feeds.size(); i++) {
		return_feeds[i] = get_feed(i);
	};

	return return_feeds;
}

RID CameraSystem::feed_texture(int p_id, CameraSystem::FeedImage p_texture) {
	ERR_FAIL_COND_V_MSG(!monitoring_feeds, RID(), "CameraSystem is not actively monitoring feeds; call set_monitoring_feeds(true) first.");
	int index = get_feed_index(p_id);
	ERR_FAIL_COND_V(index == -1, RID());

	Ref<CameraFeed> feed = get_feed(index);

	return feed->get_texture(p_texture);
}

CameraSystem::CameraSystem() {
	singleton = this;
}

CameraSystem::~CameraSystem() {
	singleton = nullptr;
}
