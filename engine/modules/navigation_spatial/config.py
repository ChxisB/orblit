def can_build(env, platform):
    if env["disable_navigation_spatial"]:
        return False

    env.module_add_dependencies("navigation", ["csg", "gridmap"], True)
    return True


def configure(env):
    pass
