/**************************************************************************/
/*  camera_feed.cpp                                                       */

#include "camera_feed.h"

#include "core/object/class_registry.h"
#include "systems/rendering/rendering_system.h"

void CameraFeed::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_id"), &CameraFeed::get_id);

	ClassRegistry::bind_method(D_METHOD("is_active"), &CameraFeed::is_active);
	ClassRegistry::bind_method(D_METHOD("set_active", "active"), &CameraFeed::set_active);

	ClassRegistry::bind_method(D_METHOD("get_name"), &CameraFeed::get_name);
	ClassRegistry::bind_method(D_METHOD("set_name", "name"), &CameraFeed::set_name);

	ClassRegistry::bind_method(D_METHOD("get_position"), &CameraFeed::get_position);
	ClassRegistry::bind_method(D_METHOD("set_position", "position"), &CameraFeed::set_position);

	// Note, for transform some feeds may override what the user sets (such as ARKit)
	ClassRegistry::bind_method(D_METHOD("get_transform"), &CameraFeed::get_transform);
	ClassRegistry::bind_method(D_METHOD("set_transform", "transform"), &CameraFeed::set_transform);

	ClassRegistry::bind_method(D_METHOD("set_rgb_image", "rgb_image"), &CameraFeed::set_rgb_image);
	ClassRegistry::bind_method(D_METHOD("set_ycbcr_image", "ycbcr_image"), &CameraFeed::set_ycbcr_image);
	ClassRegistry::bind_method(D_METHOD("set_ycbcr_images", "y_image", "cbcr_image"), &CameraFeed::set_ycbcr_images);
	ClassRegistry::bind_method(D_METHOD("set_external", "width", "height"), &CameraFeed::set_external);
	ClassRegistry::bind_method(D_METHOD("get_texture_tex_id", "feed_image_type"), &CameraFeed::get_texture_tex_id);

	ClassRegistry::bind_method(D_METHOD("get_datatype"), &CameraFeed::get_datatype);

	ClassRegistry::bind_method(D_METHOD("get_formats"), &CameraFeed::get_formats);
	ClassRegistry::bind_method(D_METHOD("set_format", "index", "parameters"), &CameraFeed::set_format);

	ORBLIT_VIRTUAL_BIND(_activate_feed);
	ORBLIT_VIRTUAL_BIND(_deactivate_feed);
	ORBLIT_VIRTUAL_BIND(_set_format, "index", "parameters");
	ORBLIT_VIRTUAL_BIND(_get_formats);

	ADD_SIGNAL(MethodInfo("frame_changed"));
	ADD_SIGNAL(MethodInfo("format_changed"));

	ADD_GROUP("Feed", "feed_");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "feed_is_active"), "set_active", "is_active");
	ADD_PROPERTY(PropertyInfo(Variant::TRANSFORM2D, "feed_transform"), "set_transform", "get_transform");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "formats"), "", "get_formats");

	BIND_ENUM_CONSTANT(FEED_NOIMAGE);
	BIND_ENUM_CONSTANT(FEED_RGB);
	BIND_ENUM_CONSTANT(FEED_YCBCR);
	BIND_ENUM_CONSTANT(FEED_YCBCR_SEP);
	BIND_ENUM_CONSTANT(FEED_EXTERNAL);

	BIND_ENUM_CONSTANT(FEED_UNSPECIFIED);
	BIND_ENUM_CONSTANT(FEED_FRONT);
	BIND_ENUM_CONSTANT(FEED_BACK);
}

int CameraFeed::get_id() const {
	return id;
}

bool CameraFeed::is_active() const {
	return active;
}

void CameraFeed::set_active(bool p_is_active) {
	if (p_is_active == active) {
		// all good
	} else if (p_is_active) {
		// attempt to activate this feed
		if (activate_feed()) {
			active = true;
		}
	} else {
		// just deactivate it
		deactivate_feed();
		active = false;
	}
}

String CameraFeed::get_name() const {
	return name;
}

void CameraFeed::set_name(String p_name) {
	name = p_name;
}

int CameraFeed::get_base_width() const {
	return base_width;
}

int CameraFeed::get_base_height() const {
	return base_height;
}

CameraFeed::FeedDataType CameraFeed::get_datatype() const {
	return datatype;
}

CameraFeed::FeedPosition CameraFeed::get_position() const {
	return position;
}

void CameraFeed::set_position(CameraFeed::FeedPosition p_position) {
	position = p_position;
}

