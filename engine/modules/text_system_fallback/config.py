def can_build(env, platform):
    env.module_add_dependencies("text_system_fallback", ["freetype", "msdfgen", "svg"], True)
    return True


def configure(env):
    pass


def is_enabled():
    # The module is disabled by default. Use module_text_system_fallback_enabled=yes to enable it.
    return False


def get_doc_classes():
    return [
        "TextSystemFallback",
    ]


def get_doc_path():
    return "doc_classes"
