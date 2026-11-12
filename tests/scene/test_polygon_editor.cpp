/**************************************************************************/
/*  test_polygon_editor.cpp                                               */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_polygon_editor)

#ifdef TOOLS_ENABLED

#include "editor/scene/planar/abstract_polygon_planar_editor.h"

namespace TestPolygonEditor {

class CenterProbe final : public AbstractPolygon2DEditor {
	Vector<Vector<Vector2>> polygons;

protected:
	Entity2D *_get_node() const override { return nullptr; }
	void _set_node(Entity *p_polygon) override {}
	int _get_polygon_count() const override { return polygons.size(); }
	Variant _get_polygon(int p_idx) const override { return polygons[p_idx]; }

public:
	void add_polygon(const Vector<Vector2> &p_polygon) { polygons.push_back(p_polygon); }
	Vector2 get_center() const { return _get_geometric_center(); }
};

static Vector<Vector2> square(const Vector2 &p_origin, real_t p_side) {
	Vector<Vector2> points;
	points.push_back(p_origin);
	points.push_back(p_origin + Vector2(p_side, 0));
	points.push_back(p_origin + Vector2(p_side, p_side));
	points.push_back(p_origin + Vector2(0, p_side));
	return points;
}

TEST_CASE("[Stage][PolygonEditor] The center is weighted by the area of each polygon") {
	CenterProbe *probe = memnew(CenterProbe);
	probe->add_polygon(square(Vector2(0, 0), 4));
	probe->add_polygon(square(Vector2(10, 0), 1));

	const Vector2 center = probe->get_center();
	CHECK(center.x == doctest::Approx(2.5));
	CHECK(center.y == doctest::Approx(1.9117647));
	memdelete(probe);
}

TEST_CASE("[Stage][PolygonEditor] The center of no polygon is the origin") {
	CenterProbe *probe = memnew(CenterProbe);

	CHECK(probe->get_center() == Vector2());
	memdelete(probe);
}

} // namespace TestPolygonEditor

#endif // TOOLS_ENABLED
