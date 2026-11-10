/**************************************************************************/
/*  net_socket_android.cpp                                                */

#include "net_socket_android.h"

#include "thread_jandroid.h"

jobject NetSocketAndroid::net_utils = nullptr;
jclass NetSocketAndroid::cls = nullptr;
jmethodID NetSocketAndroid::_multicast_lock_acquire = nullptr;
jmethodID NetSocketAndroid::_multicast_lock_release = nullptr;

void NetSocketAndroid::setup(jobject p_net_utils) {
	JNIEnv *env = get_jni_env();

	net_utils = env->NewGlobalRef(p_net_utils);

	jclass c = env->GetObjectClass(net_utils);
	cls = (jclass)env->NewGlobalRef(c);

	_multicast_lock_acquire = env->GetMethodID(cls, "multicastLockAcquire", "()V");
	_multicast_lock_release = env->GetMethodID(cls, "multicastLockRelease", "()V");
}

void NetSocketAndroid::terminate() {
	JNIEnv *env = get_jni_env();
	ERR_FAIL_NULL(env);

	env->DeleteGlobalRef(cls);
	env->DeleteGlobalRef(net_utils);
}

void NetSocketAndroid::multicast_lock_acquire() {
	if (_multicast_lock_acquire) {
		JNIEnv *env = get_jni_env();
		env->CallVoidMethod(net_utils, _multicast_lock_acquire);
	}
}

void NetSocketAndroid::multicast_lock_release() {
	if (_multicast_lock_release) {
		JNIEnv *env = get_jni_env();
		env->CallVoidMethod(net_utils, _multicast_lock_release);
	}
}

Ref<NetSocket> NetSocketAndroid::_create_func() {
	return memnew(NetSocketAndroid);
}

void NetSocketAndroid::make_default() {
	_create = _create_func;
}

NetSocketAndroid::~NetSocketAndroid() {
	close();
}

void NetSocketAndroid::close() {
	NetSocketUnix::close();
	if (wants_broadcast) {
		multicast_lock_release();
	}
	if (multicast_groups) {
		multicast_lock_release();
	}
	wants_broadcast = false;
	multicast_groups = 0;
}

Error NetSocketAndroid::set_broadcasting_enabled(bool p_enabled) {
	RETURN_IF_ERROR(NetSocketUnix::set_broadcasting_enabled(p_enabled));

	if (p_enabled != wants_broadcast) {
		if (p_enabled) {
			multicast_lock_acquire();
		} else {
			multicast_lock_release();
		}

		wants_broadcast = p_enabled;
	}

	return OK;
}

Error NetSocketAndroid::join_multicast_group(const IPAddress &p_multi_address, const String &p_if_name) {
	RETURN_IF_ERROR(NetSocketUnix::join_multicast_group(p_multi_address, p_if_name));

	if (!multicast_groups) {
		multicast_lock_acquire();
	}
	multicast_groups++;

	return OK;
}

Error NetSocketAndroid::leave_multicast_group(const IPAddress &p_multi_address, const String &p_if_name) {
	RETURN_IF_ERROR(NetSocketUnix::leave_multicast_group(p_multi_address, p_if_name));

	ERR_FAIL_COND_V(multicast_groups == 0, ERR_BUG);

	multicast_groups--;
	if (!multicast_groups) {
		multicast_lock_release();
	}

	return OK;
}
