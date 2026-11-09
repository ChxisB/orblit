/**************************************************************************/
/*  AndroidRuntimePlugin.kt                                               */

package org.orblitengine.orblit.plugin

import android.content.Intent
import android.util.Log
import androidx.annotation.Keep
import androidx.core.net.toUri

import org.orblitengine.orblit.Orblit
import org.orblitengine.orblit.variant.Callable
import java.lang.reflect.InvocationHandler
import java.lang.reflect.Proxy

/**
 * Built-in Orblit Android plugin used to provide access to the Android runtime capabilities.
 */
class AndroidRuntimePlugin(orblit: Orblit) : OrblitPlugin(orblit) {

	companion object {
		private val TAG = AndroidRuntimePlugin::class.java.simpleName

		/**
		 * Helper method used to generate Orblit Proxy instances.
		 */
		@JvmStatic
		@Keep
		private fun generateProxyInstance(interfaces: Array<String>, invocationHandler: InvocationHandler): Any? {
			try {
				val interfaceClasses = interfaces.map { Class.forName(it) }.toTypedArray()

				val proxy = Proxy.newProxyInstance(invocationHandler.javaClass.classLoader, interfaceClasses, invocationHandler)
				return proxy
			} catch (e: Exception) {
				Log.w(TAG, "Error generating Orblit proxy for interfaces ${interfaces.joinToString(",")}", e)
			}
			return null
		}

		/**
		 * Utility method used to create [java.lang.reflect.Proxy] instance wrapping a given Orblit [Callable].
		 *
		 * The [Proxy] instance is used to implement one SAM interface with the [Callable] serving as the delegate
		 * implementation for the SAM interface overridden methods.
		 */
		@JvmStatic
		@Keep
		private fun createProxyFromOrblitCallable(interfaceName: String, orblitCallable: Callable): Any? {
			return generateProxyInstance(arrayOf(interfaceName)) { proxy, method, args ->
				when (method.name) {
					// We automatically handle 'toString', 'equals' and 'hashCode' to simplify the task of the caller
					// and provide consistency.
					"toString" -> "Orblit Callable Proxy for $interfaceName"
					"equals" -> proxy === args[0]
					"hashCode" -> orblitCallable.hashCode()

					// Invocation for the interface single abstract method falls here and is dispatched to the
					// Orblit [Callable].
					else -> orblitCallable.call(*(args ?: emptyArray()))
				}
			}
		}

		/**
		 * Utility method used to create [java.lang.reflect.Proxy] instance wrapping a given Orblit Object represented by
		 * its ObjectID.
		 *
		 * The [Proxy] instance is used to implement one or multiple interfaces with the Object represented by
		 * [orblitObjectID] serving as the delegate implementation for the interface(s) overridden methods.
		 */
		@JvmStatic
		@Keep
		private fun createProxyFromOrblitObjectID(orblitObjectID: Long, interfaces: Array<String>): Any? {
			return generateProxyInstance(interfaces) { proxy, method, args ->
				when (val methodName = method.name) {
					// We automatically handle 'toString', 'equals' and 'hashCode' to simplify the task of the caller
					// and provide consistency.
					"toString" -> "Orblit Object Proxy for ${interfaces.joinToString(",")}"
					"equals" -> proxy === args[0]
					"hashCode" -> orblitObjectID.hashCode()

					// Invocation for the remaining interface(s) methods falls here and is dispatched to the
					// Orblit Object.
					else -> Callable.call(orblitObjectID, methodName, *(args ?: emptyArray()))
				}
			}
		}
	}

	override fun getPluginName() = "AndroidRuntime"

	/**
	 * Provides access to the application [android.content.Context] to scripts
	 */
	@UsedByOrblit
	fun getApplicationContext() = activity?.applicationContext

	/**
	 * Provides access to the host [android.app.Activity] to scripts
	 */
	@UsedByOrblit
	public override fun getActivity() = super.getActivity()

	/**
	 * Utility method used to create [Runnable] from Orblit [Callable].
	 */
	@UsedByOrblit
	fun createRunnableFromOrblitCallable(orblitCallable: Callable): Runnable {
		return Runnable { orblitCallable.call() }
	}

	/**
	 * Utility method used to create [java.util.concurrent.Callable] from Orblit [Callable].
	 */
	@UsedByOrblit
	fun createCallableFromOrblitCallable(orblitCallable: Callable): java.util.concurrent.Callable<Any> {
		return java.util.concurrent.Callable { orblitCallable.call() }
	}

	/**
	 * Helper method to take/release persistable URI permission.
	 */
	@UsedByOrblit
	fun updatePersistableUriPermission(uriString: String, persist: Boolean): Boolean {
		try {
			val uri = uriString.toUri()
			val contentResolver = context.contentResolver
			if (persist) {
				contentResolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
			} else {
				contentResolver.releasePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
			}
		} catch (e: RuntimeException) {
			Log.d(TAG, "Error updating persistable permission: ", e)
			return false
		}
		return true
	}
}
