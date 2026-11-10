/**************************************************************************/
/*  shader_expression.h                                                   */

#pragma once

#include "core/math/expression.h"

class ShaderExpression : public Expression {
	ORBLIT_CLASS(ShaderExpression, Expression);

protected:
	virtual Error _get_token(Token &r_token) override;
};
