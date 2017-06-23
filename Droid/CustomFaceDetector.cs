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
using System.ComponentModel;



namespace GrowPea.Droid
{
    public class CustomFaceDetector : Detector, INotifyPropertyChanged
    {
        private FaceDetector _detector;

        public SortedList<float, FrameData> _allFrameData;

        private bool _isRecording;

        public bool isRecording
        {
            get { return _isRecording; }
            set
            {
                _isRecording = value;
                PropertyChanged(this, new PropertyChangedEventArgs("isRecording"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public CustomFaceDetector(FaceDetector detector)
        {
            _detector = detector;
            _allFrameData = new SortedList<float, FrameData>();
        }

        public override SparseArray Detect(Frame frame)
        {
            if (isRecording)
            {
                if (_allFrameData.Count > 10000) //cancel recording if too many frames are collected and notify subscribers
                {
                    isRecording = false;
                }

                var _framebuff = Utils.deepCopy(frame.GrayscaleImageData); //must copy buffer right away before it gets overriden VERY IMPORTANT
                var _frametimestamp = frame.GetMetadata().TimestampMillis;

                var detected = _detector.Detect(frame);

                if (!_allFrameData.ContainsKey(_frametimestamp))
                    _allFrameData.Add(_frametimestamp, new FrameData(_frametimestamp, _framebuff, detected));

                return detected;
            }
            else
            {
                return _detector.Detect(frame);
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


        private void GatherData(SparseArray detected, ByteBuffer framebuff, float timestamp)
        {
            _allFrameData.Add(timestamp, new FrameData(timestamp, framebuff, detected));
        }

    }



    public class FrameData
    {
        public ByteBuffer _bytebuff;

        public SparseArray _sparsearray;

        public float _timestamp;

        public FrameData(float timestamp, ByteBuffer bytebuff, SparseArray sparsearray)
        {
            _timestamp = timestamp;
            _bytebuff = bytebuff;
            _sparsearray = sparsearray;
        }
    }
}