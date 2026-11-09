def can_build(env, platform):
    return True


def configure(env):
    # Check if the platform has marked mono as supported.
    supported = env.get("supported", [])
    if "csharp" not in supported:
        import sys

        print("The 'csharp' module does not currently support building for this platform. Aborting.")
        sys.exit(255)

    env.add_module_version_string("csharp")


def get_doc_classes():
    return [
        "CSharpScript",
        "OrblitSharp",
    ]


def get_doc_path():
    return "doc_classes"


def is_enabled():
    # The module is disabled by default. Use module_csharp_enabled=yes to enable it.
    return False
