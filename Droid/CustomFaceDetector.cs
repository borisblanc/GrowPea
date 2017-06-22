﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Gms.Vision.Faces;
using Android.Gms.Vision;
using Android.Util;
using Android.Graphics;
using Java.IO;
using Java.Nio;
using System.Threading.Tasks;
using System.ComponentModel;



namespace GrowPea.Droid
{
    public class CustomFaceDetector: Detector, INotifyPropertyChanged
    {
        private FaceDetector _detector;

        private SortedList<float, BMFaces> _goodfaces;

        private bool _isRecording = false;

        public bool isRecording
        {
            get { return _isRecording; }
            set {
                _isRecording = value;
                PropertyChanged(this, new PropertyChangedEventArgs("isRecording"));
            }
        }




        private bool IsFrameDimensionSet = false;
        private int FrameWidth;
        private int FrameHeight;


        private Object thisLock1 = new Object();
        private Object thisLock = new Object();

        public event PropertyChangedEventHandler PropertyChanged;

        public CustomFaceDetector(FaceDetector detector) {
            _goodfaces = new SortedList<float, BMFaces>();
            _detector = detector;
        }

        public override SparseArray Detect(Frame frame)
        {
            VerifyFrameDimensions(frame);
            ByteBuffer _framebuff = null;

            if (isRecording)
            _framebuff = frame.GrayscaleImageData.Duplicate(); //must copy buffer right away before it gets overriden VERY IMPORTANT
     
            var detected = _detector.Detect(frame);

            if (isRecording)
                Task.Run(() => QualifyBitmap(detected, _framebuff)); //fire and forget

            return detected;
        }

        private void QualifyBitmap(SparseArray detected, ByteBuffer framebuff)
        {
            if (detected.Size() > 0)
            {
                
                for (int i = 0, nsize = detected.Size(); i < nsize; i++)
                {
                    Object obj = detected.ValueAt(i);
                    if (obj != null && obj.GetType() == typeof(Face))
                    {
                        var face = (Face)obj;
                        var iUse = GetImageUsability(face);

                        if (iUse > 0)
                        {
                            if (_goodfaces.Count < 100) //compile 100 best
                            {
                                if (!_goodfaces.ContainsKey(iUse)) //don't keep one with same score twice
                                {
                                    var bmap = GetBitmap(framebuff);
                                    _goodfaces.Add(iUse, new BMFaces(bmap, face));
                                }
                            }
                            else //save top 3 to phone
                            {

                                isRecording = false; //turn off recording this will notify parents to stop also

                                lock (thisLock1)
                                {
                                    //last = best
                                    var lastkey = _goodfaces.Keys.ToList().Last();
                                    var lastix = _goodfaces.IndexOfKey(lastkey);
                                    Bitmap bm = _goodfaces.Values[lastix].BM;
                                    ExportBitmapAsPNG(bm, lastkey);


                                    //second last/best
                                    bm = _goodfaces.Values[lastix - 1].BM;
                                    ExportBitmapAsPNG(bm, _goodfaces.Keys[lastix - 1]);

                                    //third last/best
                                    bm = _goodfaces.Values[lastix - 2].BM;
                                    ExportBitmapAsPNG(bm, _goodfaces.Keys[lastix - 2]);
                                }

                                _goodfaces.Clear();
                            }

                        }
                        
                    }
                }
            }
        }

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
                var encoder= new Encoder(width, height, bitRate, Savelocation);
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
            lock (thisLock)
            {
                var sdCardPath = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
                var filePath = System.IO.Path.Combine(sdCardPath, string.Format("{0}test.png", score.ToString().Replace(".", string.Empty)));
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
                }
            }
        }

        public bool isOperational()
        {
            return _detector.IsOperational;
        }

        public bool setFocus(int id)
        {
            return _detector.SetFocus(id);
        }


    }

    public class BMFaces
    {
        public Bitmap BM;

        public Face F;

        public BMFaces(Bitmap bitmap, Face face)
        {
            BM = bitmap;
            F = face;
        }
    }
}