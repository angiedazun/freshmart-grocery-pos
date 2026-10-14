using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace GroceryPOS.UI.Controls
{
    /// <summary>Loads product photos from the Images folder (no file locks) and caches thumbnails.</summary>
    public static class ImageCache
    {
        static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        const int MaxDim = 360;

        public static string Folder
        {
            get
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static Image Get(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            Image img;
            if (cache.TryGetValue(fileName, out img)) return img;
            try
            {
                string path = Path.IsPathRooted(fileName) ? fileName : Path.Combine(Folder, fileName);
                if (File.Exists(path))
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var src = Image.FromStream(fs))
                    {
                        float k = Math.Min(1f, (float)MaxDim / Math.Max(src.Width, src.Height));
                        img = new Bitmap(src, Math.Max(1, (int)(src.Width * k)), Math.Max(1, (int)(src.Height * k)));
                    }
                }
            }
            catch { img = null; }
            cache[fileName] = img;
            return img;
        }

        public static void Invalidate(string fileName)
        {
            Image img;
            if (fileName != null && cache.TryGetValue(fileName, out img)) { if (img != null) img.Dispose(); cache.Remove(fileName); }
        }

        /// <summary>Copies a picked file into the Images folder and returns the stored file name.</summary>
        public static string Import(string sourcePath)
        {
            string ext = Path.GetExtension(sourcePath);
            string name = Guid.NewGuid().ToString("N") + ext;
            File.Copy(sourcePath, Path.Combine(Folder, name), true);
            return name;
        }
    }
}
