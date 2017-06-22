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



namespace GrowPea.Droid
{
    public class FrameDataProcessor
    {

        private SortedList<float, BMFaces> _goodfaces;


        private bool IsFrameDimensionSet = false;
        private int FrameWidth;
        private int FrameHeight;

        //private void QualifyBitmap(SparseArray detected, ByteBuffer framebuff, long framets)
        //{
        //    for (int i = 0, nsize = detected.Size(); i < nsize; i++)
        //    {
        //        Object obj = detected.ValueAt(i);
        //        if (obj != null && obj.GetType() == typeof(Face))
        //        {
        //            var face = (Face)obj;
        //            var iUse = GetImageUsability(face);

        //            if (_goodfaces.Count <= 20) //get 10 frames
        //            {
        //                var bmap = GetBitmap(framebuff);
        //                _goodfaces.Add(framets, new BMFaces(bmap, face, iUse));
        //            }
        //            else //save top 3 to phone
        //            {
        //                isRecording = false; //turn off recording this will notify parents to stop also

        //                lock (thisLock1)
        //                {
        //                    //last = best
        //                    //var lastkey = _goodfaces.Keys.ToList().Last();
        //                    //var lastix = _goodfaces.IndexOfKey(lastkey);
        //                    //Bitmap bm = _goodfaces.Values[lastix].BM;
        //                    //ExportBitmapAsPNG(bm, lastkey);


        //                    ////second last/best
        //                    //bm = _goodfaces.Values[lastix - 1].BM;
        //                    //ExportBitmapAsPNG(bm, _goodfaces.Keys[lastix - 1]);

        //                    ////third last/best
        //                    //bm = _goodfaces.Values[lastix - 2].BM;
        //                    //ExportBitmapAsPNG(bm, _goodfaces.Keys[lastix - 2]);

        //                    foreach (var BMFace in _goodfaces.Values)
        //                    {
        //                        ExportBitmapAsPNG(BMFace.BM, BMFace.Iuse);
        //                    }

        //                    _goodfaces.Clear();
        //                }


        //            }
        //        }
        //    }
        //}

        public String MakeBitmapVideo(List<Bitmap> images, String Savelocation, String name, int width, int height, int bitRate)
        {

            //setParameters(640, 480, 4000000);

            var directory = new Java.IO.File(Savelocation);
            if (!directory.Exists())
            {
                directory.Mkdir();
            }
            var outputfile = new Java.IO.File(directory, name + ".mp4");

            try
            {
                var encoder = new Encoder(width, height, bitRate, Savelocation);
                encoder.EncodeAll(images);
            }
            catch
            {

            }
            return outputfile.AbsolutePath;
        }

        private void VerifyFrameDimensions(Frame frame)
        {
            if (!IsFrameDimensionSet)
            {
                FrameWidth = frame.GetMetadata().Width;
                FrameHeight = frame.GetMetadata().Height;
            }
            else
            {
                if (FrameWidth != frame.GetMetadata().Width || FrameHeight != frame.GetMetadata().Height)
                {
                    throw new InvalidOperationException("Frame Dimensions can never change after processing starts"); //will always be landscape
                }

            }

        }

        private Bitmap GetBitmap(ByteBuffer framebuff)
        {
            var yuvimage = GetYUVImage(framebuff);

            Bitmap b;

            using (var baos = new MemoryStream())
            {
                yuvimage.CompressToJpeg(new Rect(0, 0, FrameWidth, FrameHeight), 100, baos); // Where 100 is the quality of the generated jpeg
                byte[] jpegArray = baos.ToArray();
                b = BitmapFactory.DecodeByteArray(jpegArray, 0, jpegArray.Length);
            }

            return b;
        }

        private YuvImage GetYUVImage(ByteBuffer framebuff)
        {
            byte[] barray = new byte[framebuff.Remaining()];
            framebuff.Get(barray);

            return new YuvImage(barray, ImageFormatType.Nv21, FrameWidth, FrameHeight, null);
        }

        private float GetImageUsability(Face face)
        {
            return ((face.IsSmilingProbability * 2) + face.IsRightEyeOpenProbability + face.IsLeftEyeOpenProbability) / 3;
        }


        private void ExportBitmapAsPNG(Bitmap bitmap, float score)
        {
            var sdCardPath = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
            var filePath = System.IO.Path.Combine(sdCardPath, string.Format("{0}test.png", score.ToString().Replace(".", string.Empty)));
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
            }
        }



    }

    public class BMFaces
    {
        public Bitmap BM;

        public Face F;

        public float Iuse;

        public BMFaces(Bitmap bitmap, Face face, float ImageUsability)
        {
            BM = bitmap;
            F = face;
            Iuse = ImageUsability;
        }
    }


}
