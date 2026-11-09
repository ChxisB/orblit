/**************************************************************************/
/*  rendering_system_globals.h                                            */

#pragma once

#include "systems/rendering/environment/renderer_fog.h"
#include "systems/rendering/environment/renderer_gi.h"
#include "systems/rendering/storage/camera_attributes_storage.h"
#include "systems/rendering/storage/light_storage.h"
#include "systems/rendering/storage/material_storage.h"
#include "systems/rendering/storage/mesh_storage.h"
#include "systems/rendering/storage/particles_storage.h"
#include "systems/rendering/storage/texture_storage.h"
#include "systems/rendering/storage/utilities.h"

class RendererCompositor;
class RendererCanvasCull;
class RendererCanvasRender;
class RendererViewport;
class RenderingMethod;

class RenderingSystemGlobals {
public:
	static inline bool threaded = false;

	static inline RendererUtilities *utilities = nullptr;
	static inline RendererLightStorage *light_storage = nullptr;
	static inline RendererMaterialStorage *material_storage = nullptr;
	static inline RendererMeshStorage *mesh_storage = nullptr;
	static inline RendererParticlesStorage *particles_storage = nullptr;
	static inline RendererTextureStorage *texture_storage = nullptr;
	static inline RendererGI *gi = nullptr;
	static inline RendererFog *fog = nullptr;
	static inline RendererCameraAttributes *camera_attributes = nullptr;
	static inline RendererCanvasRender *canvas_render = nullptr;
	static inline RendererCompositor *rasterizer = nullptr;

	static inline RendererCanvasCull *canvas = nullptr;
	static inline RendererViewport *viewport = nullptr;
	static inline RenderingMethod *scene = nullptr;
};

#define RSG RenderingSystemGlobals
