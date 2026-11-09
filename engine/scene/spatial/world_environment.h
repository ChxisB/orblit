/**************************************************************************/
/*  world_environment.h                                                   */

#pragma once

#include "scene/stage/entity.h"
#include "scene/resources/camera_attributes.h"
#include "scene/resources/compositor.h"
#include "scene/resources/environment.h"

class WorldEnvironment : public Entity {
	ORBLIT_CLASS(WorldEnvironment, Entity);

	Ref<Environment> environment;
	Ref<CameraAttributes> camera_attributes;
	Ref<Compositor> compositor;

	void _update_current_environment();
	void _update_current_camera_attributes();
	void _update_current_compositor();

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void set_environment(const Ref<Environment> &p_environment);
	Ref<Environment> get_environment() const;

	void set_camera_attributes(const Ref<CameraAttributes> &p_camera_attributes);
	Ref<CameraAttributes> get_camera_attributes() const;

	void set_compositor(const Ref<Compositor> &p_compositor);
	Ref<Compositor> get_compositor() const;

	PackedStringArray get_configuration_warnings() const override;

	WorldEnvironment();
};
