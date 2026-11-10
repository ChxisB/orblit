/**************************************************************************/
/*  navigation_constants_spatial.h                                             */

#pragma once

namespace NavigationEnums3D {

enum PathfindingAlgorithm {
	PATHFINDING_ALGORITHM_ASTAR = 0,
};

enum PathPostProcessing {
	PATH_POSTPROCESSING_CORRIDORFUNNEL = 0,
	PATH_POSTPROCESSING_EDGECENTERED,
	PATH_POSTPROCESSING_NONE,
};

enum PathSegmentType {
	PATH_SEGMENT_TYPE_REGION = 0,
	PATH_SEGMENT_TYPE_LINK
};

enum PathMetadataFlags {
	PATH_INCLUDE_NONE = 0,
	PATH_INCLUDE_TYPES = 1,
	PATH_INCLUDE_RIDS = 2,
	PATH_INCLUDE_OWNERS = 4,
	PATH_INCLUDE_ALL = PATH_INCLUDE_TYPES | PATH_INCLUDE_RIDS | PATH_INCLUDE_OWNERS
};

} //namespace NavigationEnums3D

namespace NavigationDefaults3D {

// Rasterization.

// To find the polygons edges the vertices are displaced in a grid where
// each cell has the following cell_size and cell_height.
constexpr float NAV_MESH_CELL_HEIGHT = 0.25f; // Must match ProjectSettings default 3D cell_height and NavigationMesh cell_height.
constexpr float NAV_MESH_CELL_SIZE = 0.25f; // Must match ProjectSettings default 3D cell_size and NavigationMesh cell_size.
constexpr float NAV_MESH_CELL_SIZE_MIN = 0.01f;
constexpr const char *const NAV_MESH_CELL_SIZE_HINT = "0.001,100,0.001,or_greater";

// Map.

constexpr float EDGE_CONNECTION_MARGIN = 0.25f;
constexpr float LINK_CONNECTION_RADIUS = 1.0f;
constexpr int path_search_max_polygons = 4096;

// Agent.

constexpr float AVOIDANCE_AGENT_HEIGHT = 1.0;
constexpr float AVOIDANCE_AGENT_RADIUS = 0.5;
constexpr float AVOIDANCE_AGENT_MAX_SPEED = 10.0;
constexpr float AVOIDANCE_AGENT_TIME_HORIZON_AGENTS = 1.0;
constexpr float AVOIDANCE_AGENT_TIME_HORIZON_OBSTACLES = 0.0;
constexpr int AVOIDANCE_AGENT_MAX_NEIGHBORS = 10;
constexpr float AVOIDANCE_AGENT_NEIGHBOR_DISTANCE = 50.0;

} //namespace NavigationDefaults3D
