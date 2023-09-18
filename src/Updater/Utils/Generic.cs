using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

namespace Updater.Utils
{
    public static class Generic
    {
        public static void GetVersion(string exec_path, out string name, out string version, out string file_version)
        {
            string exePath = exec_path;
            name = string.Empty;
            version = string.Empty;
            file_version = string.Empty;

            try
            {
                FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(exePath);

                name = fileVersionInfo.ProductName ?? "can't read";
                version = fileVersionInfo.ProductVersion ?? "can't read";
                file_version = fileVersionInfo.FileVersion ?? "can't read";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        
        public static int CountSubstringOccurrences(string input, string substring)
        {
            return input.Count(i => substring.Contains(i));
        }

        public static List<string> GetProjectList(string sln_path)
        {
            List<string> project_list = new List<string>();
            var psi = new ProcessStartInfo("dotnet", $"sln {sln_path} list") { RedirectStandardOutput = true };
            psi.CreateNoWindow = true;
            
            var proc = Process.Start(psi);
            if (proc == null)
            { 
                Debug.WriteLine("Can not exec.");
            }
            else
            {
                Debug.WriteLine("-------------Start read standard output--------------");
                using (var sr = new StreamReader(proc.StandardOutput.BaseStream, Encoding.UTF8))
                {
                    while (!sr.EndOfStream)
                    {
                        string tmp = sr.ReadLine() ?? "";
                        if (string.IsNullOrWhiteSpace(tmp)) continue;
                        if (tmp.Contains(".csproj")) project_list.Add(tmp);
                    }

                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
            }

            return project_list;
        }

        public static string? GetSdkVersionFromProjectFile(string projectFilePath)
        {
            if (File.Exists(projectFilePath))
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(projectFilePath);

                XmlNode? sdkNode = xmlDoc.SelectSingleNode("//Project/PropertyGroup/TargetFramework");
                if (sdkNode != null)
                {
                    return sdkNode.InnerText;
                }
            }
            return null;
        }
    }
}
