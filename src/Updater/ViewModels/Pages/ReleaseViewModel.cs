
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using LibGit2Sharp;

using Microsoft.Win32;
using Updater.Utils;
using Updater.Views.Pages;

namespace Updater.ViewModels.Pages
{
    public partial class ReleaseViewModel : ObservableObject
    {
        public ReleaseViewModel()
        {
            
        }

        /// <summary>
        /// 项目的路径
        /// </summary>
        [ObservableProperty] private string _projectUrl = "暂无项目";

        /// <summary>
        /// 项目的分支
        /// </summary>
        [ObservableProperty] private string _projectBranch = "暂无项目";

        /// <summary>
        /// 项目的分支标识
        /// </summary>
        [ObservableProperty] private string _projectBranchHash = "暂无项目";

        [ObservableProperty] private string _csprojVersion = string.Empty;

        /// <summary>
        /// 控制台输出信息
        /// </summary>
        [ObservableProperty] private string _outPut = string.Empty;

        /// <summary>
        /// 是否可以编译
        /// </summary>
        [ObservableProperty] private bool _canCompile = true;

        [ObservableProperty] private bool _canPackage = true;

        /// <summary>
        /// 编译配置 - debug or release
        /// </summary>
        [ObservableProperty] private ComboBoxItem? _compildConfig;

        /// <summary>
        /// 打包项目
        /// </summary>
        [ObservableProperty] private ComboBoxItem? _compildProject;

        partial void OnCompildProjectChanged(ComboBoxItem? value)
        {
            var sln_base_dir = Path.GetDirectoryName(ProjectUrl);
            var csproj_path = Path.Combine(sln_base_dir!, value!.Content.ToString() ?? "");
            var csproj_dir = Path.GetDirectoryName(csproj_path);

            var Properties_dir = Path.Combine(csproj_dir!, "Properties");
            if (Directory.Exists(Properties_dir))
            {
                var AssemblyInfo_path = Path.Combine(Properties_dir, "AssemblyInfo.cs");
                string assemblyInfoContent = File.ReadAllText(AssemblyInfo_path);

                // Use a regular expression to extract the AssemblyVersion
                string pattern = @"\[assembly: AssemblyVersion\(""(\d+\.\d+\.\d+\.\d+)""\)\]";
                Match match = Regex.Match(assemblyInfoContent, pattern);

                if (match.Success)
                {
                    string versionString = match.Groups[1].Value;
                    System.Version version = new System.Version(versionString);
                    CsprojVersion = version.ToString();
                    Debug.WriteLine("Assembly Version: " + version);
                }
                else
                {
                    CsprojVersion = "not found";
                    Debug.WriteLine("Assembly version not found in AssemblyInfo.cs");
                }
            }
        }

        [RelayCommand]
        private void OnOpenFolderClick()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "选择项目";
            ofd.InitialDirectory = @"C:\";
            ofd.Filter = "vs项目|*.sln";

            bool? result = ofd.ShowDialog() ?? false;
            if (result is false) return;
            Debug.WriteLine($"打开项目:{ofd.FileName}");
            
            string selectedFileName = ofd.FileName;
            ProjectUrl = selectedFileName;

            // 设置Git存储库的路径
            string repositoryPath = Path.GetDirectoryName(ProjectUrl) ?? "";


            // 打开Git存储库
            if (!string.IsNullOrWhiteSpace(repositoryPath))
            {
                using (var repo = new Repository(repositoryPath))
                {
                    // 获取当前分支名称
                    string currentBranchName = repo.Head.FriendlyName;
                    ProjectBranch = currentBranchName;

                    // 获取最近一次提交信息
                    var lastCommit = repo.Head.Tip;

                    Console.WriteLine("Git Repository Information:");
                    Console.WriteLine("Current Branch: " + currentBranchName);
                    Console.WriteLine("Last Commit:");
                    Console.WriteLine("  Author: " + lastCommit.Author.Name);
                    Console.WriteLine("  Message: " + lastCommit.Message);
                    Console.WriteLine("  Date: " + lastCommit.Author.When);

                    ProjectBranchHash = $"{lastCommit.Sha} --- {lastCommit.Author.When}";
                }
            }
            Debug.WriteLine($"获取git信息:{ProjectBranch} | {ProjectBranchHash}");

