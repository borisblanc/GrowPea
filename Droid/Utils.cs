using System;
using System.Collections.Generic;
using System.IO;
using Android.Graphics;
using Android.Util;
using Java.Lang;
using Java.Nio;
using Exception = System.Exception;
using System.IO.Compression;
using Android.Gms.Vision.Faces;
using GrowPea.Droid;
using Object = System.Object;

namespace GrowPea
{
    public static class Utils
    {
        private static readonly System.Object obj = new Object();




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



        public static void AddConvertByteBuffer(ref SortedList<float, FrameData> allframes, ByteBuffer bytebuff, long timestamp, SparseArray detected, int width, int height, int quality)
        {
            try
            {

                var b = new byte[bytebuff.Remaining()];
                bytebuff.Get(b);

                var yuv = new YuvImage(b, ImageFormatType.Nv21, width, height, null);
                byte[] jpegArray;
                using (var baos = new MemoryStream())
                {
                    yuv.CompressToJpeg(new Rect(0, 0, width, height), quality, baos); // Where 100 is the quality of the generated jpeg
                    jpegArray = baos.ToArray();
                }

                lock (obj)
                {
                    //if (!allframes.ContainsKey(timestamp)) //can i spped up this check?
                        allframes.Add(timestamp, new FrameData(timestamp, jpegArray, detected));
                }
            }
            catch (Exception e)
            {
                var x = e;
            }
        }

        public static Face GetSparseFace(SparseArray array)
        {
            Face face = null;
            try
            {
                for (int i = 0, nsize = array.Size(); i < nsize; i++)
                {
                    Object obj = array.ValueAt(i);
                    if (obj != null && obj.GetType() == typeof(Face))
                    {
                        face = (Face)obj;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("utils", "GetSparseFace borked somehow", e);
            }
            return face;
        }


    }


}
