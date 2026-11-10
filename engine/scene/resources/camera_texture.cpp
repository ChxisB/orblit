/**************************************************************************/
/*  camera_texture.cpp                                                    */

#include "camera_texture.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "systems/camera/camera_feed.h"
#include "systems/rendering/rendering_system.h"

void CameraTexture::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_camera_feed_id", "feed_id"), &CameraTexture::set_camera_feed_id);
	ClassRegistry::bind_method(D_METHOD("get_camera_feed_id"), &CameraTexture::get_camera_feed_id);

	ClassRegistry::bind_method(D_METHOD("set_which_feed", "which_feed"), &CameraTexture::set_which_feed);
	ClassRegistry::bind_method(D_METHOD("get_which_feed"), &CameraTexture::get_which_feed);

	ClassRegistry::bind_method(D_METHOD("set_camera_active", "active"), &CameraTexture::set_camera_active);
	ClassRegistry::bind_method(D_METHOD("get_camera_active"), &CameraTexture::get_camera_active);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "camera_feed_id"), "set_camera_feed_id", "get_camera_feed_id");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "which_feed"), "set_which_feed", "get_which_feed");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "camera_is_active"), "set_camera_active", "get_camera_active");
	ADD_PROPERTY_DEFAULT("camera_is_active", false);
}

void CameraTexture::_on_format_changed() {
	// FIXME: `emit_changed` is more appropriate, but causes errors for some reason.
	callable_mp((Resource *)this, &Resource::emit_changed).call_deferred();
}

int CameraTexture::get_width() const {
	Ref<CameraFeed> feed = CameraSystem::get_singleton()->get_feed_by_id(camera_feed_id);
	if (feed.is_valid()) {
		return feed->get_base_width();
	} else {
		return 0;
	}
}

int CameraTexture::get_height() const {
	Ref<CameraFeed> feed = CameraSystem::get_singleton()->get_feed_by_id(camera_feed_id);
	if (feed.is_valid()) {
		return feed->get_base_height();
	} else {
		return 0;
	}
}

bool CameraTexture::has_alpha() const {
	return false;
}

RID CameraTexture::get_rid() const {
	Ref<CameraFeed> feed = CameraSystem::get_singleton()->get_feed_by_id(camera_feed_id);
	if (feed.is_valid()) {
		return feed->get_texture(which_feed);
	} else {
		if (_texture.is_null()) {
			_texture = RenderingSystem::get_singleton()->texture_2d_placeholder_create();
		}
		return _texture;
	}
}

Ref<Image> CameraTexture::get_image() const {
	return RenderingSystem::get_singleton()->texture_2d_get(get_rid());
}

void CameraTexture::set_camera_feed_id(int p_new_id) {
	Ref<CameraFeed> feed = CameraSystem::get_singleton()->get_feed_by_id(camera_feed_id);
	if (feed.is_valid()) {
		if (feed->is_connected("format_changed", callable_mp(this, &CameraTexture::_on_format_changed))) {
			feed->disconnect("format_changed", callable_mp(this, &CameraTexture::_on_format_changed));
		}
	}

	camera_feed_id = p_new_id;

	feed = CameraSystem::get_singleton()->get_feed_by_id(camera_feed_id);
	if (feed.is_valid()) {
		feed->connect("format_changed", callable_mp(this, &CameraTexture::_on_format_changed));
	}

	notify_property_list_changed();
	callable_mp((Resource *)this, &Resource::emit_changed).call_deferred();
}

int CameraTexture::get_camera_feed_id() const {
	return camera_feed_id;
}

void CameraTexture::set_which_feed(CameraSystem::FeedImage p_which) {
	which_feed = p_which;
	notify_property_list_changed();
	callable_mp((Resource *)this, &Resource::emit_changed).call_deferred();
}

CameraSystem::FeedImage CameraTexture::get_which_feed() const {
	return which_feed;
}

void CameraTexture::set_camera_active(bool p_active) {
	Ref<CameraFeed> feed = CameraSystem::get_singleton()->get_feed_by_id(camera_feed_id);
	if (feed.is_valid()) {
		feed->set_active(p_active);
		notify_property_list_changed();
		callable_mp((Resource *)this, &Resource::emit_changed).call_deferred();
	}
}

bool CameraTexture::get_camera_active() const {
	Ref<CameraFeed> feed = CameraSystem::get_singleton()->get_feed_by_id(camera_feed_id);
	if (feed.is_valid()) {
		return feed->is_active();
	} else {
		return false;
	}
}

CameraTexture::CameraTexture() {
	// Note: When any CameraTexture is created, we need to automatically activate monitoring
	//       of camera feeds. This may incur a small lag spike, so it may be preferable to
	//       enable it manually before creating the camera texture.
	CameraSystem::get_singleton()->set_monitoring_feeds(true);
}

CameraTexture::~CameraTexture() {
	if (_texture.is_valid()) {
		ERR_FAIL_NULL(RenderingSystem::get_singleton());
		RenderingSystem::get_singleton()->free_rid(_texture);
	}
}
