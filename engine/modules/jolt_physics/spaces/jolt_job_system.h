/**************************************************************************/
/*  jolt_job_system.h                                                     */

#pragma once

#include "core/containers/hash_map.h"
#include "core/os/spin_lock.h"

#include <Jolt/Jolt.h>

#include <Jolt/Core/FixedSizeFreeList.h>
#include <Jolt/Core/JobSystemWithBarrier.h>

#include <atomic>

class JoltJobSystem final : public JPH::JobSystemWithBarrier {
	class Job : public JPH::JobSystem::Job {
		inline static std::atomic<Job *> completed_head = nullptr;

#ifdef DEBUG_ENABLED
		const char *name = nullptr;
#endif

		int64_t task_id = -1;

		std::atomic<Job *> completed_next = nullptr;

		static void _execute(void *p_user_data);

	public:
		Job(const char *p_name, JPH::ColorArg p_color, JPH::JobSystem *p_job_system, const JPH::JobSystem::JobFunction &p_job_function, JPH::uint32 p_dependency_count);
		Job(const Job &p_other) = delete;
		Job(Job &&p_other) = delete;
		~Job();

		static void push_completed(Job *p_job);
		static Job *pop_completed();

		void queue();

		Job &operator=(const Job &p_other) = delete;
		Job &operator=(Job &&p_other) = delete;
	};

#ifdef DEBUG_ENABLED
	// We use `const void*` here to avoid the cost of hashing the actual string, since the job names
	// are always literals and as such will point to the same address every time.
	inline static HashMap<const void *, uint64_t> timings_by_job;

	// TODO: Check whether the usage of SpinLock is justified or if this should be a mutex instead.
	inline static SpinLock timings_lock;
#endif

	JPH::FixedSizeFreeList<Job> jobs;

	int thread_count = 0;

	virtual int GetMaxConcurrency() const override;

	virtual JPH::JobHandle CreateJob(const char *p_name, JPH::ColorArg p_color, const JPH::JobSystem::JobFunction &p_job_function, JPH::uint32 p_dependency_count = 0) override;
	virtual void QueueJob(JPH::JobSystem::Job *p_job) override;
	virtual void QueueJobs(JPH::JobSystem::Job **p_jobs, JPH::uint p_job_count) override;
	virtual void FreeJob(JPH::JobSystem::Job *p_job) override;

	void _reclaim_jobs();

public:
	JoltJobSystem();

	void pre_step();
	void post_step();

#ifdef DEBUG_ENABLED
	void flush_timings();
#endif
};
