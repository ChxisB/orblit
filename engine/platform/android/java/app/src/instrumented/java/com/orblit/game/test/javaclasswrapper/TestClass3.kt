/**************************************************************************/
/*  TestClass3.kt                                                         */

package com.orblit.game.test.javaclasswrapper

class TestClass3(private val value: String) {
    fun getValue(): String {
        return value
    }
    override fun toString(): String {
        return value
    }
}
