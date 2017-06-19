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

namespace GrowPea.Droid
{
    public class CustomFaceDetector: Detector
    {
        private FaceDetector _detector;

        private List<BMFaces> _goodfaces;

        public CustomFaceDetector(FaceDetector detector) {
            _goodfaces = new List<BMFaces>();
            _detector = detector;
        }

        public override SparseArray Detect(Frame frame)
        {
            var detected = _detector.Detect(frame);

            //if something good is found get bitmap and save to array
            if (detected.Size() > 0)
            {
                for (int i = 0, nsize = detected.Size(); i < nsize; i++)
                {
                    Object obj = detected.ValueAt(i);
                    if (obj != null && obj.GetType() == typeof(Face))
                    {
                        var face = (Face)obj;
                        var iUse = GetImageUsability(face);

                        if (iUse > 0 && _goodfaces.Count < 200) //keep image 
                        {
                            var bmap = GetBitmap(frame);
                            _goodfaces.Add(new BMFaces(bmap, face));
                        }
                    }
                }
            }

            //Frame croppedFrame =
            //    new Frame.Builder()
            //            .SetBitmap(bitmap)
            //            .SetRotation(frame.GetMetadata().Rotation)
            //            .Build();

            return detected;
        }

        private Bitmap GetBitmap(Frame frame)
        {
            Bitmap b;
            using (ByteBuffer byteBuffer = frame.GrayscaleImageData)
            {
                byte[] barray = new byte[byteBuffer.Remaining()];
                byteBuffer.Get(barray);

                int w = frame.GetMetadata().Width;
                int h = frame.GetMetadata().Height;

                var yuvimage = new YuvImage(barray, ImageFormatType.Nv21, w, h, null);

                using (var baos = new MemoryStream())
                {
                    yuvimage.CompressToJpeg(new Rect(0, 0, w, h), 100, baos); // Where 100 is the quality of the generated jpeg
                    byte[] jpegArray = baos.ToArray();
                    b = BitmapFactory.DecodeByteArray(jpegArray, 0, jpegArray.Length);
                }
            }
            return b;
        }

        private float GetImageUsability(Face face)
        {
            return ((face.IsSmilingProbability * 2) + face.IsRightEyeOpenProbability + face.IsLeftEyeOpenProbability) / 3;
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