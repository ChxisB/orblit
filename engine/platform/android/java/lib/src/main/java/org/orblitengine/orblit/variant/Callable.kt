/**************************************************************************/
/*  Callable.kt                                                           */

package org.orblitengine.orblit.variant

import androidx.annotation.Keep

/**
 * Android version of a Orblit built-in Callable type representing a method or a standalone function.
 */
@Keep
class Callable private constructor(private val nativeCallablePointer: Long) {

	companion object {
		/**
		 * Invoke method [methodName] on the Orblit object specified by [orblitObjectId]
		 */
		@JvmStatic
		fun call(orblitObjectId: Long, methodName: String, vararg methodParameters: Any): Any? {
			return nativeCallObject(orblitObjectId, methodName, methodParameters)
		}

		/**
		 * Invoke method [methodName] on the Orblit object specified by [orblitObjectId] during idle time.
		 */
		@JvmStatic
		fun callDeferred(orblitObjectId: Long, methodName: String, vararg methodParameters: Any) {
			nativeCallObjectDeferred(orblitObjectId, methodName, methodParameters)
		}

		@JvmStatic
		private external fun nativeCall(pointer: Long, params: Array<out Any>): Any?

		@JvmStatic
		private external fun nativeCallObject(orblitObjectId: Long, methodName: String, params: Array<out Any>): Any?

		@JvmStatic
		private external fun nativeCallObjectDeferred(orblitObjectId: Long, methodName: String, params: Array<out Any>)

		@JvmStatic
		private external fun releaseNativePointer(nativePointer: Long)
	}

	/**
	 * Calls the method represented by this [Callable]. Arguments can be passed and should match the method's signature.
	 */
	fun call(vararg params: Any): Any? {
		if (nativeCallablePointer == 0L) {
			return null
		}

		return nativeCall(nativeCallablePointer, params)
	}

	/**
	 * Used to provide access to the native callable pointer to the native logic.
	 */
	private fun getNativePointer() = nativeCallablePointer

	/** Note that [finalize] is deprecated and shouldn't be used, unfortunately its replacement,
	 * [java.lang.ref.Cleaner], is only available on Android api 33 and higher.
	 * So we resort to using it for the time being until our min api catches up to api 33.
	 **/
	protected fun finalize() {
		releaseNativePointer(nativeCallablePointer)
	}
}
