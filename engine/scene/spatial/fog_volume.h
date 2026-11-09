/**************************************************************************/
/*  fog_volume.h                                                          */

#pragma once

#include "core/containers/rid.h"
#include "scene/resources/material.h"
#include "scene/spatial/visual_instance_spatial.h"

class FogVolume : public VisualInstance3D {
	ORBLIT_CLASS(FogVolume, VisualInstance3D);

	Vector3 size = Vector3(2, 2, 2);
	Ref<Material> material;
	RSE::FogVolumeShape shape = RSE::FOG_VOLUME_SHAPE_BOX;

	RID volume;

protected:
	_FORCE_INLINE_ RID _get_volume() { return volume; }
	static void register_members();
	void _validate_property(PropertyInfo &p_property) const;

public:
	void set_size(const Vector3 &p_size);
	Vector3 get_size() const;

	void set_shape(RSE::FogVolumeShape p_type);
	RSE::FogVolumeShape get_shape() const;

	void set_material(const Ref<Material> &p_material);
	Ref<Material> get_material() const;

	virtual AABB get_aabb() const override;
	PackedStringArray get_configuration_warnings() const override;

	FogVolume();
	~FogVolume();
};
