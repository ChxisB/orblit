/**************************************************************************/
/*  register_core_types.cpp                                               */

#include "register_core_types.h"

#include "core/config/engine.h"
#include "core/config/project_settings.h"
#include "core/script_api.h"
#include "core/crypto/aes_context.h"
#include "core/crypto/crypto.h"
#include "core/crypto/crypto_resource_format.h"
#include "core/crypto/hashing_context.h"
#include "core/debugger/engine_profiler.h"
#include "core/native_extension/native_extension.h"
#include "core/native_extension/native_extension_manager.h"
#include "core/native_extension/native_extension_resource_format.h"
#include "core/native_extension/instance.h"
#include "core/input/input.h"
#include "core/input/input_map.h"
#include "core/input/shortcut.h"
#include "core/io/config_file.h"
#include "core/io/dir_access.h"
#include "core/io/dtls_server.h"
#include "core/io/http_client.h"
#include "core/io/image_loader.h"
#include "core/io/image_resource_format.h"
#include "core/io/json.h"
#include "core/io/json_resource_format.h"
#include "core/io/marshalls.h"
#include "core/io/missing_resource.h"
#include "core/io/packet_peer.h"
#include "core/io/packet_peer_dtls.h"
#include "core/io/packet_peer_udp.h"
#include "core/io/pck_packer.h"
#include "core/io/resource_format_binary.h"
#include "core/io/resource_importer.h"
#include "core/io/resource_loader.h"
#include "core/io/resource_saver.h"
#include "core/io/resource_uid.h"
#include "core/io/stream_peer_gzip.h"
#include "core/io/stream_peer_tls.h"
#include "core/io/tcp_server.h"
#include "core/io/translation_loader_po.h"
#include "core/io/udp_server.h"
#include "core/io/uds_server.h"
#include "core/io/xml_parser.h"
#include "core/math/a_star.h"
#include "core/math/a_star_grid_planar.h"
#include "core/math/expression.h"
#include "core/math/random_number_generator.h"
#include "core/math/triangle_mesh.h"
#include "core/object/class_registry.h"
#include "core/object/script_backtrace.h"
#include "core/object/script_language_extension.h"
#include "core/object/undo_redo.h"
#include "core/object/worker_thread_pool.h"
#include "core/os/main_loop.h"
#include "core/os/os.h"
#include "core/os/time.h"
#include "core/string/fuzzy_search.h"
#include "core/string/optimized_translation.h"
#include "core/string/regex.h"
#include "core/string/translation.h"
#include "core/string/translation_system.h"

static Ref<ResourceFormatSaverBinary> resource_saver_binary;
static Ref<ResourceFormatLoaderBinary> resource_loader_binary;
static Ref<ResourceFormatImporter> resource_format_importer;
static Ref<ResourceFormatImporterSaver> resource_format_importer_saver;
static Ref<ResourceFormatLoaderImage> resource_format_image;
static Ref<TranslationLoaderPO> resource_format_po;
static Ref<ResourceFormatSaverCrypto> resource_format_saver_crypto;
static Ref<ResourceFormatLoaderCrypto> resource_format_loader_crypto;
static Ref<NativeExtensionResourceLoader> resource_loader_native_extension;
static Ref<ResourceFormatSaverJSON> resource_saver_json;
static Ref<ResourceFormatLoaderJSON> resource_loader_json;

static ScriptApi::ResourceLoader *_resource_loader = nullptr;
static ScriptApi::ResourceSaver *_resource_saver = nullptr;
static ScriptApi::OS *_os = nullptr;
static ScriptApi::Engine *_engine = nullptr;
static ScriptApi::Special::ClassRegistry *_class_registry = nullptr;
static ScriptApi::Marshalls *_marshalls = nullptr;
static ScriptApi::EngineDebugger *_engine_debugger = nullptr;

static IP *ip = nullptr;
static Time *_time = nullptr;

static ScriptApi::Geometry2D *_geometry_2d = nullptr;
static ScriptApi::Geometry3D *_geometry_3d = nullptr;

static WorkerThreadPool *worker_thread_pool = nullptr;

extern Mutex _global_mutex;

static NativeExtensionManager *native_extension_manager = nullptr;

extern void register_global_constants();
extern void unregister_global_constants();