            var project_list = Generic.GetProjectList(ProjectUrl);
            var view = App.GetService<ReleasePage>();
            view.CompildProjectComboBox.Items.Clear();
            foreach(var project in project_list)
            {
                view.CompildProjectComboBox.Items.Add(new ComboBoxItem() { Content = project });
            }
        }

        [RelayCommand]
        private async Task OnCompilationClick()
        {
            CanCompile = false;
            CanPackage = false;

            await Task.Run(async () =>
            {
                await Build();
            });

            CanCompile = true;
            CanPackage = true;
        }

        [RelayCommand]
        private async Task OnPackageClick()
        {
            if (CompildProject is null || CompildConfig is null)
            {
                OutPut += "配置项未选，无法启用打包" + Environment.NewLine;
                return;
            }

            CanCompile = false;
            CanPackage = false;

            await Task.Run(async () =>
            {
                await Build();
            });

            var base_path = Path.GetDirectoryName(ProjectUrl) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(base_path)) { OutPut += "程序发生异常:无法获取解决方案所在的路径" + Environment.NewLine; }

            var pack_project = Path.Combine(base_path, CompildProject.Content.ToString() ?? string.Empty);
            var project_name = Path.GetFileName(pack_project).Replace(".csproj", "") ?? "output";
            var pack_project_base = Path.GetDirectoryName(pack_project) ?? string.Empty;

            var sdk_version = Generic.GetSdkVersionFromProjectFile(pack_project);
            if (string.IsNullOrEmpty(sdk_version))
            {
                OutPut += "编译失败:无法查找所需的sdk" + Environment.NewLine;
                CanCompile = true;
                CanPackage = true;
                return;
            }

            var output_dir = Path.Combine(pack_project_base, "bin", CompildConfig.Content.ToString() ?? "Release", sdk_version);
            if (Directory.Exists(output_dir))
            {
                string zipFilePath = $"./output/{project_name}.zip"; // 压缩包输出路径
                if (!Directory.Exists("./output")) Directory.CreateDirectory("./output");
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

                try
                {
                    await Task.Run(() =>
                    {
                        ZipFile.CreateFromDirectory(output_dir, zipFilePath);
                    });

                    OutPut += Environment.NewLine + "------------------------------" + Environment.NewLine;
                    OutPut += $"文件夹 {output_dir} 已成功压缩到 {zipFilePath}." + Environment.NewLine;
                    OutPut += "------------------------------" + Environment.NewLine;
                    Debug.WriteLine($"文件夹 {output_dir} 已成功压缩到 {zipFilePath}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"压缩文件夹时出现错误: {ex.Message}");
                }
            }

            CanCompile = true;
            CanPackage = true;
        }

        #region Func

        public Task Build()
        {
            if (ProjectUrl is "暂无项目" || CompildConfig is null)
            {
                int cout = Generic.CountSubstringOccurrences(OutPut, "\n");
                if (cout > 10) OutPut = OutPut[OutPut.IndexOf("\n")..];

                OutPut += "暂无项目或者未选择编译配置，编译失败" + Environment.NewLine;
                return Task.CompletedTask;
            }

            string arg_cmd = string.Empty; 

            App.Current.Dispatcher.Invoke(() => arg_cmd = $"build {ProjectUrl} -c {CompildConfig.Content}");

            var view = App.GetService<ReleasePage>();
            var psi = new ProcessStartInfo("dotnet", arg_cmd) { RedirectStandardOutput = true };
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
                        var tmp = sr.ReadLine();
                        Debug.WriteLine(tmp);

                        int cout = Generic.CountSubstringOccurrences(OutPut, "\n");
                        int index = OutPut.IndexOf("\n") + 1;
                        if (cout > 60) OutPut = OutPut[index..];

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            view.OutPutScrollViewer.ScrollToBottom();
                            OutPut += $"{tmp}{Environment.NewLine}";
                        });

                    }

                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
                var time = $"Total execute time :{(proc.ExitTime - proc.StartTime).TotalMilliseconds} ms";
                var code = $"Exited Code ： {proc.ExitCode}";
                App.Current.Dispatcher.Invoke(() =>
                {
                    OutPut += $"{time}{Environment.NewLine}";
                    OutPut += $"{code}{Environment.NewLine}";
                });
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}
