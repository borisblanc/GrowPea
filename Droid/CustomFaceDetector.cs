using System;
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

namespace GrowPea.Droid
{
    public class CustomFaceDetector: Detector
    {
        private FaceDetector _detector;

        private SortedList<float, BMFaces> _goodfaces;
        private bool _sessiondone = false;

        private Object thisLock = new Object();

        public CustomFaceDetector(FaceDetector detector) {
            _goodfaces = new SortedList<float, BMFaces>();
            _detector = detector;
        }

        public override SparseArray Detect(Frame frame)
        {
            var width = frame.GetMetadata().Width;
            var height = frame.GetMetadata().Height;
            var _framebuff = frame.GrayscaleImageData.Duplicate(); //must copy buffer right away before it gets overriden
     
            var detected = _detector.Detect(frame);

            if (!_sessiondone)
                Task.Run(() => QualifyBitmap(detected, _framebuff, width, height)); //fire and forget

            return detected;
        }

        private void QualifyBitmap(SparseArray detected, ByteBuffer framebuff, int width, int height)
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
                                    var bmap = GetBitmap(framebuff, width, height);
                                    _goodfaces.Add(iUse, new BMFaces(bmap, face));
                                }
                            }
                            else //save top 3 to phone
                            {

                                //foreach (KeyValuePair<float, BMFaces> kvp in _goodfaces)
                                //{
                                //    ExportBitmapAsPNG(kvp.Value.BM, kvp.Key);

                                //}

                                //last = best
                                var lastkey = _goodfaces.Keys.ToList().Last();
                                var lastix = _goodfaces.IndexOfKey(lastkey);
                                Bitmap bm = _goodfaces.Values[lastix].BM;
                                ExportBitmapAsPNG(bm, lastkey); 
                                
                                
                                //second last/best
                                bm = _goodfaces.Values[lastix -1].BM;
                                ExportBitmapAsPNG(bm, _goodfaces.Keys[lastix - 1]); 

                                //third last/best
                                bm = _goodfaces.Values[lastix - 2].BM;
                                ExportBitmapAsPNG(bm, _goodfaces.Keys[lastix - 1]); 


                                _goodfaces.Clear();
                                _sessiondone = true;
                            }

                        }
                        
                    }
                }
            }
        }



        private Bitmap GetBitmap(ByteBuffer framebuff, int width, int height)
        {
            Bitmap b;

            byte[] barray = new byte[framebuff.Remaining()];
            framebuff.Get(barray);

            var yuvimage = new YuvImage(barray, ImageFormatType.Nv21, width, height, null);

            using (var baos = new MemoryStream())
            {
                yuvimage.CompressToJpeg(new Rect(0, 0, width, height), 100, baos); // Where 100 is the quality of the generated jpeg
                byte[] jpegArray = baos.ToArray();
                b = BitmapFactory.DecodeByteArray(jpegArray, 0, jpegArray.Length);
            }
            
            return b;
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
                var filePath = System.IO.Path.Combine(sdCardPath, string.Format("{0}test.jpeg", score.ToString().Replace(".", string.Empty)));
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