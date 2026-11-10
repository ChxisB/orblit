/**************************************************************************/
/*  d3d12_nir_bridge.h                                                    */

#pragma once

#ifndef __cplusplus
#include <stddef.h>
#include <stdint.h>
#else
#include <cstddef>
#include <cstdint>

extern "C" {
#endif

// This one leaves room for potentially extremely copious bindings in a set.
static const uint32_t ORBLIT_NIR_DESCRIPTOR_SET_MULTIPLIER = 100000000;
// This one leaves room for potentially big sized arrays.
static const uint32_t ORBLIT_NIR_BINDING_MULTIPLIER = 100000;

static const uint64_t ORBLIT_NIR_SC_SENTINEL_MAGIC = 0x45678900; // This must be as big as to be VBR-ed as a 32 bits number.
static const uint64_t ORBLIT_NIR_SC_SENTINEL_MAGIC_MASK = 0xffffffffffffff00;
static const uint64_t ORBLIT_NIR_SC_SENTINEL_ID_MASK = 0x00000000000000ff;

typedef struct OrblitNirCallbacks {
	void *data;
	void (*report_resource)(uint32_t p_register, uint32_t p_space, uint32_t p_dxil_type, void *p_data);
	void (*report_sc_bit_offset_fn)(uint32_t p_sc_id, uint64_t p_bit_offset, void *p_data);
	void (*report_bitcode_bit_offset_fn)(uint64_t p_bit_offset, void *p_data);
} OrblitNirCallbacks;

extern void *orblit_nir_malloc(size_t p_size);
extern void *orblit_nir_realloc(void *p_block, size_t p_size);
extern void orblit_nir_free(void *p_block);

#ifdef __cplusplus
}
#endif
