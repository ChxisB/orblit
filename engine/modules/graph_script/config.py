def can_build(env, platform):
    return True


def configure(env):
    pass


def get_doc_classes():
    return [
        "DialogueBox",
        "GraphCall",
        "GraphScript",
    ]


def get_doc_path():
    return "doc_classes"
