/**************************************************************************/
/*  d3d12_hooks.cpp                                                       */

#include "d3d12_hooks.h"

D3D12Hooks *D3D12Hooks::singleton = nullptr;

D3D12Hooks::D3D12Hooks() {
	if (singleton == nullptr) {
		singleton = this;
	}
}

D3D12Hooks::~D3D12Hooks() {
	if (singleton == this) {
		singleton = nullptr;
	}
}
