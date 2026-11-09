def can_build(env, platform):
    return not env["disable_planar"]


def configure(env):
    pass


def get_doc_classes():
    return [
        "TileMapLayer",
        "TileData",
        "TileMapPattern",
        "TileSet",
        "TileSetAtlasSource",
        "TileSetScenesCollectionSource",
        "TileSetSource",
    ]


def get_doc_path():
    return "doc_classes"
