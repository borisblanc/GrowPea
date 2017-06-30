﻿using System;
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
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using Java.Lang;
using Java.Security;
using Console = System.Console;
using Exception = System.Exception;
using Object = System.Object;
using String = System.String;


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

        private int _bitRate;

        private int _fps;

        private int _vidlengthseconds;

        private int bitRate
        {
            get
            {
                //if 640 by 480 use 4000000 bitrate
                //if 320 by 240 use 2000000 bitrate
                if (_frameWidth == 640 && _frameHeight == 480)
                    _bitRate = 6000000;
                else if (_frameWidth == 320 && _frameHeight == 240)
                    _bitRate = 3000000;
                else
                {
                    Log.Error(TAG, "Uknown bitrate calculation");
                    throw new RuntimeException("Uknown bitrate calculation");
                }
                return _bitRate;
            }
            
        }



        public FrameDataProcessor(SortedList<float, FrameData> allframedata, int framewidth, int frameheight, int fps, int vidlengthseconds)
        {
            _allFrameData = allframedata;
            _frameWidth = framewidth;
            _frameHeight = frameheight;
            _fps = fps;
            _vidlengthseconds = vidlengthseconds;
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

                    
                    var frameoffsetMinusStart = GetStartOffset(); //start offset
                    var frameboundPlusEnd = GetFrameTotal(); //count of frames
                    float maxIuse;
                    lock (obj)
                    {
                        var validframes = ALLFaces.Select((Value, Index) => new {Value, Index})
                        .Where(f => f.Index >= frameoffsetMinusStart &&
                                    f.Index <= ALLFaces.Count - (frameboundPlusEnd - frameoffsetMinusStart)).ToList(); //offsets take into account array size so best face is within bounds


                        maxIuse = validframes.Max(x => x.Value.Iuse);


                        var bestfaceIndex = validframes.First(f => f.Value.Iuse == maxIuse);


                        bestfaceframes = ALLFaces.GetRange(bestfaceIndex.Index - frameoffsetMinusStart, frameboundPlusEnd); //range around bestface of _fps * _vidlengthseconds
                    }
                    
                }
            }
            catch (Exception e)
            {
                Log.Error(TAG, "Frames Processing messed up", e);
                throw new RuntimeException("Frames Processing messed up");
            }


            //for(int i = 0; i < bestfaceframes.Count; i++)
            //{
            //    ExportBitmapAsPNG(bestfaceframes[i].BM, i); 
            //}

            List<ByteBuffer> allBitmaps = bestfaceframes.Select(f => f.bytebuff).ToList();

            string outputfilepath = MakeBufferVideo(allBitmaps, DateTime.Now.Ticks.ToString());

            if ( outputfilepath != null)
                return true;
            else
                return false;
        }

        //will depend on fps and length of video
        private int GetStartOffset()
        {
            if (_vidlengthseconds == 3)
            {
                return _fps; //for 3 second vids fps will always equal start offset (eg.. we will always want one second worth of frames prior to best smile)
            }
            else if (_vidlengthseconds == 4)
            {
                return _fps; //for 4 second vids fps will always equal start offset (eg.. we will always want one second worth of frames prior to best smile)
            }
            else if (_vidlengthseconds == 5)
            {
                return _fps * 2; //for 4 second vids fps will always equal start offset * 2 (eg.. we will always want two seconds worth of frames prior to best smile)
            }
            else
            {
                return _fps;
            }
        }

        //will depend on fps and length of video
        private int GetFrameTotal()
        {
            return _fps * _vidlengthseconds; //total frames will always be frames per second * number of seconds
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




        public String MakeBufferVideo(List<ByteBuffer> imagesinfo, String filename)
        {
            var Savelocation = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;

            var directory = new Java.IO.File(Savelocation);
            if (!directory.Exists())
            {
                directory.Mkdir();
            }
            var outputfilepath = new Java.IO.File(directory, filename + ".mp4").AbsolutePath;

            try
            {
                var encoder = new EncoderMuxer(_frameWidth, _frameHeight, bitRate, _fps, outputfilepath, imagesinfo);
                encoder.EncodeVideoToMp4();


            }
            catch(Exception e)
            {

            }
            return outputfilepath;
        }


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
