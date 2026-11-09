/**************************************************************************/
/*  navigation_path_query_parameters_spatial.h                                 */

#pragma once

#include "core/object/ref_counted.h"
#include "core/variant/type_info.h"
#include "systems/navigation_spatial/navigation_constants_spatial.h"

class NavigationPathQueryParameters3D : public RefCounted {
	ORBLIT_CLASS(NavigationPathQueryParameters3D, RefCounted);

protected:
	static void register_members();

public:
	enum PathfindingAlgorithm {
		PATHFINDING_ALGORITHM_ASTAR = NavigationEnums3D::PATHFINDING_ALGORITHM_ASTAR,
	};

	enum PathPostProcessing {
		PATH_POSTPROCESSING_CORRIDORFUNNEL = NavigationEnums3D::PATH_POSTPROCESSING_CORRIDORFUNNEL,
		PATH_POSTPROCESSING_EDGECENTERED = NavigationEnums3D::PATH_POSTPROCESSING_EDGECENTERED,
		PATH_POSTPROCESSING_NONE = NavigationEnums3D::PATH_POSTPROCESSING_NONE,
	};

	enum PathMetadataFlags {
		PATH_METADATA_INCLUDE_NONE = NavigationEnums3D::PathMetadataFlags::PATH_INCLUDE_NONE,
		PATH_METADATA_INCLUDE_TYPES = NavigationEnums3D::PathMetadataFlags::PATH_INCLUDE_TYPES,
		PATH_METADATA_INCLUDE_RIDS = NavigationEnums3D::PathMetadataFlags::PATH_INCLUDE_RIDS,
		PATH_METADATA_INCLUDE_OWNERS = NavigationEnums3D::PathMetadataFlags::PATH_INCLUDE_OWNERS,
		PATH_METADATA_INCLUDE_ALL = NavigationEnums3D::PathMetadataFlags::PATH_INCLUDE_ALL
	};

private:
	PathfindingAlgorithm pathfinding_algorithm = PATHFINDING_ALGORITHM_ASTAR;
	PathPostProcessing path_postprocessing = PATH_POSTPROCESSING_CORRIDORFUNNEL;
	RID map;
	Vector3 start_position;
	Vector3 target_position;
	uint32_t navigation_layers = 1;
	BitField<PathMetadataFlags> metadata_flags = PATH_METADATA_INCLUDE_ALL;
	bool simplify_path = false;
	real_t simplify_epsilon = 0.0;

	LocalVector<RID> _excluded_regions;
	LocalVector<RID> _included_regions;

	float path_return_max_length = 0.0;
	float path_return_max_radius = 0.0;
	int path_search_max_polygons = NavigationDefaults3D::path_search_max_polygons;
	float path_search_max_distance = 0.0;

public:
	void set_pathfinding_algorithm(const PathfindingAlgorithm p_pathfinding_algorithm);
	PathfindingAlgorithm get_pathfinding_algorithm() const;

	void set_path_postprocessing(const PathPostProcessing p_path_postprocessing);
	PathPostProcessing get_path_postprocessing() const;

	void set_map(RID p_map);
	RID get_map() const;

	void set_start_position(Vector3 p_start_position);
	Vector3 get_start_position() const;

	void set_target_position(Vector3 p_target_position);
	Vector3 get_target_position() const;

	void set_navigation_layers(uint32_t p_navigation_layers);
	uint32_t get_navigation_layers() const;

	void set_metadata_flags(BitField<NavigationPathQueryParameters3D::PathMetadataFlags> p_flags);
	BitField<NavigationPathQueryParameters3D::PathMetadataFlags> get_metadata_flags() const;

	void set_simplify_path(bool p_enabled);
	bool get_simplify_path() const;

	void set_simplify_epsilon(real_t p_epsilon);
	real_t get_simplify_epsilon() const;

	void set_excluded_regions(const TypedArray<RID> &p_regions);
	TypedArray<RID> get_excluded_regions() const;

	void set_included_regions(const TypedArray<RID> &p_regions);
	TypedArray<RID> get_included_regions() const;

	void set_path_return_max_length(float p_length);
	float get_path_return_max_length() const;

	void set_path_return_max_radius(float p_radius);
	float get_path_return_max_radius() const;

	void set_path_search_max_polygons(int p_max_polygons);
	int get_path_search_max_polygons() const;

	void set_path_search_max_distance(float p_distance);
	float get_path_search_max_distance() const;
};

VARIANT_ENUM_CAST(NavigationPathQueryParameters3D::PathfindingAlgorithm);
VARIANT_ENUM_CAST(NavigationPathQueryParameters3D::PathPostProcessing);
VARIANT_BITFIELD_CAST(NavigationPathQueryParameters3D::PathMetadataFlags);
