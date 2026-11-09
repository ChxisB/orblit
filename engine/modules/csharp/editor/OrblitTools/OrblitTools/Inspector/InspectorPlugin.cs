using System;
using System.Collections.Generic;
using Orblit;
using OrblitTools.Build;
using OrblitTools.Utils;

namespace OrblitTools.Inspector
{
    public partial class InspectorPlugin : EditorInspectorPlugin
    {
        public override bool _CanHandle(OrblitObject orblitObject)
        {
            if (orblitObject == null)
            {
                return false;
            }

            foreach (var script in EnumerateScripts(orblitObject))
            {
                if (script is CSharpScript)
                {
                    return true;
                }
            }
            return false;
        }

        public override void _ParseBegin(OrblitObject orblitObject)
        {
            foreach (var script in EnumerateScripts(orblitObject))
            {
                if (script is not CSharpScript)
                    continue;

                string scriptPath = script.ResourcePath;

                if (string.IsNullOrEmpty(scriptPath))
                {
                    // Generic types used empty paths in older versions of Orblit
                    // so we assume your project is out of sync.
                    AddCustomControl(new InspectorOutOfSyncWarning());
                    break;
                }

                if (scriptPath.StartsWith("csharp://"))
                {
                    // This is a virtual path used by generic types, extract the real path.
                    var scriptPathSpan = scriptPath.AsSpan("csharp://".Length);
                    scriptPathSpan = scriptPathSpan[..scriptPathSpan.IndexOf(':')];
                    scriptPath = $"res://{scriptPathSpan}";
                }

                if (File.GetLastWriteTime(scriptPath) > BuildManager.LastValidBuildDateTime)
                {
                    AddCustomControl(new InspectorOutOfSyncWarning());
                    break;
                }
            }
        }

        private static IEnumerable<Script> EnumerateScripts(OrblitObject orblitObject)
        {
            var script = orblitObject.GetScript().As<Script>();
            while (script != null)
            {
                yield return script;
                script = script.GetBaseScript();
            }
        }
    }
}
