using System.Diagnostics.CodeAnalysis;

namespace UpdateTool.Core;

public class App
{
    public static void Init(IServiceProvider services)
    {
        Services = services;
    }

    [NotNull] 
    public static IServiceProvider? Services { get; private set; }

}
