/**************************************************************************/
/*  system_wrap_mt_common.h                                               */

#pragma once

#ifdef DEBUG_ENABLED
#include "core/config/engine.h" // IWYU pragma: keep. Used in macro.

#define MAIN_THREAD_SYNC_CHECK \
	if (unlikely(Thread::is_main_thread() && Engine::get_singleton()->notify_frame_system_synced())) { \
		MAIN_THREAD_SYNC_WARN \
	}
#else
#define MAIN_THREAD_SYNC_CHECK
#endif

#define FUNC0R(m_r, m_type) \
	virtual m_r m_type() override { \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(); \
		} \
	}

#define FUNCRIDSPLIT(m_type) \
	virtual RID m_type##_create() override { \
		RID ret = system_name->m_type##_allocate(); \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type##_initialize, ret); \
		} else { \
			system_name->m_type##_initialize(ret); \
		} \
		return ret; \
	}

//RID now returns directly, ensure thread safety yourself
#define FUNCRID(m_type) \
	virtual RID m_type##_create() override { \
		return system_name->m_type##_create(); \
	}

#define FUNC0RC(m_r, m_type) \
	virtual m_r m_type() const override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(); \
		} \
	}

#define FUNC0(m_type) \
	virtual void m_type() override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(); \
		} \
	}

#define FUNC0C(m_type) \
	virtual void m_type() const override { \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(); \
		} \
	}

#define FUNC0S(m_type) \
	virtual void m_type() override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(); \
		} \
	}

#define FUNC0SC(m_type) \
	virtual void m_type() const override { \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(); \
		} \
	}

///////////////////////////////////////////////

#define FUNC1R(m_r, m_type, m_arg1) \
	virtual m_r m_type(m_arg1 p1) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1); \
		} \
	}

#define FUNC1RC(m_r, m_type, m_arg1) \
	virtual m_r m_type(m_arg1 p1) const override { \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1); \
		} \
	}

#define FUNC1S(m_type, m_arg1) \
	virtual void m_type(m_arg1 p1) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1); \
		} \
	}

#define FUNC1SC(m_type, m_arg1) \
	virtual void m_type(m_arg1 p1) const override { \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1); \
		} \
	}

#define FUNC1(m_type, m_arg1) \
	virtual void m_type(m_arg1 p1) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1); \
		} \
	}

#define FUNC1C(m_type, m_arg1) \
	virtual void m_type(m_arg1 p1) const override { \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1); \
		} \
	}

#define FUNC2R(m_r, m_type, m_arg1, m_arg2) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2); \
		} \
	}

#define FUNC2RC(m_r, m_type, m_arg1, m_arg2) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2) const override { \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2); \
		} \
	}

#define FUNC2S(m_type, m_arg1, m_arg2) \
	virtual void m_type(m_arg1 p1, m_arg2 p2) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2); \
		} \
	}

#define FUNC2SC(m_type, m_arg1, m_arg2) \
	virtual void m_type(m_arg1 p1, m_arg2 p2) const override { \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2); \
		} \
	}

#define FUNC2(m_type, m_arg1, m_arg2) \
	virtual void m_type(m_arg1 p1, m_arg2 p2) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2); \
		} \
	}

#define FUNC2C(m_type, m_arg1, m_arg2) \
	virtual void m_type(m_arg1 p1, m_arg2 p2) const override { \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2); \
		} \
	}

#define FUNC3R(m_r, m_type, m_arg1, m_arg2, m_arg3) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3); \
		} \
	}

#define FUNC3RC(m_r, m_type, m_arg1, m_arg2, m_arg3) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3) const override { \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3); \
		} \
	}

#define FUNC3S(m_type, m_arg1, m_arg2, m_arg3) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3); \
		} \
	}

#define FUNC3SC(m_type, m_arg1, m_arg2, m_arg3) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3) const override { \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3); \
		} \
	}

#define FUNC3(m_type, m_arg1, m_arg2, m_arg3) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3); \
		} \
	}

#define FUNC3C(m_type, m_arg1, m_arg2, m_arg3) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3) const override { \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3); \
		} \
	}

#define FUNC4R(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4); \
		} \
	}

#define FUNC4RC(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4) const override { \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4); \
		} \
	}

#define FUNC4S(m_type, m_arg1, m_arg2, m_arg3, m_arg4) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4); \
		} \
	}

#define FUNC4SC(m_type, m_arg1, m_arg2, m_arg3, m_arg4) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4) const override { \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4); \
		} \
	}

#define FUNC4(m_type, m_arg1, m_arg2, m_arg3, m_arg4) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4); \
		} \
	}

