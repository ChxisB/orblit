/**************************************************************************/
/*  default_theme.h                                                       */

#pragma once

#include "scene/resources/theme.h"

void fill_default_theme(Ref<Theme> &theme, const Ref<Font> &default_font, const Ref<Font> &bold_font, const Ref<Font> &bold_italics_font, const Ref<Font> &italics_font, Ref<Texture2D> &default_icon, Ref<StyleBox> &default_style, float p_scale);
void make_default_theme(float p_scale, Ref<Font> p_font, TextSystem::SubpixelPositioning p_font_subpixel = TextSystem::SUBPIXEL_POSITIONING_AUTO, TextSystem::Hinting p_font_hinting = TextSystem::HINTING_LIGHT, TextSystem::FontAntialiasing p_font_antialiased = TextSystem::FONT_ANTIALIASING_GRAY, bool p_font_msdf = false, bool p_font_generate_mipmaps = false);
