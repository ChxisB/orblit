def can_build(env, platform):
    # Having this on release by default, it's small and a lot of users like to do procedural stuff
    return not env["disable_spatial"]


def configure(env):
    pass