Transform2D CameraFeed::get_transform() const {
	return transform;
}

void CameraFeed::set_transform(const Transform2D &p_transform) {
	transform = p_transform;
}

RID CameraFeed::get_texture(CameraSystem::FeedImage p_which) {
	return texture[p_which];
}

uint64_t CameraFeed::get_texture_tex_id(CameraSystem::FeedImage p_which) {
	return RenderingSystem::get_singleton()->texture_get_native_handle(texture[p_which]);
}

CameraFeed::CameraFeed() {
	// initialize our feed
	id = CameraSystem::get_singleton()->get_free_id();
	base_width = 0;
	base_height = 0;
	name = "???";
	active = false;
	datatype = CameraFeed::FEED_RGB;
	position = CameraFeed::FEED_UNSPECIFIED;
	transform = Transform2D(1.0, 0.0, 0.0, -1.0, 0.0, 1.0);
	texture[CameraSystem::FEED_Y_IMAGE] = RenderingSystem::get_singleton()->texture_2d_placeholder_create();
	texture[CameraSystem::FEED_CBCR_IMAGE] = RenderingSystem::get_singleton()->texture_2d_placeholder_create();
}

CameraFeed::CameraFeed(String p_name, FeedPosition p_position) {
	// initialize our feed
	id = CameraSystem::get_singleton()->get_free_id();
	base_width = 0;
	base_height = 0;
	name = p_name;
	active = false;
	datatype = CameraFeed::FEED_NOIMAGE;
	position = p_position;
	transform = Transform2D(1.0, 0.0, 0.0, -1.0, 0.0, 1.0);
	texture[CameraSystem::FEED_Y_IMAGE] = RenderingSystem::get_singleton()->texture_2d_placeholder_create();
	texture[CameraSystem::FEED_CBCR_IMAGE] = RenderingSystem::get_singleton()->texture_2d_placeholder_create();
}

CameraFeed::~CameraFeed() {
	// Free our textures
	ERR_FAIL_NULL(RenderingSystem::get_singleton());
	RenderingSystem::get_singleton()->free_rid(texture[CameraSystem::FEED_Y_IMAGE]);
	RenderingSystem::get_singleton()->free_rid(texture[CameraSystem::FEED_CBCR_IMAGE]);
}

void CameraFeed::set_rgb_image(const Ref<Image> &p_rgb_img) {
	ERR_FAIL_COND(p_rgb_img.is_null());
	if (active) {
		int new_width = p_rgb_img->get_width();
		int new_height = p_rgb_img->get_height();

		if (datatype != CameraFeed::FEED_RGB || (base_width != new_width) || (base_height != new_height)) {
			base_width = new_width;
			base_height = new_height;

			RID new_texture = RenderingSystem::get_singleton()->texture_2d_create(p_rgb_img);
			RenderingSystem::get_singleton()->texture_replace(texture[CameraSystem::FEED_RGBA_IMAGE], new_texture);

			// `format_changed` signal is deferred to ensure:
			// - They are emitted on Orblit's main thread.
			// - Both datatype and frame size are updated before the emission.
			call_deferred("emit_signal", format_changed_signal_name);
		} else {
			RenderingSystem::get_singleton()->texture_2d_update(texture[CameraSystem::FEED_RGBA_IMAGE], p_rgb_img);
		}

		datatype = CameraFeed::FEED_RGB;
		// Most of the time the pixel data of camera devices comes from threads outside Orblit.
		// Defer `frame_changed` signals to ensure they are emitted on Orblit's main thread.
		call_deferred("emit_signal", frame_changed_signal_name);
	}
}

void CameraFeed::set_ycbcr_image(const Ref<Image> &p_ycbcr_img) {
	ERR_FAIL_COND(p_ycbcr_img.is_null());
	if (active) {
		int new_width = p_ycbcr_img->get_width();
		int new_height = p_ycbcr_img->get_height();

		if (datatype != CameraFeed::FEED_YCBCR || (base_width != new_width) || (base_height != new_height)) {
			base_width = new_width;
			base_height = new_height;

			RID new_texture = RenderingSystem::get_singleton()->texture_2d_create(p_ycbcr_img);
			RenderingSystem::get_singleton()->texture_replace(texture[CameraSystem::FEED_YCBCR_IMAGE], new_texture);

			// `format_changed` signal is deferred to ensure:
			// - They are emitted on Orblit's main thread.
			// - Both datatype and frame size are updated before the emission.
			call_deferred("emit_signal", format_changed_signal_name);
		} else {
			RenderingSystem::get_singleton()->texture_2d_update(texture[CameraSystem::FEED_YCBCR_IMAGE], p_ycbcr_img);
		}

		datatype = CameraFeed::FEED_YCBCR;
		// Most of the time the pixel data of camera devices comes from threads outside Orblit.
		// Defer `frame_changed` signals to ensure they are emitted on Orblit's main thread.
		call_deferred("emit_signal", frame_changed_signal_name);
	}
}