#define FUNC4C(m_type, m_arg1, m_arg2, m_arg3, m_arg4) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4) const override { \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4); \
		} \
	}

#define FUNC5R(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4, p5); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4, p5); \
		} \
	}

#define FUNC5RC(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5) const override { \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4, p5); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4, p5); \
		} \
	}

#define FUNC5S(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4, p5); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5); \
		} \
	}

#define FUNC5SC(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5) const override { \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4, p5); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5); \
		} \
	}

#define FUNC5(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5); \
		} \
	}

#define FUNC5C(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5) const override { \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5); \
		} \
	}

#define FUNC6R(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6) { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4, p5, p6); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4, p5, p6); \
		} \
	}

#define FUNC6RC(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6) const override { \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4, p5, p6); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4, p5, p6); \
		} \
	}

#define FUNC6S(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6); \
		} \
	}

#define FUNC6SC(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6) const override { \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6); \
		} \
	}

#define FUNC6(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6); \
		} \
	}

#define FUNC6C(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6) const override { \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6); \
		} \
	}

#define FUNC7R(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4, p5, p6, p7); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4, p5, p6, p7); \
		} \
	}

#define FUNC7RC(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7) const override { \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4, p5, p6, p7); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4, p5, p6, p7); \
		} \
	}

#define FUNC7S(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7); \
		} \
	}

#define FUNC7SC(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7) const override { \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7); \
		} \
	}

#define FUNC7(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7); \
		} \
	}

#define FUNC7C(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7) const override { \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7); \
		} \
	}

#define FUNC8R(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4, p5, p6, p7, p8); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8); \
		} \
	}

#define FUNC8RC(m_r, m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8) \
	virtual m_r m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8) const override { \
		if (ASYNC_COND_PUSH_AND_RET) { \
			m_r ret; \
			command_queue.push_and_ret(system_name, &SystemName::m_type, &ret, p1, p2, p3, p4, p5, p6, p7, p8); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
			return ret; \
		} else { \
			command_queue.flush_if_pending(); \
			return system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8); \
		} \
	}

#define FUNC8S(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8); \
		} \
	}

#define FUNC8SC(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8) const override { \
		if (ASYNC_COND_PUSH_AND_SYNC) { \
			command_queue.push_and_sync(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8); \
			SYNC_DEBUG \
			MAIN_THREAD_SYNC_CHECK \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8); \
		} \
	}

#define FUNC8(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8); \
		} \
	}

#define FUNC8C(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8) const override { \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8); \
		} \
	}

#define FUNC9(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8, m_arg9) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8, m_arg9 p9) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8, p9); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8, p9); \
		} \
	}

#define FUNC10(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8, m_arg9, m_arg10) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8, m_arg9 p9, m_arg10 p10) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10); \
		} \
	}

#define FUNC11(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8, m_arg9, m_arg10, m_arg11) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8, m_arg9 p9, m_arg10 p10, m_arg11 p11) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11); \
		} \
	}

#define FUNC12(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8, m_arg9, m_arg10, m_arg11, m_arg12) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8, m_arg9 p9, m_arg10 p10, m_arg11 p11, m_arg12 p12) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12); \
		} \
	}

#define FUNC13(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8, m_arg9, m_arg10, m_arg11, m_arg12, m_arg13) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8, m_arg9 p9, m_arg10 p10, m_arg11 p11, m_arg12 p12, m_arg13 p13) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13); \
		} \
	}

#define FUNC14(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8, m_arg9, m_arg10, m_arg11, m_arg12, m_arg13, m_arg14) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8, m_arg9 p9, m_arg10 p10, m_arg11 p11, m_arg12 p12, m_arg13 p13, m_arg14 p14) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14); \
		} \
	}

#define FUNC15(m_type, m_arg1, m_arg2, m_arg3, m_arg4, m_arg5, m_arg6, m_arg7, m_arg8, m_arg9, m_arg10, m_arg11, m_arg12, m_arg13, m_arg14, m_arg15) \
	virtual void m_type(m_arg1 p1, m_arg2 p2, m_arg3 p3, m_arg4 p4, m_arg5 p5, m_arg6 p6, m_arg7 p7, m_arg8 p8, m_arg9 p9, m_arg10 p10, m_arg11 p11, m_arg12 p12, m_arg13 p13, m_arg14 p14, m_arg15 p15) override { \
		WRITE_ACTION \
		if (ASYNC_COND_PUSH) { \
			command_queue.push(system_name, &SystemName::m_type, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15); \
		} else { \
			command_queue.flush_if_pending(); \
			system_name->m_type(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15); \
		} \
	}