static ResourceUID *resource_uid = nullptr;

static bool _is_core_extensions_registered = false;

void register_core_types() {
	OS::get_singleton()->benchmark_begin_measure("Core", "Register Types");

	//consistency check
	static_assert(sizeof(Callable) <= 16);

	CryptoCore::initialize();

	ObjectRegistry::setup();
	StringName::setup();
	register_global_constants();
	CoreStringNames::create();

	ORBLIT_REGISTER_CLASS(Object);
	ORBLIT_REGISTER_CLASS(RefCounted);
	ORBLIT_REGISTER_CLASS(ScriptApi::WeakRef);
	ORBLIT_REGISTER_CLASS(Resource);

	ORBLIT_REGISTER_CLASS(Time);
	_time = memnew(Time);
	ResourceLoader::initialize();

	Variant::register_types();

	ORBLIT_REGISTER_CLASS(ResourceFormatLoader);
	ORBLIT_REGISTER_CLASS(ResourceFormatSaver);

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Translation)) {
		resource_format_po.instantiate();
		ResourceLoader::add_resource_format_loader(resource_format_po);
	}

	resource_saver_binary.instantiate();
	ResourceSaver::add_resource_format_saver(resource_saver_binary);
	resource_loader_binary.instantiate();
	ResourceLoader::add_resource_format_loader(resource_loader_binary);

	resource_format_importer.instantiate();
	ResourceLoader::add_resource_format_loader(resource_format_importer);

	resource_format_importer_saver.instantiate();
	ResourceSaver::add_resource_format_saver(resource_format_importer_saver);

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Image)) {
		resource_format_image.instantiate();
		ResourceLoader::add_resource_format_loader(resource_format_image);
	}

	ORBLIT_REGISTER_ABSTRACT_CLASS(Script);
	ORBLIT_REGISTER_ABSTRACT_CLASS(ScriptLanguage);
	ORBLIT_REGISTER_CLASS(ScriptBacktrace);
	ORBLIT_REGISTER_VIRTUAL_CLASS(ScriptExtension);
	ORBLIT_REGISTER_VIRTUAL_CLASS(ScriptLanguageExtension);

	ORBLIT_REGISTER_CLASS(MissingResource);
	ORBLIT_REGISTER_CLASS(Image);

	ORBLIT_REGISTER_CLASS(Shortcut);
	ORBLIT_REGISTER_ABSTRACT_CLASS(InputEvent);
	ORBLIT_REGISTER_ABSTRACT_CLASS(InputEventFromWindow);
	ORBLIT_REGISTER_ABSTRACT_CLASS(InputEventWithModifiers);
	ORBLIT_REGISTER_CLASS(InputEventKey);
	ORBLIT_REGISTER_CLASS(InputEventShortcut);
	ORBLIT_REGISTER_ABSTRACT_CLASS(InputEventMouse);
	ORBLIT_REGISTER_CLASS(InputEventMouseButton);
	ORBLIT_REGISTER_CLASS(InputEventMouseMotion);
	ORBLIT_REGISTER_CLASS(InputEventJoypadButton);
	ORBLIT_REGISTER_CLASS(InputEventJoypadMotion);
	ORBLIT_REGISTER_CLASS(InputEventScreenDrag);
	ORBLIT_REGISTER_CLASS(InputEventScreenTouch);
	ORBLIT_REGISTER_CLASS(InputEventAction);
	ORBLIT_REGISTER_ABSTRACT_CLASS(InputEventGesture);
	ORBLIT_REGISTER_CLASS(InputEventMagnifyGesture);
	ORBLIT_REGISTER_CLASS(InputEventPanGesture);
	ORBLIT_REGISTER_CLASS(InputEventMIDI);

	// Network
	ORBLIT_REGISTER_ABSTRACT_CLASS(StreamPeer);
	ORBLIT_REGISTER_ABSTRACT_CLASS(StreamPeerSocket);
	ORBLIT_REGISTER_ABSTRACT_CLASS(SocketServer);
	ORBLIT_REGISTER_CLASS(StreamPeerExtension);
	ORBLIT_REGISTER_CLASS(StreamPeerBuffer);
	ORBLIT_REGISTER_CLASS(StreamPeerGZIP);
	ORBLIT_REGISTER_CLASS(StreamPeerTCP);
	ORBLIT_REGISTER_CLASS(TCPServer);

	// IPC using UNIX domain sockets.
	ORBLIT_REGISTER_CLASS(StreamPeerUDS);
	ORBLIT_REGISTER_CLASS(UDSServer);

	ORBLIT_REGISTER_ABSTRACT_CLASS(PacketPeer);
	ORBLIT_REGISTER_CLASS(PacketPeerExtension);
	ORBLIT_REGISTER_CLASS(PacketPeerStream);
	ORBLIT_REGISTER_CLASS(PacketPeerUDP);
	ORBLIT_REGISTER_CLASS(UDPServer);

	ORBLIT_REGISTER_ABSTRACT_CLASS(WorkerThreadPool);

	ClassRegistry::register_custom_instance_class<HTTPClient>();

	// Crypto
	ORBLIT_REGISTER_CLASS(HashingContext);
	ORBLIT_REGISTER_CLASS(AESContext);
	ClassRegistry::register_custom_instance_class<X509Certificate>();
	ClassRegistry::register_custom_instance_class<CryptoKey>();
	ORBLIT_REGISTER_ABSTRACT_CLASS(TLSOptions);
	ClassRegistry::register_custom_instance_class<HMACContext>();
	ClassRegistry::register_custom_instance_class<Crypto>();
	ClassRegistry::register_custom_instance_class<StreamPeerTLS>();
	ClassRegistry::register_custom_instance_class<PacketPeerDTLS>();
	ClassRegistry::register_custom_instance_class<DTLSServer>();

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Crypto)) {
		resource_format_saver_crypto.instantiate();
		ResourceSaver::add_resource_format_saver(resource_format_saver_crypto);

		resource_format_loader_crypto.instantiate();
		ResourceLoader::add_resource_format_loader(resource_format_loader_crypto);
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(JSON)) {
		resource_saver_json.instantiate();
		ResourceSaver::add_resource_format_saver(resource_saver_json);

		resource_loader_json.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_json);
	}

	ORBLIT_REGISTER_CLASS(MainLoop);
	ORBLIT_REGISTER_CLASS(Translation);
	ORBLIT_REGISTER_CLASS(TranslationDomain);
	ORBLIT_REGISTER_CLASS(OptimizedTranslation);
	ORBLIT_REGISTER_CLASS(UndoRedo);
	ORBLIT_REGISTER_CLASS(TriangleMesh);

	ORBLIT_REGISTER_ABSTRACT_CLASS(FileAccess);
	ORBLIT_REGISTER_ABSTRACT_CLASS(DirAccess);
	ORBLIT_REGISTER_CLASS(ScriptApi::Thread);
	ORBLIT_REGISTER_CLASS(ScriptApi::Mutex);
	ORBLIT_REGISTER_CLASS(ScriptApi::Semaphore);
	ORBLIT_REGISTER_VIRTUAL_CLASS(ScriptApi::Logger);

	ORBLIT_REGISTER_CLASS(XMLParser);
	ORBLIT_REGISTER_CLASS(JSON);
	ORBLIT_REGISTER_CLASS(RegExMatch);
	ORBLIT_REGISTER_CLASS(RegEx);

	ORBLIT_REGISTER_CLASS(ConfigFile);

	ORBLIT_REGISTER_CLASS(PCKPacker);

	ORBLIT_REGISTER_CLASS(AStar3D);
	ORBLIT_REGISTER_CLASS(AStar2D);
	ORBLIT_REGISTER_CLASS(AStarGrid2D);
	ORBLIT_REGISTER_CLASS(EncodedObjectAsID);
	ORBLIT_REGISTER_CLASS(RandomNumberGenerator);

	ORBLIT_REGISTER_ABSTRACT_CLASS(ImageFormatLoader);
	ORBLIT_REGISTER_CLASS(ImageFormatLoaderExtension);
	ORBLIT_REGISTER_ABSTRACT_CLASS(ResourceImporter);

	ORBLIT_REGISTER_CLASS(NativeExtension);

	ORBLIT_REGISTER_ABSTRACT_CLASS(OrblitInstance);

	ORBLIT_REGISTER_ABSTRACT_CLASS(NativeExtensionManager);

	ORBLIT_REGISTER_ABSTRACT_CLASS(ResourceUID);

	ORBLIT_REGISTER_CLASS(EngineProfiler);

	ORBLIT_REGISTER_CLASS(FuzzySearch);
	ORBLIT_REGISTER_CLASS(FuzzySearchMatch);

	resource_uid = memnew(ResourceUID);

	native_extension_manager = memnew(NativeExtensionManager);

	if constexpr (ORBLIT_IS_CLASS_ENABLED(NativeExtension)) {
		resource_loader_native_extension.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_native_extension);
	}

	ORBLIT_REGISTER_ABSTRACT_CLASS(IP);
	ORBLIT_REGISTER_CLASS(ScriptApi::Geometry2D);
	ORBLIT_REGISTER_CLASS(ScriptApi::Geometry3D);
	ORBLIT_REGISTER_CLASS(ScriptApi::ResourceLoader);
	ORBLIT_REGISTER_CLASS(ScriptApi::ResourceSaver);
	ORBLIT_REGISTER_CLASS(ScriptApi::OS);
	ORBLIT_REGISTER_CLASS(ScriptApi::Engine);
	ORBLIT_REGISTER_CLASS(ScriptApi::Special::ClassRegistry);
	ORBLIT_REGISTER_CLASS(ScriptApi::Marshalls);
	ORBLIT_REGISTER_CLASS(ScriptApi::EngineDebugger);

	ORBLIT_REGISTER_CLASS(TranslationSystem);
	ORBLIT_REGISTER_ABSTRACT_CLASS(Input);
	ORBLIT_REGISTER_CLASS(InputMap);
	ORBLIT_REGISTER_CLASS(Expression);
	ORBLIT_REGISTER_CLASS(ProjectSettings);

	ip = IP::create();

	_geometry_2d = memnew(ScriptApi::Geometry2D);
	_geometry_3d = memnew(ScriptApi::Geometry3D);

	_resource_loader = memnew(ScriptApi::ResourceLoader);
	_resource_saver = memnew(ScriptApi::ResourceSaver);
	_os = memnew(ScriptApi::OS);
	_engine = memnew(ScriptApi::Engine);
	_class_registry = memnew(ScriptApi::Special::ClassRegistry);
	_marshalls = memnew(ScriptApi::Marshalls);
	_engine_debugger = memnew(ScriptApi::EngineDebugger);

	ORBLIT_REGISTER_NATIVE_STRUCT(ObjectID, "uint64_t id = 0");
	ORBLIT_REGISTER_NATIVE_STRUCT(ScriptLanguageExtensionProfilingInfo, "StringName signature;uint64_t call_count;uint64_t total_time;uint64_t self_time");

	worker_thread_pool = memnew(WorkerThreadPool);

	OS::get_singleton()->benchmark_end_measure("Core", "Register Types");
}

