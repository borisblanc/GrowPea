

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Graphics;
using Java.Nio;


namespace GrowPea
{
    public static class Utils
    {
        

        public static ByteBuffer deepCopy(ByteBuffer orig)
        {
            int pos = orig.Position(), lim = orig.Limit();
            try
            {
                orig.Position(0).Limit(orig.Capacity()); // set range to entire buffer
                ByteBuffer toReturn = deepCopyVisible(orig); // deep copy range
                toReturn.Position(pos).Limit(lim); // set range to original
                return toReturn;
            }
            finally // do in finally in case something goes wrong we don't bork the orig
            {
                orig.Position(pos).Limit(lim); // restore original
            }
        }

        public static ByteBuffer deepCopyVisible(ByteBuffer orig)
        {
            int pos = orig.Position();
            try
            {
                ByteBuffer toReturn;
                // try to maintain implementation to keep performance
                if (orig.IsDirect)
                    toReturn = ByteBuffer.AllocateDirect(orig.Remaining());
                else
                    toReturn = ByteBuffer.Allocate(orig.Remaining());

                toReturn.Put(orig);
                toReturn.Order(orig.Order());

                return (ByteBuffer)toReturn.Position(0);
            }
            finally
            {
                orig.Position(pos);
            }
        }

        private static Bitmap Resize(Bitmap image, int maxWidth, int maxHeight)
        {
            if (maxHeight > 0 && maxWidth > 0)
            {
                int width = image.Width;
                int height = image.Height;
                float ratioBitmap = (float)width / (float)height;
                float ratioMax = (float)maxWidth / (float)maxHeight;

                int finalWidth = maxWidth;
                int finalHeight = maxHeight;
                if (ratioMax > 1)
                {
                    finalWidth = (int)((float)maxHeight * ratioBitmap);
                }
                else
                {
                    finalHeight = (int)((float)maxWidth / ratioBitmap);
                }
                image = Bitmap.CreateScaledBitmap(image, finalWidth, finalHeight, true);
                return image;
            }
            else
            {
                return image;
            }
        }

        private static void ExportBitmapAsPNG(Bitmap bitmap, float score)
        {
            var sdCardPath = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
            var filePath = System.IO.Path.Combine(sdCardPath, string.Format("{0}test.png", score.ToString().Replace(".", string.Empty)));
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
            }
        }


    }


}
