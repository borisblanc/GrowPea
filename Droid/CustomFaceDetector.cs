using System;
using System.Collections.Generic;
using Android.Gms.Vision.Faces;
using Android.Gms.Vision;
using Android.Util;
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
                try
                {
                    if (_allFrameData.Count > 5000
                    ) //cancel recording if too many frames are collected and notify subscribers
                    {
                        isRecording = false;
                    }

                    var _framebuff =
                        Utils.deepCopy(frame
                            .GrayscaleImageData); //must copy buffer right away before it gets overriden VERY IMPORTANT
                    var _frametimestamp = frame.GetMetadata().TimestampMillis;

                    var detected = _detector.Detect(frame);

                    if (!_allFrameData.ContainsKey(_frametimestamp))
                        _allFrameData.Add(_frametimestamp, new FrameData(_frametimestamp, _framebuff, detected));

                    return detected;
                }
                catch (Exception e)
                {
                    Log.Error("CustomFaceDetector", "Detect(Frame frame) missed something", e, e.Message);
                    return _detector.Detect(frame);
                }
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

    }

}