/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "csharp_script.h"
#include "csharp_script_resource_format.h"

#include "core/io/resource_loader.h"
#include "core/io/resource_saver.h"
#include "core/object/class_registry.h"

CSharpLanguage *script_language_cs = nullptr;
Ref<ResourceFormatLoaderCSharpScript> resource_loader_cs;
Ref<ResourceFormatSaverCSharpScript> resource_saver_cs;

ManagedBind::OrblitSharp *_orblitsharp = nullptr;

void initialize_csharp_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ORBLIT_REGISTER_CLASS(CSharpScript);

	_orblitsharp = memnew(ManagedBind::OrblitSharp);

	script_language_cs = memnew(CSharpLanguage);
	script_language_cs->set_language_index(ScriptSystem::get_language_count());
	ScriptSystem::register_language(script_language_cs);

	if constexpr (ORBLIT_IS_CLASS_ENABLED(CSharpScript)) {
		resource_loader_cs.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_cs);
		resource_saver_cs.instantiate();
		ResourceSaver::add_resource_format_saver(resource_saver_cs);
	}
}

void uninitialize_csharp_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ScriptSystem::unregister_language(script_language_cs);

	memdelete(script_language_cs);

	if constexpr (ORBLIT_IS_CLASS_ENABLED(CSharpScript)) {
		ResourceLoader::remove_resource_format_loader(resource_loader_cs);
		resource_loader_cs.unref();
		ResourceSaver::remove_resource_format_saver(resource_saver_cs);
		resource_saver_cs.unref();
	}

	memdelete(_orblitsharp);
}