void register_core_settings() {
	// Since in register core types, globals may not be present.
	GLOBAL_DEF(PropertyInfo(Variant::INT, "network/limits/tcp/connect_timeout_seconds", PROPERTY_HINT_RANGE, "1,1800,1"), (30));
	GLOBAL_DEF(PropertyInfo(Variant::INT, "network/limits/unix/connect_timeout_seconds", PROPERTY_HINT_RANGE, "1,1800,1"), (30));
	GLOBAL_DEF_RST(PropertyInfo(Variant::INT, "network/limits/packet_peer_stream/max_buffer_po2", PROPERTY_HINT_RANGE, "8,64,1,or_greater"), (16));
	GLOBAL_DEF(PropertyInfo(Variant::STRING, "network/tls/certificate_bundle_override", PROPERTY_HINT_FILE, "*.crt"), "");

	GLOBAL_DEF("threading/worker_pool/max_threads", -1);
	GLOBAL_DEF("threading/worker_pool/low_priority_thread_ratio", 0.3);
}

void register_early_core_singletons() {
	Engine::get_singleton()->add_singleton(Engine::Singleton("Engine", ScriptApi::Engine::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("ProjectSettings", ProjectSettings::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("OS", ScriptApi::OS::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("Time", Time::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("ClassRegistry", _class_registry));
}

void register_core_singletons() {
	OS::get_singleton()->benchmark_begin_measure("Core", "Register Singletons");

	Engine::get_singleton()->add_singleton(Engine::Singleton("IP", IP::get_singleton(), "IP"));
	Engine::get_singleton()->add_singleton(Engine::Singleton("Geometry2D", ScriptApi::Geometry2D::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("Geometry3D", ScriptApi::Geometry3D::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("ResourceLoader", ScriptApi::ResourceLoader::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("ResourceSaver", ScriptApi::ResourceSaver::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("Marshalls", ScriptApi::Marshalls::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("TranslationSystem", TranslationSystem::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("Input", Input::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("InputMap", InputMap::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("EngineDebugger", ScriptApi::EngineDebugger::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("NativeExtensionManager", NativeExtensionManager::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("ResourceUID", ResourceUID::get_singleton()));
	Engine::get_singleton()->add_singleton(Engine::Singleton("WorkerThreadPool", worker_thread_pool));

	OS::get_singleton()->benchmark_end_measure("Core", "Register Singletons");
}

void register_core_extensions() {
	OS::get_singleton()->benchmark_begin_measure("Core", "Register Extensions");

	// Hardcoded for now.
	NativeExtension::initialize_native_extensions();
	native_extension_manager->load_extensions();
	native_extension_manager->initialize_extensions(NativeExtension::INITIALIZATION_LEVEL_CORE);
	_is_core_extensions_registered = true;

	OS::get_singleton()->benchmark_end_measure("Core", "Register Extensions");
}

void unregister_core_extensions() {
	OS::get_singleton()->benchmark_begin_measure("Core", "Unregister Extensions");

	if (_is_core_extensions_registered) {
		native_extension_manager->deinitialize_extensions(NativeExtension::INITIALIZATION_LEVEL_CORE);
	}
	NativeExtension::finalize_native_extensions();

	OS::get_singleton()->benchmark_end_measure("Core", "Unregister Extensions");
}

void unregister_core_types() {
	OS::get_singleton()->benchmark_begin_measure("Core", "Unregister Types");

	// Destroy singletons in reverse order to ensure dependencies are not broken.

	memdelete(worker_thread_pool);

	memdelete(_engine_debugger);
	memdelete(_marshalls);
	memdelete(_class_registry);
	memdelete(_engine);
	memdelete(_os);
	memdelete(_resource_saver);
	memdelete(_resource_loader);

	memdelete(_geometry_3d);
	memdelete(_geometry_2d);

	memdelete(native_extension_manager);

	memdelete(resource_uid);

	memdelete(ip);

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Image)) {
		ResourceLoader::remove_resource_format_loader(resource_format_image);
		resource_format_image.unref();
	}

	ResourceSaver::remove_resource_format_saver(resource_saver_binary);
	resource_saver_binary.unref();

	ResourceLoader::remove_resource_format_loader(resource_loader_binary);
	resource_loader_binary.unref();

	ResourceLoader::remove_resource_format_loader(resource_format_importer);
	resource_format_importer.unref();

	ResourceSaver::remove_resource_format_saver(resource_format_importer_saver);
	resource_format_importer_saver.unref();

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Translation)) {
		ResourceLoader::remove_resource_format_loader(resource_format_po);
		resource_format_po.unref();
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Crypto)) {
		ResourceSaver::remove_resource_format_saver(resource_format_saver_crypto);
		resource_format_saver_crypto.unref();

		ResourceLoader::remove_resource_format_loader(resource_format_loader_crypto);
		resource_format_loader_crypto.unref();
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(JSON)) {
		ResourceSaver::remove_resource_format_saver(resource_saver_json);
		resource_saver_json.unref();

		ResourceLoader::remove_resource_format_loader(resource_loader_json);
		resource_loader_json.unref();
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(NativeExtension)) {
		ResourceLoader::remove_resource_format_loader(resource_loader_native_extension);
		resource_loader_native_extension.unref();
	}

	ResourceLoader::finalize();

	ClassRegistry::cleanup_defaults();
	memdelete(_time);
	ObjectRegistry::cleanup();

	CryptoCore::finalize();

	Variant::unregister_types();

	unregister_global_constants();

	ResourceCache::clear();
	ClassRegistry::cleanup();
	CoreStringNames::free();
	StringName::cleanup();

	OS::get_singleton()->benchmark_end_measure("Core", "Unregister Types");
}
