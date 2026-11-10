/**************************************************************************/
/*  camera_feed.h                                                         */

#pragma once

#include "core/io/image.h"
#include "core/math/transform_planar.h"
#include "systems/camera/camera_system.h"

/**
	The camera server is a singleton object that gives access to the various
	camera feeds that can be used as the background for our environment.
**/

class CameraFeed : public RefCounted {
	ORBLIT_CLASS(CameraFeed, RefCounted);

public:
	enum FeedDataType {
		FEED_NOIMAGE, // we don't have an image yet
		FEED_RGB, // our texture will contain a normal RGB texture that can be used directly
		FEED_YCBCR, // our texture will contain a YCbCr texture that needs to be converted to RGB before output
		FEED_YCBCR_SEP, // our camera is split into two textures, first plane contains Y data, second plane contains CbCr data
		FEED_EXTERNAL, // specific for android atm, camera feed is managed externally, assumed RGB for now
	};

	enum FeedPosition {
		FEED_UNSPECIFIED, // we have no idea
		FEED_FRONT, // this is a camera on the front of the device
		FEED_BACK // this is a camera on the back of the device
	};

private:
	int id; // unique id for this, for internal use in case feeds are removed
	const StringName format_changed_signal_name = "format_changed";
	const StringName frame_changed_signal_name = "frame_changed";

protected:
	struct FeedFormat {
		int width = 0;
		int height = 0;
		String format;
		int frame_numerator = 0;
		int frame_denominator = 0;
		uint32_t pixel_format = 0;
	};

	String name; // name of our camera feed
	FeedDataType datatype; // type of texture data stored
	FeedPosition position; // position of camera on the device
	Transform2D transform; // display transform
	int base_width = 0;
	int base_height = 0;
	Vector<FeedFormat> formats;
	Dictionary parameters;
	int selected_format = -1;

	bool active; // only when active do we actually update the camera texture each frame
	RID texture[CameraSystem::FEED_IMAGES]; // texture images needed for this

	static void register_members();

public:
	int get_id() const;
	bool is_active() const;
	void set_active(bool p_is_active);

	String get_name() const;
	void set_name(String p_name);

	int get_base_width() const;
	int get_base_height() const;

	FeedPosition get_position() const;
	void set_position(FeedPosition p_position);

	Transform2D get_transform() const;
	void set_transform(const Transform2D &p_transform);

	RID get_texture(CameraSystem::FeedImage p_which);
	uint64_t get_texture_tex_id(CameraSystem::FeedImage p_which);

	CameraFeed();
	CameraFeed(String p_name, FeedPosition p_position = CameraFeed::FEED_UNSPECIFIED);
	virtual ~CameraFeed();

	FeedDataType get_datatype() const;
	void set_rgb_image(const Ref<Image> &p_rgb_img);
	void set_ycbcr_image(const Ref<Image> &p_ycbcr_img);
	void set_ycbcr_images(const Ref<Image> &p_y_img, const Ref<Image> &p_cbcr_img);
	void set_external(int p_width, int p_height);

	virtual bool set_format(int p_index, const Dictionary &p_parameters);
	virtual Array get_formats() const;
	virtual FeedFormat get_format() const;

	virtual bool activate_feed();
	virtual void deactivate_feed();

	ORBLIT_VIRTUAL2R(bool, _set_format, int, const Dictionary &);
	ORBLIT_VIRTUAL0RC(Array, _get_formats);
	ORBLIT_VIRTUAL0R(bool, _activate_feed)
	ORBLIT_VIRTUAL0(_deactivate_feed)
};

VARIANT_ENUM_CAST(CameraFeed::FeedDataType);
VARIANT_ENUM_CAST(CameraFeed::FeedPosition);
