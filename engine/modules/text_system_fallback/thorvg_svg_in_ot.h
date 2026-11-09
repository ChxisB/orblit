/**************************************************************************/
/*  thorvg_svg_in_ot.h                                                    */

#pragma once

#include "core/os/mutex.h"
#include "core/string/ustring.h"
#include "core/containers/hash_map.h"
#include "core/typedefs.h"

#include "modules/modules_enabled.gen.h" // For svg, freetype.

#ifdef MODULE_SVG_ENABLED
#ifdef MODULE_FREETYPE_ENABLED

#include <freetype/freetype.h>
#include <freetype/otsvg.h>
#include <ft2build.h>
#include <thorvg.h>

struct GL_State {
	bool ready = false;
	float x = 0;
	float y = 0;
	float w = 0;
	float h = 0;
	CharString xml_code;
	tvg::Matrix m;
};

struct TVG_NodeCache {
	uint64_t document_offset;
	uint64_t body_offset;
};

struct TVG_DocumentCache {
	String xml_body;
	double embox_x;
	double embox_y;
	HashMap<int64_t, Vector<TVG_NodeCache>> node_caches;
};

struct TVG_State {
	Mutex mutex;
	HashMap<uint32_t, GL_State> glyph_map;
	HashMap<FT_Byte *, TVG_DocumentCache> document_map;
};

FT_Error tvg_svg_in_ot_init(FT_Pointer *p_state);
void tvg_svg_in_ot_free(FT_Pointer *p_state);
FT_Error tvg_svg_in_ot_preset_slot(FT_GlyphSlot p_slot, FT_Bool p_cache, FT_Pointer *p_state);
FT_Error tvg_svg_in_ot_render(FT_GlyphSlot p_slot, FT_Pointer *p_state);

SVG_RendererHooks *get_tvg_svg_in_ot_hooks();

#endif // MODULE_FREETYPE_ENABLED
#endif // MODULE_SVG_ENABLED
