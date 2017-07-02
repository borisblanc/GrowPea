using System;
using System.Collections.Generic;
using System.Linq;
using Android.Gms.Vision.Faces;
using Android.Util;
using Java.Nio;
using System.Threading.Tasks;
using Exception = System.Exception;
using Object = System.Object;
using String = System.String;


namespace GrowPea.Droid
{
    public class FrameDataProcessor
    {
        private static readonly string TAG = "FrameDataProcessor";

        public List<BmFace> ALLFaces;
        private List<FrameData> _allFrameData;

        private readonly int _frameWidth;
        private readonly int _frameHeight;

        private static readonly Object obj = new Object();

        private int _bitRate;

        private int _fps;

        private int _vidlengthseconds;

        public List<int> beststartindexes;

        private int bitRate
        {
            get
            {
                //if 640 by 480 use 6000000 bitrate
                //if 320 by 240 use 3000000 bitrate
                if (_frameWidth == 640 && _frameHeight == 480)
                    _bitRate = 6000000;
                else if (_frameWidth == 320 && _frameHeight == 240)
                    _bitRate = 3000000;
                else
                {
                    Log.Warn(TAG, "Uknown bitrate calculation");
                    _bitRate = 6000000;
                }
                return _bitRate;
            }
            
        }

        public FrameDataProcessor(SortedList<float, FrameData> allframedata, int framewidth, int frameheight, int fps, int vidlengthseconds)
        {
            _allFrameData = allframedata.Select(f=> f.Value).ToList();
            _frameWidth = framewidth;
            _frameHeight = frameheight;
            _fps = fps;
            _vidlengthseconds = vidlengthseconds;
            ALLFaces = new List<BmFace>();
            beststartindexes = new List<int>();
        }

        public async Task<List<ByteBuffer>> BeginProcessingFrames()
        {
            Task<List<ByteBuffer>> t = new Task<List<ByteBuffer>>(ProcessFrames);
            t.Start();
            var result = await t;
            return result;
        }

        public async Task<bool> BeginMakeBufferVideo(List<ByteBuffer> images)
        {
            Task<bool> t = new Task<bool>(() => MakeBufferVideo(images, DateTime.Now.Ticks.ToString()));
            t.Start();
            return await t;
        }



        private List<ByteBuffer> ProcessFrames()
        {
            var coreframesavg = new Dictionary<int, double>();
            var coreframeslength = _fps * 2; //core sample of frames will be two seconds of video 
            lock (obj) //one thread at a time
            {
                try
                {
                    if (_allFrameData != null)
                    {
                        for (var i = 0; i < _allFrameData.Count - coreframeslength; i++)
                        {
                            var range = _allFrameData.GetRange(i, coreframeslength).Select(f => GetImageUsability(GetSparseFace(f._sparsearray))).ToList();

                            var avg = range.Average();

                            var sumOfSquaresOfDifferences = range.Select(val => (val - avg) * (val - avg)).Sum();

                            var stdev = Math.Sqrt(sumOfSquaresOfDifferences / range.Count);

                            coreframesavg.Add(i, avg - stdev); //avg - std dev should give those with best avg score and lowest deviation
                            //Log.Info(TAG, "moving average index" + i);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(TAG, "Frames Processing messed up", e);
                    throw;
                }
            }

            var bestframegroupindex = coreframesavg.Aggregate((l, r) => l.Value > r.Value ? l : r).Key; //gets keys of max value (avg-stdev) from dictionary
            beststartindexes.Add(bestframegroupindex); //used if video is reparsed by user.

            var frameoffset = (GetFrameTotal() - coreframeslength) / 2; //will get offset to put coreframes in middle of total frames for entire video.
                
            return _allFrameData.GetRange(bestframegroupindex - frameoffset, GetFrameTotal() - frameoffset).Select(f => f._bytebuff).ToList();
        }


        private int GetFrameTotal()
        {
            return _fps * _vidlengthseconds; //total frames will always be frames per second * number of seconds
        }

        private Face GetSparseFace(SparseArray array)
        {
            Face face = null;
            try
            {
                for (int i = 0, nsize = array.Size(); i < nsize; i++)
                {
                    Object obj = array.ValueAt(i);
                    if (obj != null && obj.GetType() == typeof(Face))
                    {
                        face = (Face) obj;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(TAG, "GetSparseFace borked somehow", e);
            }
            return face;
        }


        public bool MakeBufferVideo(List<ByteBuffer> imagesinfo, String filename)
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
                Log.Error(TAG, "MakeBufferVideo borked somehow", e);
                throw;
            }
            return true;
        }


        private float GetImageUsability(Face face)
        {
            try
            {
                if (face != null && System.Math.Abs(face.EulerY) <= 18) //forward facing
                {
                    return (face.IsSmilingProbability + face.IsRightEyeOpenProbability + face.IsLeftEyeOpenProbability) / 3;
                }
                else //if not forward facing then return 0
                {
                    return 0;
                }
            }
            catch (Exception e)
            {
                Log.Error(TAG, "GetImageUsability borked somehow", e);
                return 0;
            }
        }

    }




}
