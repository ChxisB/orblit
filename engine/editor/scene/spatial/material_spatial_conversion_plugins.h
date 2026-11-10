/**************************************************************************/
/*  material_spatial_conversion_plugins.h                                      */

#pragma once

#include "editor/plugins/editor_resource_conversion_plugin.h"

class StandardMaterial3DConversionPlugin : public EditorResourceConversionPlugin {
	ORBLIT_CLASS(StandardMaterial3DConversionPlugin, EditorResourceConversionPlugin);

public:
	virtual String converts_to() const override;
	virtual bool handles(const Ref<Resource> &p_resource) const override;
	virtual Ref<Resource> convert(const Ref<Resource> &p_resource) const override;
};

class ORMMaterial3DConversionPlugin : public EditorResourceConversionPlugin {
	ORBLIT_CLASS(ORMMaterial3DConversionPlugin, EditorResourceConversionPlugin);

public:
	virtual String converts_to() const override;
	virtual bool handles(const Ref<Resource> &p_resource) const override;
	virtual Ref<Resource> convert(const Ref<Resource> &p_resource) const override;
};

class ProceduralSkyMaterialConversionPlugin : public EditorResourceConversionPlugin {
	ORBLIT_CLASS(ProceduralSkyMaterialConversionPlugin, EditorResourceConversionPlugin);

public:
	virtual String converts_to() const override;
	virtual bool handles(const Ref<Resource> &p_resource) const override;
	virtual Ref<Resource> convert(const Ref<Resource> &p_resource) const override;
};

class PanoramaSkyMaterialConversionPlugin : public EditorResourceConversionPlugin {
	ORBLIT_CLASS(PanoramaSkyMaterialConversionPlugin, EditorResourceConversionPlugin);

public:
	virtual String converts_to() const override;
	virtual bool handles(const Ref<Resource> &p_resource) const override;
	virtual Ref<Resource> convert(const Ref<Resource> &p_resource) const override;
};

class PhysicalSkyMaterialConversionPlugin : public EditorResourceConversionPlugin {
	ORBLIT_CLASS(PhysicalSkyMaterialConversionPlugin, EditorResourceConversionPlugin);

public:
	virtual String converts_to() const override;
	virtual bool handles(const Ref<Resource> &p_resource) const override;
	virtual Ref<Resource> convert(const Ref<Resource> &p_resource) const override;
};

class FogMaterialConversionPlugin : public EditorResourceConversionPlugin {
	ORBLIT_CLASS(FogMaterialConversionPlugin, EditorResourceConversionPlugin);

public:
	virtual String converts_to() const override;
	virtual bool handles(const Ref<Resource> &p_resource) const override;
	virtual Ref<Resource> convert(const Ref<Resource> &p_resource) const override;
};
