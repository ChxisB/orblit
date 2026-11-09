/**************************************************************************/
/*  TestClass2.kt                                                         */

package com.orblit.game.test.javaclasswrapper

class TestClass2(private val value: Int) {
    fun getValue(): Int {
        return value
    }
    override fun toString(): String {
        return value.toString()
    }
}
