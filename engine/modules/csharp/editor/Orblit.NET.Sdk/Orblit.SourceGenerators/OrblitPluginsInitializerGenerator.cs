using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Orblit.SourceGenerators
{
    [Generator]
    public class OrblitPluginsInitializerGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.IsOrblitToolsProject() || context.IsOrblitSourceGeneratorDisabled("OrblitPluginsInitializer"))
                return;

            string source =
                @"using System;
using System.Runtime.InteropServices;
using Orblit.Bridge;
using Orblit.NativeInterop;

namespace OrblitPlugins.Game
{
    internal static partial class Main
    {
        [UnmanagedCallersOnly(EntryPoint = ""orblitsharp_game_main_init"")]
        private static orblit_bool InitializeFromGameProject(IntPtr orblitDllHandle, IntPtr outManagedCallbacks,
            IntPtr unmanagedCallbacks, int unmanagedCallbacksSize)
        {
            try
            {
                NativeFuncs.Initialize(unmanagedCallbacks, unmanagedCallbacksSize);

                Marshaling.Initialize();

                DllImportResolver dllImportResolver = new OrblitDllImportResolver(orblitDllHandle).OnResolveDllImport;

                var coreApiAssembly = typeof(global::Orblit.OrblitObject).Assembly;

                NativeLibrary.SetDllImportResolver(coreApiAssembly, dllImportResolver);

                ManagedCallbacks.Create(outManagedCallbacks);

                ScriptManagerBridge.LookupScriptsInAssembly(typeof(global::OrblitPlugins.Game.Main).Assembly);

                return orblit_bool.True;
            }
            catch (Exception e)
            {
                global::System.Console.Error.WriteLine(e);
                return false.ToOrblitBool();
            }
        }
    }
}
";

            context.AddSource("OrblitPlugins.Game.generated",
                SourceText.From(source, Encoding.UTF8));
        }
    }
}
