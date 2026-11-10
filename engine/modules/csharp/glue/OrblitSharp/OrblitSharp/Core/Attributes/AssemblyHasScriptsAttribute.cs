using System;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace Orblit
{
    /// <summary>
    /// Attribute that determines that the assembly contains Orblit scripts and, optionally, the
    /// collection of types that implement scripts; otherwise, retrieving the types requires lookup.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class AssemblyHasScriptsAttribute : Attribute
    {
        /// <summary>
        /// If the Orblit scripts contained in the assembly require lookup
        /// and can't rely on <see cref="ScriptTypes"/>.
        /// </summary>
        [MemberNotNullWhen(false, nameof(ScriptTypes))]
        public bool RequiresLookup { get; }

        /// <summary>
        /// The collection of types that implement a Orblit script.
        /// </summary>
        public Type[]? ScriptTypes { get; }

        /// <summary>
        /// Constructs a new AssemblyHasScriptsAttribute instance
        /// that requires lookup to get the Orblit scripts.
        /// </summary>
        public AssemblyHasScriptsAttribute()
        {
            RequiresLookup = true;
            ScriptTypes = null;
        }

        /// <summary>
        /// Constructs a new AssemblyHasScriptsAttribute instance
        /// that includes the Orblit script types and requires no lookup.
        /// </summary>
        /// <param name="scriptTypes">The collection of types that implement a Orblit script.</param>
        public AssemblyHasScriptsAttribute(Type[] scriptTypes)
        {
            RequiresLookup = false;
            ScriptTypes = scriptTypes;
        }
    }
}

#nullable restore
