/**************************************************************************/
/*  modifier_bone_target_spatial.h                                             */

#pragma once

#include "scene/spatial/skeleton_modifier_spatial.h"

class ModifierBoneTarget3D : public SkeletonModifier3D {
	ORBLIT_CLASS(ModifierBoneTarget3D, SkeletonModifier3D);

	String bone_name;
	int bone = -1;

protected:
	void _validate_property(PropertyInfo &p_property) const;
	virtual void _validate_bone_names() override;
	static void register_members();
	virtual void _process_modification(double p_delta) override;

public:
#ifdef TOOLS_ENABLED
	virtual bool is_processed_on_saving() const override { return true; }
#endif
	virtual PackedStringArray get_configuration_warnings() const override;

	void set_bone_name(const String &p_bone_name);
	String get_bone_name() const;
	void set_bone(int p_bone);
	int get_bone() const;

	ModifierBoneTarget3D();
};
