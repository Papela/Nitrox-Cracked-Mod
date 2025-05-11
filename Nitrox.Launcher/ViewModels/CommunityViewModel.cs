using CommunityToolkit.Mvvm.Input;
using Nitrox.Launcher.Models.Utils;
using Nitrox.Launcher.ViewModels.Abstract;

namespace Nitrox.Launcher.ViewModels;

public partial class CommunityViewModel : RoutableViewModelBase
{

    [RelayCommand]
    private void GithubLink()
    {
        ProcessUtils.OpenUrl("github.com/Papela/Nitrox-Cracked-Mod");
    }
}
