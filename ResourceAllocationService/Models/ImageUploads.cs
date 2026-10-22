using System;
using System.IO;
using System.Linq;
using System.Web;
namespace ResourceAllocationService.Models
{
    public static class ImageUploads
    {
        public static bool Valid(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return true;
            if (file.ContentLength > 5 * 1024 * 1024 || !file.InputStream.CanSeek) return false;
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var bytes = new byte[8];
            var position = file.InputStream.Position;
            var count = file.InputStream.Read(bytes, 0, bytes.Length);
            file.InputStream.Position = position;
            return (new[] { ".jpg", ".jpeg" }.Contains(extension) && count >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255)
                || (extension == ".png" && count == 8 && bytes.SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
                || (extension == ".gif" && count >= 6 && bytes[0] == 71 && bytes[1] == 73 && bytes[2] == 70 && bytes[3] == 56 && (bytes[4] == 55 || bytes[4] == 57) && bytes[5] == 97);
        }
        public static string Save(HttpPostedFileBase file, string directory)
        {
            if (file == null || file.ContentLength == 0) return null;
            if (!Valid(file)) throw new InvalidOperationException("Choose a JPG, PNG or GIF image under 5 MB.");
            Directory.CreateDirectory(directory);
            var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
            file.SaveAs(Path.Combine(directory, name));
            return "~/Uploads/" + name;
        }
    }
}
