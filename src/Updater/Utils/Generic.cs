using System.Diagnostics;

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
    }
}
