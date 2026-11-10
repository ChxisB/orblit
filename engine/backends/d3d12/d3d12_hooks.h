/**************************************************************************/
/*  d3d12_hooks.h                                                         */

#pragma once

#include <backends/d3d12/d3dx12.h>

class D3D12Hooks {
private:
	static D3D12Hooks *singleton;

public:
	D3D12Hooks();
	virtual ~D3D12Hooks();
	virtual D3D_FEATURE_LEVEL get_feature_level() const = 0;
	virtual LUID get_adapter_luid() const = 0;
	virtual void set_device(ID3D12Device *p_device) = 0;
	virtual void set_command_queue(ID3D12CommandQueue *p_queue) = 0;
	virtual void cleanup_device() = 0;
	static D3D12Hooks *get_singleton() { return singleton; }
};
