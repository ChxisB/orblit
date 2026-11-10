using Orblit;
using System;
using OrblitTools.ProjectEditor;

namespace OrblitTools
{
    public static class CsProjOperations
    {
        public static string GenerateGameProject(string dir, string name)
        {
            try
            {
                return ProjectGenerator.GenAndSaveGameProject(dir, name);
            }
            catch (Exception e)
            {
                Orb.PushError(e.ToString());
                return string.Empty;
            }
        }
    }
}
