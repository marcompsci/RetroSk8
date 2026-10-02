#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace RetroSk8.EditorTools
{
    /// <summary>Links the system frameworks the native plugins need into the exported Xcode project.</summary>
    public static class RetroSk8IOSPostBuild
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            // Plugins compile into the UnityFramework target, so that is where ReplayKit must be linked.
            string framework = project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(framework, "ReplayKit.framework", false);
            project.WriteToFile(projectPath);
        }
    }
}
#endif
