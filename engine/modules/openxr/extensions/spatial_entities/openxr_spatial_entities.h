/**************************************************************************/
/*  openxr_spatial_entities.h                                             */

#pragma once

#include "../../openxr_structure.h"

#include "scene/resources/mesh.h"
#include "systems/xr/xr_positional_tracker.h"

#include <openxr/openxr.h>

// Wrapper class for XrSpatialCapabilityConfigurationBaseHeaderEXT
class OpenXRSpatialCapabilityConfigurationBaseHeader : public RefCounted {
	ORBLIT_CLASS(OpenXRSpatialCapabilityConfigurationBaseHeader, RefCounted);

protected:
	static void register_members();

public:
	virtual bool has_valid_configuration() const;
	uint64_t _get_configurationgd();
	virtual XrSpatialCapabilityConfigurationBaseHeaderEXT *get_configuration();

	ORBLIT_VIRTUAL0RC(bool, _has_valid_configuration);
	ORBLIT_VIRTUAL0R(uint64_t, _get_configuration);
};

// Tracker for our spatial entities
class OpenXRSpatialEntityTracker : public XRPositionalTracker {
	ORBLIT_CLASS(OpenXRSpatialEntityTracker, XRPositionalTracker);

public:
	enum EntityTrackingState {
		ENTITY_TRACKING_STATE_STOPPED = XR_SPATIAL_ENTITY_TRACKING_STATE_STOPPED_EXT,
		ENTITY_TRACKING_STATE_PAUSED = XR_SPATIAL_ENTITY_TRACKING_STATE_PAUSED_EXT,
		ENTITY_TRACKING_STATE_TRACKING = XR_SPATIAL_ENTITY_TRACKING_STATE_TRACKING_EXT,
	};

	OpenXRSpatialEntityTracker();
	virtual ~OpenXRSpatialEntityTracker();

	void set_spatial_context(const RID &p_spatial_context);
	RID get_spatial_context() const;

	void set_entity(const RID &p_entity);
	RID get_entity() const;

	void set_spatial_tracking_state(const XrSpatialEntityTrackingStateEXT p_state);
	XrSpatialEntityTrackingStateEXT get_spatial_tracking_state() const;

	void add_next(Ref<OpenXRStructureBase> p_next);
	void remove_next(Ref<OpenXRStructureBase> p_next);
	Ref<OpenXRStructureBase> get_next() const;

protected:
	static void register_members();

private:
	RID spatial_context;
	RID spatial_entity;
	XrSpatialEntityTrackingStateEXT spatial_tracking_state = XR_SPATIAL_ENTITY_TRACKING_STATE_PAUSED_EXT;
	Ref<OpenXRStructureBase> next;

	void _set_spatial_tracking_state(const EntityTrackingState p_state);
	EntityTrackingState _get_spatial_tracking_state() const;
};

VARIANT_ENUM_CAST(OpenXRSpatialEntityTracker::EntityTrackingState)

// Wrapper class for our spatial component data returned by discovery queries
class OpenXRSpatialComponentData : public RefCounted {
	ORBLIT_CLASS(OpenXRSpatialComponentData, RefCounted);

protected:
	static void register_members();

public:
	virtual void set_capacity(uint32_t p_capacity);
	int64_t _get_component_typegd() const;
	virtual XrSpatialComponentTypeEXT get_component_type() const;
	virtual void *get_structure_data(void *p_next);

	ORBLIT_VIRTUAL1(_set_capacity, uint32_t);
	ORBLIT_VIRTUAL0RC(uint64_t, _get_component_type);
	ORBLIT_VIRTUAL1R(uint64_t, _get_structure_data, uint64_t);
};

class OpenXRSpatialComponentBounded2DList : public OpenXRSpatialComponentData {
	ORBLIT_CLASS(OpenXRSpatialComponentBounded2DList, OpenXRSpatialComponentData);

protected:
	static void register_members();

public:
	virtual void set_capacity(uint32_t p_capacity) override;
	virtual XrSpatialComponentTypeEXT get_component_type() const override;
	virtual void *get_structure_data(void *p_next) override;

	Transform3D get_center_pose(int64_t p_index) const;
	Vector2 get_size(int64_t p_index) const;

private:
	Vector<XrSpatialBounded2DDataEXT> bounded2d_data;

	XrSpatialComponentBounded2DListEXT bounded2d_list = { XR_TYPE_SPATIAL_COMPONENT_BOUNDED_2D_LIST_EXT, nullptr, 0, nullptr };
};

