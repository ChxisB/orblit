/**************************************************************************/
/*  editor_atlas_packer.h                                                 */

#pragma once

#include "core/containers/vector.h"
#include "core/math/vector2.h"
#include "core/math/vector2i.h"

class EditorAtlasPacker {
public:
	struct Chart {
		Vector<Vector2> vertices;
		struct Face {
			int vertex[3] = { 0 };
		};
		Vector<Face> faces;
		bool can_transpose = false;

		Vector2 final_offset;
		bool transposed = false;
	};

private:
	struct PlottedBitmap {
		int chart_index = 0;
		Vector2i offset;
		int area = 0;
		Vector<int> top_heights;
		Vector<int> bottom_heights;
		bool transposed = false;

		Vector2 final_pos;

		bool operator<(const PlottedBitmap &p_bm) const {
			return area > p_bm.area;
		}
	};

public:
	static void chart_pack(Vector<Chart> &charts, int &r_width, int &r_height, int p_atlas_max_size = 2048, int p_cell_resolution = 4);
};
