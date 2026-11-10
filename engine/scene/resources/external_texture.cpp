/**************************************************************************/
/*  external_texture.cpp                                                  */

#include "external_texture.h"

#include "core/object/class_registry.h"
#include "systems/rendering/rendering_system.h"

void ExternalTexture::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_size", "size"), &ExternalTexture::set_size);
	ClassRegistry::bind_method(D_METHOD("get_external_texture_id"), &ExternalTexture::get_external_texture_id);
	ClassRegistry::bind_method(D_METHOD("set_external_buffer_id", "external_buffer_id"), &ExternalTexture::set_external_buffer_id);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "size"), "set_size", "get_size");
}

uint64_t ExternalTexture::get_external_texture_id() const {
	_ensure_created();
	return RenderingSystem::get_singleton()->texture_get_native_handle(texture);
}

void ExternalTexture::set_size(const Size2 &p_size) {
	if (p_size.width > 0 && p_size.height > 0 && p_size != size) {
		size = p_size;
		_ensure_created();
		RenderingSystem::get_singleton()->texture_external_update(texture, size.width, size.height, external_buffer);
		emit_changed();
	}
}

Size2 ExternalTexture::get_size() const {
	return size;
}

void ExternalTexture::set_external_buffer_id(uint64_t p_external_buffer) {
	if (p_external_buffer != external_buffer) {
		external_buffer = p_external_buffer;
		_ensure_created();
		RenderingSystem::get_singleton()->texture_external_update(texture, size.width, size.height, external_buffer);
	}
}

int ExternalTexture::get_width() const {
	return size.width;
}

int ExternalTexture::get_height() const {
	return size.height;
}

bool ExternalTexture::has_alpha() const {
	return false;
}

RID ExternalTexture::get_rid() const {
	if (!texture.is_valid()) {
		texture = RenderingSystem::get_singleton()->texture_2d_placeholder_create();
		using_placeholder = true;
	}
	return texture;
}

void ExternalTexture::_ensure_created() const {
	if (texture.is_valid() && !using_placeholder) {
		return;
	}

	RID new_texture = RenderingSystem::get_singleton()->texture_external_create(size.width, size.height);
	if (using_placeholder) {
		DEV_ASSERT(texture.is_valid());
		RenderingSystem::get_singleton()->texture_replace(texture, new_texture);
		using_placeholder = false;
	} else {
		texture = new_texture;
	}
}

ExternalTexture::ExternalTexture() {
}

ExternalTexture::~ExternalTexture() {
	if (texture.is_valid()) {
		ERR_FAIL_NULL(RenderingSystem::get_singleton());
		RenderingSystem::get_singleton()->free_rid(texture);
	}
}
