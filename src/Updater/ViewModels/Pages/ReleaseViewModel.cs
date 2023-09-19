
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Xml;

using LibGit2Sharp;

using Newtonsoft.Json;

using Updater.Utils;
using Updater.Views.Pages;

using Wpf.Ui;
using Wpf.Ui.Controls;



namespace Updater.ViewModels.Pages
{
    public partial class ReleaseViewModel : ObservableObject
    {
        private readonly ISnackbarService _snackbarService;

        public ReleaseViewModel(ISnackbarService snackbarService)
        {
            _snackbarService = snackbarService;
        }

        /// <summary>s
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

        /// <summary>
        /// 已选打包项目的版本
        /// </summary>
        [ObservableProperty] private string _csprojVersion = string.Empty;

        /// <summary>
        /// 控制台输出信息
        /// </summary>
        [ObservableProperty] private string _outPut = string.Empty;

        /// <summary>
        /// 禁用操作
        /// </summary>
        [ObservableProperty] private bool _canEdit = true;

        /// <summary>
        /// 编译配置 - debug or release
        /// </summary>
        [ObservableProperty] private ComboBoxItem? _compildConfig;

        /// <summary>
        /// 打包项目
        /// </summary>
        [ObservableProperty] private ComboBoxItem? _compildProject;

        /// <summary>
        /// 打包地址
        /// </summary>
        [ObservableProperty] private string _packPath = string.Empty;

        /// <summary>
        /// 远程更新配置地址
        /// </summary>
        [ObservableProperty] private string _packConfigPath = string.Empty;

        /// <summary>
        /// 远程更新地址已有的版本
        /// </summary>
        [ObservableProperty] private string _packConfigVersion = string.Empty;

        #region Private Value

        string AssemblyInfoPath = string.Empty;
        string? oldVersion = string.Empty;

        #endregion

        #region Auto ValueFun

        partial void OnCsprojVersionChanged(string? oldValue, string newValue)
        {
            oldVersion = oldValue;
        }

