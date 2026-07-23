using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateTool.Extensions;

public static class ViewHelper
{
    public static object CreateView<TView, TViewModel>(this IServiceProvider services)
        where TView : StyledElement, new()
        where TViewModel : notnull
    {
        var vm = services.GetRequiredService<TViewModel>();
        return new TView() { DataContext = vm };
    }
}
