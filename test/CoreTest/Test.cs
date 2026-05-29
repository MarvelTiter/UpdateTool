using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpdateTool.Core.Services;

namespace CoreTest
{
    [TestClass]
    public sealed class Test
    {
        [TestMethod]
        public async Task TestJsonUpdate()
        {
            var sc = new ServiceCollection();
            sc.AddLogging();
            sc.AddScoped<FileUpdateService>();
            var service = sc.BuildServiceProvider();
            var fus = service.GetRequiredService<FileUpdateService>();
            var newPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "new");
            var oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "old");
            await fus.UpdateSettingFile(newPath, oldPath, "appsettings.json");
        }

        [TestMethod]
        public async Task ProcessStopTest()
        {
            var sc = new ServiceCollection();
            sc.AddLogging();
            sc.AddScoped<ProcessService>();
            var service = sc.BuildServiceProvider();
            var ps = service.GetRequiredService<ProcessService>();
            await ps.StopProcessAsync("RisCollector");
        }
    }
}
