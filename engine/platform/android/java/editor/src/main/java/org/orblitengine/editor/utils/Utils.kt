/**************************************************************************/
/*  Utils.kt                                                              */

package org.orblitengine.editor.utils

import android.content.Context

object Utils {
	fun getDefaultSharedPreferences(context: Context?) =  context?.getSharedPreferences("${context.packageName}_preferences", Context.MODE_PRIVATE)
}
