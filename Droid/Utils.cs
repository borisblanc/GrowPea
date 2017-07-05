using System.Collections.Generic;
using System.IO;
using Android.Graphics;
using Android.Util;
using Java.Lang;
using Java.Nio;
using Exception = System.Exception;
using System.IO.Compression;
using GrowPea.Droid;
using Object = System.Object;

namespace GrowPea
{
    public static class Utils
    {
        private static readonly System.Object obj = new Object();

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


        public static Bitmap GetBitmap(YuvImage yuvimage, int width, int height)
        {
            Bitmap b;
            try
            {
                using (var baos = new MemoryStream())
                {
                    yuvimage.CompressToJpeg(new Android.Graphics.Rect(0, 0, width, height), 100, baos); // Where 100 is the quality of the generated jpeg
                    byte[] jpegArray = baos.ToArray();
                    //var bitmapoptions = new BitmapFactory.Options { InSampleSize = 2 };
                    b = BitmapFactory.DecodeByteArray(jpegArray, 0, jpegArray.Length); //, bitmapoptions);
                }
            }
            catch (Exception e)
            {
                Log.Error("Utils", "could not get bitmap", e, e.Message);
                throw new RuntimeException("could not get bitmap");
            }

            return b;
        }

        public static byte[] Compress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var dstream = new DeflateStream(output, CompressionLevel.Optimal))
                {
                    dstream.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            {
                using (var output = new MemoryStream())
                {
                    using (var dstream = new DeflateStream(input, CompressionMode.Decompress))
                    {
                        dstream.CopyTo(output);
                    }
                    return output.ToArray();
                }
            }
        }

        public static void AddCompressedData(ref SortedList<float, FrameData> allframes, ByteBuffer bytebuff, long timestamp, SparseArray detected)
        {
            lock (obj)
            {
                var b = new byte[bytebuff.Remaining()];
                bytebuff.Get(b);

                byte[] compb = Utils.CompressFast(b);

                if (!allframes.ContainsKey(timestamp))
                    allframes.Add(timestamp, new FrameData(timestamp, compb, detected));
            }
        }

        public static byte[] CompressFast(byte[] raw)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory, CompressionLevel.Fastest))
                {
                    gzip.Write(raw, 0, raw.Length);
                }
                return memory.ToArray();
            }
        }

        public static byte[] DecompressFast(byte[] data)
        {
            using (var input = new MemoryStream(data))
            {
                using (var output = new MemoryStream())
                {
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    {
                        gzip.CopyTo(output);
                    }
                    return output.ToArray();
                }
            }
        }


    }


}
