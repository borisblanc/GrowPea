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
                    yuvimage.CompressToJpeg(new Rect(0, 0, width, height), 100, baos); // Where 100 is the quality of the generated jpeg
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

        public static YuvImage GetYUVImage(ByteBuffer framebuff, ImageFormatType _CameraColorFormat, int _Width, int _Height)
        {
            byte[] barray = new byte[framebuff.Remaining()];
            framebuff.Get(barray);

            return new YuvImage(barray, _CameraColorFormat, _Width, _Height, null);
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


        //used for all possible cases of color correction accounting for discrepencies between android camera saved images and codec color formats
        //navigate to http://bigflake.com/mediacodec/ & see question 5 at the bottom



        public static void swapNV21_NV12(ref byte[] yuv, int _Width, int _Height)
        {
            int length = 0;
            if (yuv.Length % 2 == 0)
                length = yuv.Length;
            else
                length = yuv.Length - 1; //for uneven we need to shorten loop because it will go out of bounds because of i1 += 2

            for (int i1 = 0; i1 < length; i1 += 2)
            {
                if (i1 >= _Width * _Height)
                {
                    byte tmp = yuv[i1];
                    yuv[i1] = yuv[i1 + 1];
                    yuv[i1 + 1] = tmp;
                }
            }
        }



        public static byte[] swapYV12toI420(byte[] yv12bytes, int _Width, int _Height)
        {
            byte[] i420bytes = new byte[yv12bytes.Length];
            for (int i = 0; i < _Width * _Height; i++)
                i420bytes[i] = yv12bytes[i];
            for (int i = _Width * _Height; i < _Width * _Height + (_Width / 2 * _Height / 2); i++)
                i420bytes[i] = yv12bytes[i + (_Width / 2 * _Height / 2)];
            for (int i = _Width * _Height + (_Width / 2 * _Height / 2); i < _Width * _Height + 2 * (_Width / 2 * _Height / 2); i++)
                i420bytes[i] = yv12bytes[i - (_Width / 2 * _Height / 2)];
            return i420bytes;
        }


    }


}
