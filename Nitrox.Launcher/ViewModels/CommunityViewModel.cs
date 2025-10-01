using CommunityToolkit.Mvvm.Input;
using Nitrox.Launcher.ViewModels.Abstract;

namespace Nitrox.Launcher.ViewModels;

internal partial class CommunityViewModel : RoutableViewModelBase
{
    [RelayCommand]
    private void GithubLink()
    {
        OpenUri("github.com/Papela/Nitrox-Cracked-Mod");
    }
}
