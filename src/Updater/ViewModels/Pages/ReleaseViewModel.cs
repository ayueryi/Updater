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
    }
}
