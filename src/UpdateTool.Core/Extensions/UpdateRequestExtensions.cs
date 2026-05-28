using UpdateTool.Core.Models;

namespace UpdateTool.Core.Extensions;

public static class UpdateRequestExtensions
{

    public static async Task<string> ExtractFilesAsync(this UpdateRequest request)
    {
        var tempPath = Path.Combine(request.ApplicationPath, $"temp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);

        if (request.IsPackage)
        {
            var archive = await SharpCompress.Archives.ArchiveFactory.OpenAsyncArchive(request.UpdatePackage);
            await SharpCompress.Archives.IAsyncArchiveExtensions.WriteToDirectoryAsync(archive, tempPath);
        }
        else
        {
            var filefullName = $"{request.FileName}{request.FileExt}";
            var distFile = Path.Combine(tempPath, filefullName);
            File.Move(request.UpdatePackage, distFile);
        }
        return tempPath;
    }
}
