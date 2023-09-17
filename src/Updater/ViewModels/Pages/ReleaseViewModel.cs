
using System.Diagnostics;
using System.IO;
using System.Text;

using LibGit2Sharp;

using Microsoft.Win32;

using Updater.Views.Pages;

namespace Updater.ViewModels.Pages
{
    public partial class ReleaseViewModel : ObservableObject
    {
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

        [ObservableProperty] private string _outPut = string.Empty;

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
        }

        private readonly object lockObject = new object();

        [RelayCommand]
        private void OnCompilationClick()
        {
            Task.Run(() =>
            {
                lock (lockObject)
                {
                    var view = App.GetService<ReleasePage>();
                    var psi = new ProcessStartInfo("dotnet", $"build {ProjectUrl}") { RedirectStandardOutput = true };
                    psi.CreateNoWindow = true;

                    //启动
                    var proc = Process.Start(psi);
                    if (proc == null)
                    { 
                        Debug.WriteLine("Can not exec.");
                    }
                    else
                    {
                        Console.WriteLine("-------------Start read standard output--------------");
                        //开始读取
                        using (var sr = new StreamReader(proc.StandardOutput.BaseStream, Encoding.UTF8))
                        {
                            while (!sr.EndOfStream)
                            {
                                var tmp = sr.ReadLine();
                                Debug.WriteLine(tmp);
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    OutPut += $"{tmp}{Environment.NewLine}";
                                    view.OutPutScrollViewer.ScrollToBottom();
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
                }
            });
        }
    }
}
