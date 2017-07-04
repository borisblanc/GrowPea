using System;
using System.Collections.Generic;
using Android.Gms.Vision.Faces;
using Android.Gms.Vision;
using Android.Util;
using System.ComponentModel;
using Android.Graphics;

namespace GrowPea.Droid
{
    public class CustomFaceDetector : Detector//, INotifyPropertyChanged
    {
        private FaceDetector _detector;

        private SortedList<float, FrameData> _allFrameData;

        private bool _isRecording;

        private int _width;
        private int _height;

        //public bool isRecording
        //{
        //    get { return _isRecording; }
        //    set
        //    {
        //        if (value) //if new recording then reset saved frames
        //            _allFrameData = new SortedList<float, FrameData>();

        //        _isRecording = value;
        //        //PropertyChanged(this, new PropertyChangedEventArgs("isRecording"));
        //    }
        //}

        //public event PropertyChangedEventHandler PropertyChanged;

        public CustomFaceDetector(FaceDetector detector, ref SortedList<float, FrameData> allFrameData, int width, int height)
        {
            _detector = detector;
            _allFrameData = allFrameData;
            _width = width;
            _height = height;
        }

        public override SparseArray Detect(Frame frame)
        {
            //if (_allFrameData.Count > 5000) //cancel recording if too many frames are collected and notify subscribers
            //{
            //    isRecording = false;
            //}
            try
            {
                var _framebuff = Utils.deepCopy(frame.GrayscaleImageData); //must copy buffer right away before it gets overriden VERY IMPORTANT

                byte[] b = new byte[_framebuff.Remaining()];
                _framebuff.Get(b);

                YuvImage yuvImage = new YuvImage(b, ImageFormatType.Nv21, _width, _height, null);
                var _frametimestamp = frame.GetMetadata().TimestampMillis;

                var detected = _detector.Detect(frame);

                if (!_allFrameData.ContainsKey(_frametimestamp))
                    _allFrameData.Add(_frametimestamp, new FrameData(_frametimestamp, yuvImage, detected));

                return detected;
            }
            catch (Exception e)
            {
                Log.Error("CustomFaceDetector", e.StackTrace);
                return _detector.Detect(frame); //if processing fails return something
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

}