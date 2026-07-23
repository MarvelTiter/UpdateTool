using AutoInjectGenerator;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UpdateTool.Extensions;

namespace UpdateTool.ViewModels;

internal record TabItemInfo(string Header, object View);

[AutoInjectSelf]
internal partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<TabItemInfo> Tabs { get; }

    [ObservableProperty]
    public partial TabItemInfo? Current { get; set; }

    public MainWindowViewModel(IServiceProvider services)
    {
        var update = services.CreateView<UpdateView, UpdateViewModel>();
        var jsonEdit = services.CreateView<JsonEditView, JsonEditViewModel>();
        Tabs = [
            new("程序更新", update),
            new("配置编辑", jsonEdit),
        ];
    }
}
