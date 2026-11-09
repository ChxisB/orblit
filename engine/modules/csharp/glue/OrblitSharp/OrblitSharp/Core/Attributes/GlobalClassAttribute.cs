using System;

#nullable enable

namespace Orblit
{
    /// <summary>
    /// Exposes the target class as a global script class to Orblit Engine.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GlobalClassAttribute : Attribute { }
}
