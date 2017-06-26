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

        private string filepath = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;

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
                                //var bmap = GetBitmap(FD._bytebuff);
                                ALLFaces.Add(new BmFace(FD._timestamp, FD._bytebuff, iUse));
                            }
                        }

                    }

                    if (ALLFaces.Count > 0)
                    {
                        var frameoffsetMinusStart = 10; //start offset
                        var frameboundPlusEnd = 30; //count of frames
               

                        var maxIuse = ALLFaces.Max(x => x.Iuse);
                        var bestfaceIndex = ALLFaces.Select((Value, Index) => new {Value, Index})
                            .Where(f => f.Value.Iuse == maxIuse)
                            .First(f => f.Index >= frameoffsetMinusStart &&
                                        f.Index <= ALLFaces.Count - (frameboundPlusEnd - frameoffsetMinusStart)); //offsets take into account array size so best face is within bounds
                        bestfaceframes = ALLFaces.GetRange(bestfaceIndex.Index - 10, 30); //range around bestface of 30 frames
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            //for(int i = 0; i < bestfaceframes.Count; i++)
            //{
            //    ExportBitmapAsPNG(bestfaceframes[i].BM, i); 
            //}





            List<ByteBuffer> allBitmaps = bestfaceframes.Select(f => f.bytebuff).ToList();

            string outputfilepath = MakeBufferVideo(allBitmaps, filepath, DateTime.Now.Ticks.ToString(), 2000000);

            if ( outputfilepath != null)
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




        public String MakeBufferVideo(List<ByteBuffer> imagesinfo, String Savelocation, String name, int bitRate)
        {
            //if 640 by 480 use 4000000 bitrate
            //if 320 by 240 use 2000000 bitrate

            var directory = new Java.IO.File(Savelocation);
            if (!directory.Exists())
            {
                directory.Mkdir();
            }
            var outputfile = new Java.IO.File(directory, name + ".mp4");

            try
            {
                //var encoder = new Encoder(_frameWidth, _frameHeight, bitRate, outputfile.AbsolutePath);
                //encoder.EncodeAll(imagesinfo);

                //var encoder = new EncodeAndMux();
                //encoder.StartSHit(imagesinfo, _frameWidth,_frameHeight, bitRate);

                var encoder = new EncoderMuxer();
                encoder.EncodeVideoToMp4(imagesinfo);
            }
            catch(Exception e)
            {

            }
            return outputfile.AbsolutePath;
        }



        //private Bitmap GetBitmap(ByteBuffer framebuff)
        //{
        //    Bitmap b;
        //    try
        //    {
        //        var yuvimage = GetYUVImage(framebuff);
        //        using (var baos = new MemoryStream())
        //        {
        //            yuvimage.CompressToJpeg(new Rect(0, 0, _frameWidth, _frameHeight), 100, baos); // Where 90 is the quality of the generated jpeg
        //            byte[] jpegArray = baos.ToArray();
        //            var bitmapoptions = new BitmapFactory.Options { InSampleSize = 2 };
        //            b = BitmapFactory.DecodeByteArray(jpegArray, 0, jpegArray.Length, bitmapoptions);
        //            //b = Resize(bitmap, 640, 480);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e);
        //        throw;
        //    }

        //    return b;
        //}



        //private YuvImage GetYUVImage(ByteBuffer framebuff)
        //{
        //    byte[] barray = new byte[framebuff.Remaining()];
        //    framebuff.Get(barray);

        //    return new YuvImage(barray, ImageFormatType.Nv21, _frameWidth, _frameHeight, null);
        //}

        private float GetImageUsability(Face face)
        {
            return ((face.IsSmilingProbability * 2) + face.IsRightEyeOpenProbability + face.IsLeftEyeOpenProbability) / 3;
        }






    }

    public class BmFace
    {
        //public Bitmap BM;

        public float Iuse;

        public float TS;

        public ByteBuffer bytebuff;

        public BmFace(float timestamp, ByteBuffer bbyte, float ImageUsability)
        {
            TS = timestamp;
            bytebuff = bbyte;
            Iuse = ImageUsability;
        }
    }


}
