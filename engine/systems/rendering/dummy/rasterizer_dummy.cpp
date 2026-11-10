/**************************************************************************/
/*  rasterizer_dummy.cpp                                                  */

#include "rasterizer_dummy.h"

#include "systems/display/display_system.h"
#include "systems/rendering/dummy/environment/fog.h"
#include "systems/rendering/dummy/environment/gi.h"
#include "systems/rendering/dummy/rasterizer_canvas_dummy.h"
#include "systems/rendering/dummy/rasterizer_scene_dummy.h"
#include "systems/rendering/dummy/storage/light_storage.h"
#include "systems/rendering/dummy/storage/material_storage.h"
#include "systems/rendering/dummy/storage/mesh_storage.h"
#include "systems/rendering/dummy/storage/particles_storage.h"
#include "systems/rendering/dummy/storage/texture_storage.h"
#include "systems/rendering/dummy/storage/utilities.h"

void RasterizerDummy::end_frame(bool p_present) {
	if (p_present) {
		DisplaySystem::get_singleton()->swap_buffers();
	}
}

RendererCanvasRender *RasterizerDummy::get_canvas() {
	return canvas;
}
RendererSceneRender *RasterizerDummy::get_scene() {
	return scene;
}

RendererFog *RasterizerDummy::get_fog() {
	return fog;
}
RendererGI *RasterizerDummy::get_gi() {
	return gi;
}
RendererLightStorage *RasterizerDummy::get_light_storage() {
	return light_storage;
}
RendererMaterialStorage *RasterizerDummy::get_material_storage() {
	return material_storage;
}
RendererMeshStorage *RasterizerDummy::get_mesh_storage() {
	return mesh_storage;
}
RendererParticlesStorage *RasterizerDummy::get_particles_storage() {
	return particles_storage;
}
RendererTextureStorage *RasterizerDummy::get_texture_storage() {
	return texture_storage;
}
RendererUtilities *RasterizerDummy::get_utilities() {
	return utilities;
}

RasterizerDummy::RasterizerDummy() {
	canvas = memnew(RasterizerCanvasDummy);
	scene = memnew(RasterizerSceneDummy);

	fog = memnew(RendererDummy::Fog);
	gi = memnew(RendererDummy::GI);
	light_storage = memnew(RendererDummy::LightStorage);
	material_storage = memnew(RendererDummy::MaterialStorage);
	mesh_storage = memnew(RendererDummy::MeshStorage);
	particles_storage = memnew(RendererDummy::ParticlesStorage);
	texture_storage = memnew(RendererDummy::TextureStorage);
	utilities = memnew(RendererDummy::Utilities);
}

RasterizerDummy::~RasterizerDummy() {
	memdelete(canvas);
	memdelete(scene);

	memdelete(fog);
	memdelete(gi);
	memdelete(light_storage);
	memdelete(material_storage);
	memdelete(mesh_storage);
	memdelete(particles_storage);
	memdelete(texture_storage);
	memdelete(utilities);
}
