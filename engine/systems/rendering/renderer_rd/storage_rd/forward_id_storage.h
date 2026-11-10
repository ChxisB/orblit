/**************************************************************************/
/*  forward_id_storage.h                                                  */

#pragma once

#include <cstdint>

class RendererSceneRenderRD;

namespace RendererRD {

typedef int32_t ForwardID;

enum ForwardIDType {
	FORWARD_ID_TYPE_OMNI_LIGHT,
	FORWARD_ID_TYPE_SPOT_LIGHT,
	FORWARD_ID_TYPE_REFLECTION_PROBE,
	FORWARD_ID_TYPE_DECAL,
	FORWARD_ID_TYPE_AREA_LIGHT,
	FORWARD_ID_MAX,
};

class ForwardIDStorage {
private:
	static inline ForwardIDStorage *singleton = nullptr;

public:
	static ForwardIDStorage *get_singleton() { return singleton; }

	ForwardIDStorage() { singleton = this; }
	virtual ~ForwardIDStorage() { singleton = nullptr; }

	virtual RendererRD::ForwardID allocate_forward_id(RendererRD::ForwardIDType p_type) { return -1; }
	virtual void free_forward_id(RendererRD::ForwardIDType p_type, RendererRD::ForwardID p_id) {}
	virtual void map_forward_id(RendererRD::ForwardIDType p_type, RendererRD::ForwardID p_id, uint32_t p_index, uint64_t p_last_pass) {}
	virtual bool uses_forward_ids() const { return false; }
};

} // namespace RendererRD
