/**************************************************************************/
/*  visionos_simd_helpers.h                                               */

#pragma once

#ifdef VISIONOS_ENABLED

#include "core/math/projection.h"
#include "core/math/transform_spatial.h"
#include "systems/rendering/rendering_device.h"

#import <Metal/Metal.h>
#include <simd/simd.h>

namespace MTL {

_FORCE_INLINE_ static Transform3D simd_to_transform3D(const simd_float4x4 &matrix) {
	Transform3D transform(Vector3(matrix.columns[0].x, matrix.columns[0].y, matrix.columns[0].z),
			Vector3(matrix.columns[1].x, matrix.columns[1].y, matrix.columns[1].z),
			Vector3(matrix.columns[2].x, matrix.columns[2].y, matrix.columns[2].z),
			Vector3(matrix.columns[3].x, matrix.columns[3].y, matrix.columns[3].z));
	return transform;
}

_FORCE_INLINE_ static Projection simd_to_projection(const simd_float4x4 &matrix) {
	Projection projection(Vector4(matrix.columns[0].x, matrix.columns[0].y, matrix.columns[0].z, matrix.columns[0].w),
			Vector4(matrix.columns[1].x, matrix.columns[1].y, matrix.columns[1].z, matrix.columns[1].w),
			Vector4(matrix.columns[2].x, matrix.columns[2].y, matrix.columns[2].z, matrix.columns[2].w),
			Vector4(matrix.columns[3].x, matrix.columns[3].y, matrix.columns[3].z, matrix.columns[3].w));
	return projection;
}

_FORCE_INLINE_ static Rect2i rect_from_mtl_viewport(MTLViewport viewport) {
	return Rect2i(viewport.originX, viewport.originY, viewport.width, viewport.height);
}

_FORCE_INLINE_ static RenderingDevice::TextureType texture_type_from_metal(MTLTextureType p_type) {
	switch (p_type) {
		case MTLTextureType1D:
			return RenderingDevice::TEXTURE_TYPE_1D;
		case MTLTextureType2D:
			return RenderingDevice::TEXTURE_TYPE_2D;
		case MTLTextureType3D:
			return RenderingDevice::TEXTURE_TYPE_3D;
		case MTLTextureTypeCube:
			return RenderingDevice::TEXTURE_TYPE_CUBE;
		case MTLTextureType1DArray:
			return RenderingDevice::TEXTURE_TYPE_1D_ARRAY;
		case MTLTextureType2DArray:
			return RenderingDevice::TEXTURE_TYPE_2D_ARRAY;
		case MTLTextureTypeCubeArray:
			return RenderingDevice::TEXTURE_TYPE_CUBE_ARRAY;
		default:
			return RenderingDevice::TEXTURE_TYPE_MAX; // Fallback for unknown types
	}
}

_FORCE_INLINE_ static RenderingDevice::TextureSamples texture_samples_from_metal(int p_sample_count) {
	switch (p_sample_count) {
		case 1:
			return RenderingDevice::TEXTURE_SAMPLES_1;
		case 2:
			return RenderingDevice::TEXTURE_SAMPLES_2;
		case 4:
			return RenderingDevice::TEXTURE_SAMPLES_4;
		case 8:
			return RenderingDevice::TEXTURE_SAMPLES_8;
		case 16:
			return RenderingDevice::TEXTURE_SAMPLES_16;
		case 32:
			return RenderingDevice::TEXTURE_SAMPLES_32;
		case 64:
			return RenderingDevice::TEXTURE_SAMPLES_64;
		default:
			return RenderingDevice::TEXTURE_SAMPLES_MAX;
	}
}

} //namespace MTL

#endif // VISIONOS_ENABLED
