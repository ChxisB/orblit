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
        /// The instantiated node can't be cast to the given type <typeparamref name="T"/>.
        /// </exception>
        /// <typeparam name="T">The type to cast to. Should be a descendant of <see cref="Entity"/>.</typeparam>
        /// <returns>The instantiated scene.</returns>
        public T Instantiate<T>(Prefab.GenEditState editState = (Prefab.GenEditState)0) where T : class
        {
            Entity instance = Instantiate(editState);
            if (instance is null || instance is T)
            {
                return (T)(object)instance;
            }

            // The instance has no owner yet, so a failed cast would leave it in memory.
            Type instanceType = instance.GetType();
            instance.Free();
            throw new InvalidCastException($"The root node of the Prefab is of type '{instanceType}' and can't be cast to '{typeof(T)}'.");
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
            Entity instance = Instantiate(editState);
            if (instance is T typedInstance)
            {
                return typedInstance;
            }

            // The instance has no owner yet, so a failed cast would leave it in memory.
            instance?.Free();
            return null;
        }
    }
}
