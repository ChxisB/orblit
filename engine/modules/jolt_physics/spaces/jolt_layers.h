/**************************************************************************/
/*  jolt_layers.h                                                         */

#pragma once

#include "core/containers/hash_map.h"
#include "core/containers/local_vector.h"

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayer.h>
#include <Jolt/Physics/Collision/ObjectLayer.h>

class JoltLayers final
		: public JPH::BroadPhaseLayerInterface,
		  public JPH::ObjectLayerPairFilter,
		  public JPH::ObjectVsBroadPhaseLayerFilter {
	LocalVector<uint64_t> collisions_by_layer;
	HashMap<uint64_t, JPH::ObjectLayer> layers_by_collision;
	JPH::ObjectLayer next_object_layer = 0;

	virtual uint32_t GetNumBroadPhaseLayers() const override;
	virtual JPH::BroadPhaseLayer GetBroadPhaseLayer(JPH::ObjectLayer p_layer) const override;

#if defined(JPH_EXTERNAL_PROFILE) || defined(JPH_PROFILE_ENABLED)
	virtual const char *GetBroadPhaseLayerName(JPH::BroadPhaseLayer p_layer) const override;
#endif

	virtual bool ShouldCollide(JPH::ObjectLayer p_encoded_layer1, JPH::ObjectLayer p_encoded_layer2) const override;
	virtual bool ShouldCollide(JPH::ObjectLayer p_encoded_layer1, JPH::BroadPhaseLayer p_broad_phase_layer2) const override;

	JPH::ObjectLayer _allocate_object_layer(uint64_t p_collision);

public:
	JoltLayers();
	virtual ~JoltLayers();

	JPH::ObjectLayer to_object_layer(JPH::BroadPhaseLayer p_broad_phase_layer, uint32_t p_collision_layer, uint32_t p_collision_mask);
	void from_object_layer(JPH::ObjectLayer p_encoded_layer, JPH::BroadPhaseLayer &r_broad_phase_layer, uint32_t &r_collision_layer, uint32_t &r_collision_mask) const;
};
