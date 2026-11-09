using System;

namespace Orblit
{
    public partial class Prefab
    {
        /// <summary>
        /// Instantiates the scene's node hierarchy, erroring on failure.
        /// Triggers child scene instantiation(s). Triggers a
        /// <see cref="Entity.NotificationSceneInstantiated"/> notification on the root node.
        /// </summary>
        /// <seealso cref="InstantiateOrNull{T}(GenEditState)"/>
        /// <exception cref="InvalidCastException">
        /// The instantiated node can't be casted to the given type <typeparamref name="T"/>.
        /// </exception>
        /// <typeparam name="T">The type to cast to. Should be a descendant of <see cref="Entity"/>.</typeparam>
        /// <returns>The instantiated scene.</returns>
        public T Instantiate<T>(Prefab.GenEditState editState = (Prefab.GenEditState)0) where T : class
        {
            return (T)(object)Instantiate(editState);
        }

        /// <summary>
        /// Instantiates the scene's node hierarchy, returning <see langword="null"/> on failure.
        /// Triggers child scene instantiation(s). Triggers a
        /// <see cref="Entity.NotificationSceneInstantiated"/> notification on the root node.
        /// </summary>
        /// <seealso cref="Instantiate{T}(GenEditState)"/>
        /// <typeparam name="T">The type to cast to. Should be a descendant of <see cref="Entity"/>.</typeparam>
        /// <returns>The instantiated scene.</returns>
        public T InstantiateOrNull<T>(Prefab.GenEditState editState = (Prefab.GenEditState)0) where T : class
        {
            return Instantiate(editState) as T;
        }
    }
}
