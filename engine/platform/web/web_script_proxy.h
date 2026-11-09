/**************************************************************************/
/*  web_script_proxy.h                                                    */

#pragma once

// Lets a scene attach a .cs script to an entity on the Web, where the C#
// runs in the page and not in the engine. See web_script_proxy.cpp.

// Adds the .cs resource loader. Call after Main::setup and before
// Main::start, so the main scene can load its scripts.
void web_script_proxy_register();

// Removes the loader. Call before Main::cleanup.
void web_script_proxy_unregister();

// Tells the page about every entity with a .cs script, now and as entities
// enter or leave the stage. Call once, after the page can take the events.
void web_script_proxy_start();
