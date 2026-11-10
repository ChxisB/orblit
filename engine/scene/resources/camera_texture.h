/**************************************************************************/
/*  camera_texture.h                                                      */

#pragma once

#include "scene/resources/texture.h"
#include "systems/camera/camera_system.h"

class CameraTexture : public Texture2D {
	ORBLIT_CLASS(CameraTexture, Texture2D);

private:
	mutable RID _texture;
	int camera_feed_id = 0;
	CameraSystem::FeedImage which_feed = CameraSystem::FEED_RGBA_IMAGE;

protected:
	static void register_members();
	void _on_format_changed();

public:
	virtual int get_width() const override;
	virtual int get_height() const override;
	virtual RID get_rid() const override;
	virtual bool has_alpha() const override;

	virtual Ref<Image> get_image() const override;

	void set_camera_feed_id(int p_new_id);
	int get_camera_feed_id() const;

	void set_which_feed(CameraSystem::FeedImage p_which);
	CameraSystem::FeedImage get_which_feed() const;

	void set_camera_active(bool p_active);
	bool get_camera_active() const;

	CameraTexture();
	~CameraTexture();
};