        partial void OnCompildProjectChanged(ComboBoxItem? value)
        {
            var sln_base_dir = Path.GetDirectoryName(ProjectUrl);
            var csproj_path = Path.Combine(sln_base_dir!, value!.Content.ToString() ?? "");
            var csproj_dir = Path.GetDirectoryName(csproj_path);

            var Properties_dir = Path.Combine(csproj_dir!, "Properties");
            if (Directory.Exists(Properties_dir))
            {
                AssemblyInfoPath = Path.Combine(Properties_dir, "AssemblyInfo.cs");
                string assemblyInfoContent = File.ReadAllText(AssemblyInfoPath);

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

        #endregion

        #region User Command

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
            foreach (var project in project_list)
            {
                view.CompildProjectComboBox.Items.Add(new ComboBoxItem() { Content = project });
            }

            view.CompildConfigComboBox.SelectedIndex = 0;
            view.CompildProjectComboBox.SelectedIndex = 0;

            SaveConfig();
        }

        [RelayCommand]
        private async Task OnCompilationClick()
        {
            CanEdit = false;

            await Task.Run(async () =>
            {
                await Build();
            });

            CanEdit = true;
        }

        [RelayCommand]
        private async Task OnPackageClick()
        {
            if (CompildProject is null || CompildConfig is null)
            {
                OutPut += "配置项未选，无法启用打包" + Environment.NewLine;
                return;
            }

            CanEdit = false;

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
                CanEdit = true;
                return;
            }

            var output_dir = Path.Combine(pack_project_base, "bin", CompildConfig.Content.ToString() ?? "Release", sdk_version);
            if (Directory.Exists(output_dir))
            {
                string zipFilePath = PackPath; // 压缩包输出路径
                var base_dir = Path.GetDirectoryName(PackPath);
                if (!Directory.Exists(base_dir)) Directory.CreateDirectory(base_dir!);
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

                    _snackbarService.Show(
                        "成功提示.",
                        $"自动构建已成功，软件已打包至{zipFilePath}.",
                        ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.Fluent24),
                        TimeSpan.FromSeconds(4));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"压缩文件夹时出现错误: {ex.Message}");
                }
            }

            CanEdit = true;
        }

        [RelayCommand]
        private void OnEditVersionClick()
        {
            if (string.IsNullOrWhiteSpace(AssemblyInfoPath) || string.IsNullOrWhiteSpace(oldVersion))
            {
                return;
            }

            string assemblyInfoContent = File.ReadAllText(AssemblyInfoPath);
            assemblyInfoContent = assemblyInfoContent.Replace(oldVersion, CsprojVersion);
            File.WriteAllText(AssemblyInfoPath, assemblyInfoContent);

            _snackbarService.Show(
            "成功提示.",
            $"打包软件已成功修改为{CsprojVersion}.",
            ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.Fluent24),
            TimeSpan.FromSeconds(1.5));

            if (!string.IsNullOrWhiteSpace(PackPath))
            {
                PackPath = PackPath.Replace(oldVersion, CsprojVersion);
            }
        }

        [RelayCommand]
        private async Task OnReleaseSoftwareClick()
        {
            if (string.IsNullOrWhiteSpace(ProjectUrl) || string.IsNullOrWhiteSpace(PackConfigPath)) return;

            CanEdit = false;

            await OnPackageClick();

            if (File.Exists(PackConfigPath))
            {
                var content = File.ReadAllText(PackConfigPath);

                // 创建XmlDocument对象并加载XML字符串
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(content);

                // 选择version元素并获取其文本值
                XmlNode? versionNode = xmlDoc.SelectSingleNode("/item/version");
                if (versionNode is null)
                {
                    _snackbarService.Show(
                    "失败提示.",
                    $"软件发布失败，配置文件似乎损坏了.",
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.Fluent24),
                    TimeSpan.FromSeconds(2.5));
                    return;
                }
                var version = versionNode.InnerText;
                content = content.Replace(version, CsprojVersion);
                File.WriteAllText(PackConfigPath, content);

                _snackbarService.Show(
                    "成功提示.",
                    $"软件发布成功.",
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.Fluent24),
                    TimeSpan.FromSeconds(2.5));
            }
            FlushPackVersion();
            CanEdit = true;
        }

        #endregion

        #region Func

        public void FlushPackVersion()
        {
            if (string.IsNullOrWhiteSpace(PackConfigPath)) return;
            var content = File.ReadAllText(PackConfigPath);
            Generic.GetXmlVersion(content, out string version);
            PackConfigVersion = version;
        }

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

        public void SaveConfig()
        {
            Dictionary<string, object?>? propertyDictionary = new Dictionary<string, object?>();

            Type type = this.GetType();

            PropertyInfo[] properties = type.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                string propertyName = property.Name;
                object? propertyValue = property.GetValue(this);
                if (propertyValue is string)
                {

                    propertyDictionary.Add(propertyName, propertyValue);
                }
            }

            var str = JsonConvert.SerializeObject(propertyDictionary);
            if (!Directory.Exists("./config")) Directory.CreateDirectory("./config");
            File.WriteAllText("./config/releaseConfig.json", str);
        }

        public void LoadConfig()
        {
            if (File.Exists("./config/releaseConfig.json"))
            {
                var content = File.ReadAllText("./config/releaseConfig.json");
                Dictionary<string, object?>? propertyDictionary = JsonConvert.DeserializeObject<Dictionary<string, object?>>(content);
                if (propertyDictionary is null) return;

                Type personType = typeof(ReleaseViewModel);

                foreach (var kvp in propertyDictionary)
                {
                    string propertyName = kvp.Key;
                    object? propertyValue = kvp.Value;

                    // 使用反射获取属性信息
                    var propertyInfo = personType.GetProperty(propertyName);

                    if (propertyInfo != null && propertyInfo.CanWrite)
                    {
                        // 将字典中的值赋给属性
                        propertyInfo.SetValue(this, propertyValue);
                    }
                }
            }
        }

        #endregion
    }
}
