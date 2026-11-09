using System;

namespace Orblit
{
    /// <summary>
    /// An attribute that excludes a member from registering as a method or property in Orblit.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class IgnoreMemberAttribute : Attribute { }
}