void CameraFeed::set_ycbcr_images(const Ref<Image> &p_y_img, const Ref<Image> &p_cbcr_img) {
	ERR_FAIL_COND(p_y_img.is_null());
	ERR_FAIL_COND(p_cbcr_img.is_null());
	if (active) {
		///@TODO investigate whether we can use thirdparty/misc/yuv2rgb.h here to convert our YUV data to RGB, our shader approach is potentially faster though..
		// Wondering about including that into multiple projects, may cause issues.
		// That said, if we convert to RGB, we could enable using texture resources again...

		int new_y_width = p_y_img->get_width();
		int new_y_height = p_y_img->get_height();

		if (datatype != CameraFeed::FEED_YCBCR_SEP || (base_width != new_y_width) || (base_height != new_y_height)) {
			base_width = new_y_width;
			base_height = new_y_height;
			{
				RID new_texture = RenderingSystem::get_singleton()->texture_2d_create(p_y_img);
				RenderingSystem::get_singleton()->texture_replace(texture[CameraSystem::FEED_Y_IMAGE], new_texture);
			}
			{
				RID new_texture = RenderingSystem::get_singleton()->texture_2d_create(p_cbcr_img);
				RenderingSystem::get_singleton()->texture_replace(texture[CameraSystem::FEED_CBCR_IMAGE], new_texture);
			}

			// `format_changed` signal is deferred to ensure:
			// - They are emitted on Orblit's main thread.
			// - Both datatype and frame size are updated before the emission.
			call_deferred("emit_signal", format_changed_signal_name);
		} else {
			RenderingSystem::get_singleton()->texture_2d_update(texture[CameraSystem::FEED_Y_IMAGE], p_y_img);
			RenderingSystem::get_singleton()->texture_2d_update(texture[CameraSystem::FEED_CBCR_IMAGE], p_cbcr_img);
		}

		datatype = CameraFeed::FEED_YCBCR_SEP;
		// Most of the time the pixel data of camera devices comes from threads outside Orblit.
		// Defer `frame_changed` signals to ensure they are emitted on Orblit's main thread.
		call_deferred("emit_signal", frame_changed_signal_name);
	}
}

void CameraFeed::set_external(int p_width, int p_height) {
	if (datatype != CameraFeed::FEED_EXTERNAL || (base_width != p_width) || (base_height != p_height)) {
		base_width = p_width;
		base_height = p_height;

		RID new_texture = RenderingSystem::get_singleton()->texture_external_create(p_width, p_height, 0);
		RenderingSystem::get_singleton()->texture_replace(texture[CameraSystem::FEED_YCBCR_IMAGE], new_texture);

		// `format_changed` signal is deferred to ensure:
		// - They are emitted on Orblit's main thread.
		// - Both datatype and frame size are updated before the emission.
		call_deferred("emit_signal", format_changed_signal_name);
	}

	datatype = CameraFeed::FEED_EXTERNAL;
	// Most of the time the pixel data of camera devices comes from threads outside Orblit.
	// Defer `frame_changed` signals to ensure they are emitted on Orblit's main thread.
	call_deferred("emit_signal", frame_changed_signal_name);
}

bool CameraFeed::activate_feed() {
	bool ret = true;
	ORBLIT_VIRTUAL_CALL(_activate_feed, ret);
	return ret;
}

void CameraFeed::deactivate_feed() {
	ORBLIT_VIRTUAL_CALL(_deactivate_feed);
}

bool CameraFeed::set_format(int p_index, const Dictionary &p_parameters) {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_set_format, p_index, p_parameters, ret);
	return ret;
}

Array CameraFeed::get_formats() const {
	Array ret;
	ORBLIT_VIRTUAL_CALL(_get_formats, ret);
	return ret;
}

CameraFeed::FeedFormat CameraFeed::get_format() const {
	FeedFormat feed_format = {};
	return feed_format;
}
