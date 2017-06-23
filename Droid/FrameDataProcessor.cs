using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Android.Graphics;
using Java.IO;

using Android.Gms.Vision.Faces;
using Android.Gms.Vision;

using Android.Util;

using Java.Nio;

using System.IO;
using System.Threading.Tasks;
using Java.Security;
using Console = System.Console;


namespace GrowPea.Droid
{
    public class FrameDataProcessor
    {

        private static readonly string TAG = "FrameDataProcessor";


        public List<BmFace> ALLFaces;
        private SortedList<float, FrameData> _allFrameData;

        private readonly int _frameWidth;
        private readonly int _frameHeight;

        private static readonly Object obj = new Object();

        public FrameDataProcessor(SortedList<float, FrameData> allframedata, int framewidth, int frameheight)
        {
            _allFrameData = allframedata;
            _frameWidth = framewidth;
            _frameHeight = frameheight;
            ALLFaces = new List<BmFace>();
        }

        public Task<bool> BeginProcessingFrames()
        {
            Task<bool> t = new Task<bool>(ProcessFrames);
            t.Start();
            return t;
        }

        private bool ProcessFrames()
        {
            //write code here to process
            Log.Info(TAG, string.Format("number of frames {0}",_allFrameData.Values.Count));
            var bestfaceframes = new List<BmFace>();

            try
            {
                if (_allFrameData != null)
                {
                    foreach (var FD in _allFrameData.Values)
                    {
                        var face = GetSparseFace(FD._sparsearray);

                        if (face != null)
                        {
                            var iUse = GetImageUsability(face);
                            lock (obj)
                            {
                                var bmap = GetBitmap(FD._bytebuff);
                                ALLFaces.Add(new BmFace(FD._timestamp, bmap, iUse));
                            }
                        }

                    }

                    if (ALLFaces.Count > 0)
                    {
                        var frameoffsetMinusStart = 10; //start offset
                        var frameboundPlusEnd = 30; //count of frames
               

                        var maxIuse = ALLFaces.Max(x => x.Iuse);
                        var bestfaceIndex = ALLFaces.Select((Value, Index) => new { Value, Index })
                            .Where(f => f.Index >= frameoffsetMinusStart && f.Index <= ALLFaces.Count - (frameboundPlusEnd - frameoffsetMinusStart))
                            .First(f => f.Value.Iuse == maxIuse); //offsets take into account array size so best face is within bounds
                        bestfaceframes = ALLFaces.GetRange(bestfaceIndex.Index - 10, 30); //range around bestface of 30 frames
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            for(int i = 0; i < bestfaceframes.Count; i++)
            {
                ExportBitmapAsPNG(bestfaceframes[i].BM, i); 
            }

            if (bestfaceframes.Count > 0)
                return true;
            else
                return false;
            
        }

        private Face GetSparseFace(SparseArray array)
        {
            Face face = null;
            for (int i = 0, nsize = array.Size(); i < nsize; i++)
            {
                Object obj = array.ValueAt(i);
                if (obj != null && obj.GetType() == typeof(Face))
                {
                    face = (Face) obj;
                    break;
                }
            }
            return face;
        }


        //private void QualifyBitmap(SparseArray detected, ByteBuffer framebuff, long framets)
        //{
        //    for (int i = 0, nsize = detected.Size(); i < nsize; i++)
        //    {
        //        Object obj = detected.ValueAt(i);
        //        if (obj != null && obj.GetType() == typeof(Face))
        //        {
        //            var face = (Face)obj;
        //            var iUse = GetImageUsability(face);

        //            if (GoodFaces.Count <= 20) //get 10 frames
        //            {
        //                var bmap = GetBitmap(framebuff);
        //                GoodFaces.Add(framets, new BMFaces(bmap, face, iUse));
        //            }
        //            else //save top 3 to phone
        //            {


        //                //last = best
        //                //var lastkey = _goodfaces.Keys.ToList().Last();
        //                //var lastix = _goodfaces.IndexOfKey(lastkey);
        //                //Bitmap bm = _goodfaces.Values[lastix].BM;
        //                //ExportBitmapAsPNG(bm, lastkey);


        //                ////second last/best
        //                //bm = _goodfaces.Values[lastix - 1].BM;
        //                //ExportBitmapAsPNG(bm, _goodfaces.Keys[lastix - 1]);

        //                ////third last/best
        //                //bm = _goodfaces.Values[lastix - 2].BM;
        //                //ExportBitmapAsPNG(bm, _goodfaces.Keys[lastix - 2]);

        //                foreach (var BMFace in _goodfaces.Values)
        //                {
        //                    ExportBitmapAsPNG(BMFace.BM, BMFace.Iuse);
        //                }

        //                _goodfaces.Clear();

        //            }
        //        }
        //    }
        //}

        public String MakeBitmapVideo(List<Bitmap> images, String Savelocation, String name, int bitRate)
        {
            //if 640 by 480 use 4000000 bitrate

            var directory = new Java.IO.File(Savelocation);
            if (!directory.Exists())
            {
                directory.Mkdir();
            }
            var outputfile = new Java.IO.File(directory, name + ".mp4");

            try
            {
                var encoder = new Encoder(_frameWidth, _frameHeight, bitRate, Savelocation);
                encoder.EncodeAll(images);
            }
            catch
            {

            }
            return outputfile.AbsolutePath;
        }



        private Bitmap GetBitmap(ByteBuffer framebuff)
        {
            Bitmap b;
            try
            {
                var yuvimage = GetYUVImage(framebuff);
                using (var baos = new MemoryStream())
                {
                    yuvimage.CompressToJpeg(new Rect(0, 0, _frameWidth, _frameHeight), 100, baos); // Where 90 is the quality of the generated jpeg
                    byte[] jpegArray = baos.ToArray();
                    var bitmapoptions = new BitmapFactory.Options {InSampleSize = 2};
                    b = BitmapFactory.DecodeByteArray(jpegArray, 0, jpegArray.Length, bitmapoptions);
                    //b = Resize(bitmap, 640, 480);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return b;
        }

        //private static Bitmap Resize(Bitmap image, int maxWidth, int maxHeight)
        //{
        //    if (maxHeight > 0 && maxWidth > 0)
        //    {
        //        int width = image.Width;
        //        int height = image.Height;
        //        float ratioBitmap = (float)width / (float)height;
        //        float ratioMax = (float)maxWidth / (float)maxHeight;

        //        int finalWidth = maxWidth;
        //        int finalHeight = maxHeight;
        //        if (ratioMax > 1)
        //        {
        //            finalWidth = (int)((float)maxHeight * ratioBitmap);
        //        }
        //        else
        //        {
        //            finalHeight = (int)((float)maxWidth / ratioBitmap);
        //        }
        //        image = Bitmap.CreateScaledBitmap(image, finalWidth, finalHeight, true);
        //        return image;
        //    }
        //    else
        //    {
        //        return image;
        //    }
        //}

        private YuvImage GetYUVImage(ByteBuffer framebuff)
        {
            byte[] barray = new byte[framebuff.Remaining()];
            framebuff.Get(barray);

            return new YuvImage(barray, ImageFormatType.Nv21, _frameWidth, _frameHeight, null);
        }

        private float GetImageUsability(Face face)
        {
            return ((face.IsSmilingProbability * 2) + face.IsRightEyeOpenProbability + face.IsLeftEyeOpenProbability) / 3;
        }


        private void ExportBitmapAsPNG(Bitmap bitmap, float score)
        {
            lock (obj)
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

    public class BmFace
    {
        public Bitmap BM;

        public float Iuse;

        public float TS;

        public BmFace(float timestamp, Bitmap bitmap, float ImageUsability)
        {
            TS = timestamp;
            BM = bitmap;
            Iuse = ImageUsability;
        }
    }


}
