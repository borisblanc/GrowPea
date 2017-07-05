﻿using System;
using System.Collections.Generic;
using Android.Gms.Vision.Faces;
using Android.Gms.Vision;
using Android.Util;
using System.ComponentModel;
using System.Threading.Tasks;
using Android.Text.Method;


namespace GrowPea.Droid
{
    public class CustomFaceDetector : Detector//, INotifyPropertyChanged
    {
        private FaceDetector _detector;

        private SortedList<float, FrameData> _allFrameData;

        private List<Task> _compressDataTasks;

        //private bool _isRecording;

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

        public CustomFaceDetector(FaceDetector detector, ref SortedList<float, FrameData> allFrameData, ref List<Task> CompressDataTasks)
        {
            _detector = detector;
            _allFrameData = allFrameData;
            _compressDataTasks = CompressDataTasks;
        }

        public override SparseArray Detect(Frame frame)
        {
            try
            {



                var detected = _detector.Detect(frame);

                var _framebuff = Utils.deepCopy(frame.GrayscaleImageData); //must copy buffer right away before it gets overriden VERY IMPORTANT
                var _frametimestamp = frame.GetMetadata().TimestampMillis;

                _compressDataTasks.Add(Task.Run(() => Utils.AddCompressedData(ref _allFrameData, _framebuff, _frametimestamp, detected)));

                return detected;
            }
            catch(Exception e)
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