using SADA.Services.Helpers.FilesHnadler;

namespace SADA.Services.Helpers.FilesHnadler;
public class ImageHandler : BaseHandler
{
    private static string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".svg" };
    private const long _maxAllowedSize = 1048576*3; //one megebyte
    private const long _megabyte = 2048 * 2048;
    public ImageHandler()
    {
        allowedExtensions = _allowedExtensions;
        maxAllowedSize = _maxAllowedSize;
        megabyte = _megabyte;
    }
}
