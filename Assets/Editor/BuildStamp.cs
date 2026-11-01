using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Before every player build, writes the version, git branch, commit and build time to
// Assets/BuildStamp/Resources/BuildStamp.txt, which BuildStampLabel shows in the game's corner.
// The file is deleted again after the build (and is git-ignored in case a build fails midway).
public class BuildStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string Folder = "Assets/BuildStamp/Resources";
    private const string FilePath = Folder + "/BuildStamp.txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string branch = Git("rev-parse --abbrev-ref HEAD");
        string commit = Git("rev-parse --short HEAD");
        bool dirty = Git("status --porcelain --untracked-files=no -- Assets/Scripts Assets/Scenes Packages").Length > 0;

        string stamp = string.Format("v{0} · {1} · {2}{3} · {4:dd MMM HH:mm}",
            Application.version, branch, commit, dirty ? "+changes" : "", DateTime.Now);

        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, stamp);
        AssetDatabase.ImportAsset(FilePath, ImportAssetOptions.ForceSynchronousImport);
        UnityEngine.Debug.Log("[BuildStamp] " + stamp);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        AssetDatabase.DeleteAsset(FilePath);
    }

    private static string Git(string arguments)
    {
        try
        {
            var info = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return output;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[BuildStamp] git not available: " + e.Message);
            return "?";
        }
    }
}
