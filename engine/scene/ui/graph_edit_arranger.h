/**************************************************************************/
/*  graph_edit_arranger.h                                                 */

#pragma once

#include "core/containers/hash_map.h"
#include "core/containers/hash_set.h"
#include "core/object/ref_counted.h"

class GraphEdit;

class GraphEditArranger : public RefCounted {
	ORBLIT_SOFTCLASS(GraphEditArranger, RefCounted);

	enum SET_OPERATIONS {
		IS_EQUAL,
		IS_SUBSET,
		DIFFERENCE,
		UNION,
	};

	GraphEdit *graph_edit = nullptr;
	bool arranging_graph = false;

	int _set_operations(SET_OPERATIONS p_operation, HashSet<StringName> &r_u, const HashSet<StringName> &r_v);
	HashMap<int, Vector<StringName>> _layering(const HashSet<StringName> &r_selected_nodes, const HashMap<StringName, HashSet<StringName>> &r_upper_neighbours);
	Vector<StringName> _split(const Vector<StringName> &r_layer, const HashMap<StringName, Dictionary> &r_crossings);
	void _horizontal_alignment(Dictionary &r_root, Dictionary &r_align, const HashMap<int, Vector<StringName>> &r_layers, const HashMap<StringName, HashSet<StringName>> &r_upper_neighbours, const HashSet<StringName> &r_selected_nodes);
	void _crossing_minimisation(HashMap<int, Vector<StringName>> &r_layers, const HashMap<StringName, HashSet<StringName>> &r_upper_neighbours);
	void _calculate_inner_shifts(Dictionary &r_inner_shifts, const Dictionary &r_root, const Dictionary &r_node_names, const Dictionary &r_align, const HashSet<StringName> &r_block_heads, const HashMap<StringName, Pair<int, int>> &r_port_info);
	float _calculate_threshold(const StringName &p_v, const StringName &p_w, const Dictionary &r_node_names, const HashMap<int, Vector<StringName>> &r_layers, const Dictionary &r_root, const Dictionary &r_align, const Dictionary &r_inner_shift, real_t p_current_threshold, const HashMap<StringName, Vector2> &r_node_positions);
	void _place_block(const StringName &p_v, float p_delta, const HashMap<int, Vector<StringName>> &r_layers, const Dictionary &r_root, const Dictionary &r_align, const Dictionary &r_node_name, const Dictionary &r_inner_shift, Dictionary &r_sink, Dictionary &r_shift, HashMap<StringName, Vector2> &r_node_positions);

public:
	void arrange_nodes();

	GraphEditArranger(GraphEdit *p_graph_edit) :
			graph_edit(p_graph_edit) {}
};