class OpenXRSpatialComponentBounded3DList : public OpenXRSpatialComponentData {
	ORBLIT_CLASS(OpenXRSpatialComponentBounded3DList, OpenXRSpatialComponentData);

protected:
	static void register_members();

public:
	virtual void set_capacity(uint32_t p_capacity) override;
	virtual XrSpatialComponentTypeEXT get_component_type() const override;
	virtual void *get_structure_data(void *p_next) override;

	Transform3D get_center_pose(int64_t p_index) const;
	Vector3 get_size(int64_t p_index) const;

private:
	Vector<XrBoxf> bounded3d_data;

	XrSpatialComponentBounded3DListEXT bounded3d_list = { XR_TYPE_SPATIAL_COMPONENT_BOUNDED_3D_LIST_EXT, nullptr, 0, nullptr };
};

class OpenXRSpatialComponentParentList : public OpenXRSpatialComponentData {
	ORBLIT_CLASS(OpenXRSpatialComponentParentList, OpenXRSpatialComponentData);

protected:
	static void register_members();

public:
	virtual void set_capacity(uint32_t p_capacity) override;
	virtual XrSpatialComponentTypeEXT get_component_type() const override;
	virtual void *get_structure_data(void *p_next) override;

	RID get_parent(int64_t p_index) const;

private:
	Vector<XrSpatialEntityIdEXT> parent_data;

	XrSpatialComponentParentListEXT parent_list = { XR_TYPE_SPATIAL_COMPONENT_PARENT_LIST_EXT, nullptr, 0, nullptr };
};

class OpenXRSpatialComponentMesh2DList : public OpenXRSpatialComponentData {
	ORBLIT_CLASS(OpenXRSpatialComponentMesh2DList, OpenXRSpatialComponentData);

protected:
	static void register_members();

public:
	virtual void set_capacity(uint32_t p_capacity) override;
	virtual XrSpatialComponentTypeEXT get_component_type() const override;
	virtual void *get_structure_data(void *p_next) override;

	Transform3D get_transform(int64_t p_index) const;
	XrSpatialBufferIdEXT get_vertex_buffer_id(int64_t p_index) const;
	PackedVector2Array get_vertices(RID p_snapshot, int64_t p_index) const;
	XrSpatialBufferIdEXT get_index_buffer_id(int64_t p_index) const;
	PackedInt32Array get_indices(RID p_snapshot, int64_t p_index) const;

private:
	Vector<XrSpatialMeshDataEXT> mesh2d_data;

	XrSpatialComponentMesh2DListEXT mesh2d_list = { XR_TYPE_SPATIAL_COMPONENT_MESH_2D_LIST_EXT, nullptr, 0, nullptr };
};

class OpenXRSpatialComponentMesh3DList : public OpenXRSpatialComponentData {
	ORBLIT_CLASS(OpenXRSpatialComponentMesh3DList, OpenXRSpatialComponentData);

protected:
	static void register_members();

public:
	virtual void set_capacity(uint32_t p_capacity) override;
	virtual XrSpatialComponentTypeEXT get_component_type() const override;
	virtual void *get_structure_data(void *p_next) override;

	Transform3D get_transform(int64_t p_index) const;
	Ref<Mesh> get_mesh(int64_t p_index) const;

private:
	Vector<XrSpatialMeshDataEXT> mesh3d_data;

	XrSpatialComponentMesh3DListEXT mesh3d_list = { XR_TYPE_SPATIAL_COMPONENT_MESH_3D_LIST_EXT, nullptr, 0, nullptr };
};

class OpenXRSpatialQueryResultData : public OpenXRSpatialComponentData {
	ORBLIT_CLASS(OpenXRSpatialQueryResultData, OpenXRSpatialComponentData);

protected:
	static void register_members();

public:
	virtual void set_capacity(uint32_t p_capacity) override;
	virtual XrSpatialComponentTypeEXT get_component_type() const override;
	virtual void *get_structure_data(void *p_next) override;

	int64_t get_capacity() const { return entity_ids.size(); }
	XrSpatialEntityIdEXT get_entity_id(int64_t p_index) const;
	XrSpatialEntityTrackingStateEXT get_entity_state(int64_t p_index) const;

	static String get_entity_tracking_state_name(XrSpatialEntityTrackingStateEXT p_tracking_state);

private:
	Vector<XrSpatialEntityIdEXT> entity_ids;
	Vector<XrSpatialEntityTrackingStateEXT> entity_states;

	XrSpatialComponentDataQueryResultEXT query_result = { XR_TYPE_SPATIAL_COMPONENT_DATA_QUERY_RESULT_EXT, nullptr, 0, 0, nullptr, 0, 0, nullptr };

	uint64_t _get_entity_id(int64_t p_index) const;
	OpenXRSpatialEntityTracker::EntityTrackingState _get_entity_state(int64_t p_index) const;
};
