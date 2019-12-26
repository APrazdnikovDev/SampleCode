using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ChicagoSTDriveMgr.Helpers
{
    class Utils
    {
        public static bool IsImage(byte[] array, out Size imageSize, out string imageFormat, out string fileNameExtension, out string errorMessage)
        {
            imageSize = new Size(0, 0);
            imageFormat = fileNameExtension = null;
            bool result;

            if (array == null || array.Length == 0)
            {
                errorMessage = "Empty array";
                return false;
            }

            Image image = null;

            try
            {
                using (var ms = new MemoryStream(array))
                {
                    try
                    {
                        image = Image.FromStream(ms);
                        result = true;
                        imageSize = image.Size;
                        imageFormat = GetImageFormat(image);
                        fileNameExtension = GetImageFileNameExtension(image);
                    }
                    catch (ArgumentException ex)
                    {
                        result = false;
                        errorMessage = ex.Message;
                    }
                    catch (Exception ex)
                    {
                        result = false;
                        errorMessage = ex.Message;
                    }
                }
            }
            finally
            {
                image?.Dispose();
                image = null;
            }
            errorMessage = string.Empty;
            return result;
        }

        static string GetImageFormat(Image image)
        {
            var result = "unknown";

            if (image.RawFormat.Equals(ImageFormat.Bmp))
                result = "bmp";
            else if (image.RawFormat.Equals(ImageFormat.Emf))
                result = "emf";
            else if (image.RawFormat.Equals(ImageFormat.Exif))
                result = "exif";
            else if (image.RawFormat.Equals(ImageFormat.Gif))
                result = "gif";
            else if (image.RawFormat.Equals(ImageFormat.Icon))
                result = "icon";
            else if (image.RawFormat.Equals(ImageFormat.Jpeg))
                result = "jpeg";
            else if (image.RawFormat.Equals(ImageFormat.MemoryBmp))
                result = "memorybmp";
            else if (image.RawFormat.Equals(ImageFormat.Png))
                result = "png";
            else if (image.RawFormat.Equals(ImageFormat.Tiff))
                result = "tiff";
            else if (image.RawFormat.Equals(ImageFormat.Wmf))
                result = "wmf";

            return result;
        }

        static string GetImageFileNameExtension(Image image)
        {
            var result = string.Empty;

            if (image.RawFormat.Equals(ImageFormat.Bmp))
                result = ".bmp";
            else if (image.RawFormat.Equals(ImageFormat.Emf))
                result = ".emf";
            else if (image.RawFormat.Equals(ImageFormat.Exif))
                result = ".exif";
            else if (image.RawFormat.Equals(ImageFormat.Gif))
                result = ".gif";
            else if (image.RawFormat.Equals(ImageFormat.Icon))
                result = ".ico";
            else if (image.RawFormat.Equals(ImageFormat.Jpeg))
                result = ".jpg";
            else if (image.RawFormat.Equals(ImageFormat.MemoryBmp))
                result = ".bmp";
            else if (image.RawFormat.Equals(ImageFormat.Png))
                result = ".png";
            else if (image.RawFormat.Equals(ImageFormat.Tiff))
                result = ".tiff";
            else if (image.RawFormat.Equals(ImageFormat.Wmf))
                result = ".wmf";

            return result;
        }
    }
}
