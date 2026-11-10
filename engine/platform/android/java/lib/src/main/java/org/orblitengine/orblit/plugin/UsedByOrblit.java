/**************************************************************************/
/*  UsedByOrblit.java                                                     */

package org.orblitengine.orblit.plugin;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Annotation to indicate a method is being invoked from the Orblit game logic.
 *
 * At runtime, annotated plugin methods are detected and automatically registered.
 */
@Target({ ElementType.METHOD })
@Retention(RetentionPolicy.RUNTIME)
public @interface UsedByOrblit {}
