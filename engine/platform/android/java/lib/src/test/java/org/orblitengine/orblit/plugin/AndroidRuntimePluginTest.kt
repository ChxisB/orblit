/**************************************************************************/
/*  AndroidRuntimePluginTest.kt                                           */

package org.orblitengine.orblit.plugin

import org.orblitengine.orblit.variant.Callable
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

class AndroidRuntimePluginTest {

	private val createProxyFromOrblitCallable = AndroidRuntimePlugin::class.java
		.getDeclaredMethod("createProxyFromOrblitCallable", String::class.java, Callable::class.java)
		.apply { isAccessible = true }

	private val createProxyFromOrblitObjectID = AndroidRuntimePlugin::class.java
		.getDeclaredMethod("createProxyFromOrblitObjectID", Long::class.javaPrimitiveType, Array<String>::class.java)
		.apply { isAccessible = true }

	private fun newCallable(): Callable {
		val ctor = Callable::class.java.getDeclaredConstructor(Long::class.javaPrimitiveType)
		ctor.isAccessible = true
		return ctor.newInstance(0L)
	}

	private fun callableProxy(): Any {
		val proxy = createProxyFromOrblitCallable.invoke(null, Runnable::class.java.name, newCallable())
		assertNotNull(proxy)
		return proxy!!
	}

	private fun objectIDProxy(orblitObjectID: Long = 0L): Any {
		val proxy = createProxyFromOrblitObjectID.invoke(null, orblitObjectID, arrayOf(Runnable::class.java.name))
		assertNotNull(proxy)
		return proxy!!
	}

	// Regression tests for the infinite recursion in the proxy equals handler.
	// Kotlin's `proxy == args[0]` compiles to `proxy.equals(args[0])`, which the
	// JDK proxy dispatches back through the InvocationHandler — the previous
	// implementation hit the same branch and recursed into StackOverflowError.

	@Test
	fun `Given Callable proxy, When equals is invoked with itself, Then returns true without recursion`() {
		val proxy = callableProxy()
		assertTrue(proxy.equals(proxy))
	}

	@Test
	fun `Given Callable proxy, When equals is invoked with another instance, Then returns false without recursion`() {
		val proxy = callableProxy()
		val otherProxy = callableProxy()
		assertFalse(proxy.equals(otherProxy))
		assertFalse(proxy.equals(Any()))
		assertFalse(proxy.equals(null))
	}

	@Test
	fun `Given ObjectID proxy, When equals is invoked with itself, Then returns true without recursion`() {
		val proxy = objectIDProxy()
		assertTrue(proxy.equals(proxy))
	}

	@Test
	fun `Given ObjectID proxy, When equals is invoked with another instance, Then returns false without recursion`() {
		val proxy = objectIDProxy()
		val otherProxy = objectIDProxy()
		assertFalse(proxy.equals(otherProxy))
		assertFalse(proxy.equals(Any()))
		assertFalse(proxy.equals(null))
	}

	// Sanity checks that the proxy's toString / hashCode handlers still operate
	// as documented and that they do not get caught up in the equals fix.

	@Test
	fun `Given Callable proxy, When toString is invoked, Then returns the documented label`() {
		val proxy = callableProxy()
		assertEquals("Orblit Callable Proxy for ${Runnable::class.java.name}", proxy.toString())
	}

	@Test
	fun `Given ObjectID proxy, When toString is invoked, Then returns the documented label`() {
		val proxy = objectIDProxy()
		assertEquals("Orblit Object Proxy for ${Runnable::class.java.name}", proxy.toString())
	}

	@Test
	fun `Given Callable proxy, When hashCode is invoked, Then returns the Callable hashCode`() {
		val callable = newCallable()
		val proxy = createProxyFromOrblitCallable.invoke(null, Runnable::class.java.name, callable)!!
		assertEquals(callable.hashCode(), proxy.hashCode())
	}

	@Test
	fun `Given ObjectID proxy, When hashCode is invoked, Then returns the ObjectID hashCode`() {
		val orblitObjectID = 0x1_0000_002AL
		val proxy = objectIDProxy(orblitObjectID)
		assertEquals(orblitObjectID.hashCode(), proxy.hashCode())
	}
}
