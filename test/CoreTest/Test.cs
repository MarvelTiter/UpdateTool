using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpdateTool.Core.Services;

namespace CoreTest
{
    [TestClass]
    public sealed class Test
    {
        [TestMethod]
        public void TestJsonUpdate()
        {
            var sc = new ServiceCollection();
            sc.AddLogging(lb =>
            {

            });
            sc.AddScoped<FileUpdateService>();
            var service = sc.BuildServiceProvider();
            var fus = service.GetRequiredService<FileUpdateService>();
            var newPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "new");
            var oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "old");
            fus.UpdateSettingFile(newPath, oldPath, "appsettings.json");
        }
    }
}
