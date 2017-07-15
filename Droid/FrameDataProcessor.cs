using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Android.Gms.Vision.Faces;
using Android.Util;
using Java.Nio;
using System.Threading.Tasks;
using Exception = System.Exception;
using Object = System.Object;
using String = System.String;
using Android.Graphics;


namespace GrowPea.Droid
{
    public class FrameDataProcessor
    {
        private static readonly string TAG = "FrameDataProcessor";


        private List<FrameData> _allFrameData;
        private SortedList<long, SparseArray> _framelist;

        private readonly int _frameWidth;
        private readonly int _frameHeight;

        private static readonly Object obj = new Object();

        private int _bitRate;

        private int _fps; //todo need to get this from video not user setting

        private int _vidlengthseconds;

        //public List<int> beststartindexes;

        public SortedList<long, double> Coreframesavg;

        public Tuple<long, long> bestTSRange;


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

        //public FrameDataProcessor(ref SortedList<float, FrameData> allframedata, int framewidth, int frameheight, int fps, int vidlengthseconds) //old
        //{
        //    _allFrameData = allframedata.Select(f => f.Value).ToList();
        //    _frameWidth = framewidth;
        //    _frameHeight = frameheight;
        //    _fps = fps;
        //    _vidlengthseconds = vidlengthseconds;
        //    beststartindexes = new List<int>();
        //}

        public FrameDataProcessor(ref SortedList<long, SparseArray> framelist,  int fps, int vidlengthseconds)
        {
            _framelist = framelist;
            _fps = fps;
            _vidlengthseconds = vidlengthseconds;
            Coreframesavg = new SortedList<long, double>();
        }

        //public async Task<List<byte[]>> BeginProcessingFrames()
        //{
        //    Task<List<byte[]>> t = new Task<List<byte[]>>(ProcessFrames);
        //    t.Start();
        //    var result = await t;
        //    return result;
        //}

        public async Task<Tuple<long, long>> BeginFramesProcess()
        {
            Task<Tuple<long, long>> t = new Task<Tuple<long, long>>(ProcessAllFrames);
            t.Start();
            var result = await t;
            return result;
        }

        public async Task<string> BeginMakeBufferVideo(List<byte[]> images)
        {
            Task<string> t = new Task<string>(() => MakeBufferVideo(images, DateTime.Now.Ticks.ToString()));
            t.Start();
            return await t;
        }


        //returns tuple of start and end timestamps for best frame range to splice from existing mp4
        private Tuple<long, long> ProcessAllFrames()
        {
            var coreframeslength = _fps * 2; //core sample of frames will be two seconds of video might in future vary depending on user settings
            var computelimit = _framelist.Count - coreframeslength; //this will keep walking average calcs only happening within range

            try
            {
                if (_framelist != null)
                {
                    for (var i = 0; i < _framelist.Count; i++)
                    {
                        var currenttimestamp = _framelist.Keys[i];

                        if (i < computelimit) //makes sense to compute because we will can use it
                        {
                            var coreframesrange = _framelist.ToList().GetRange(i, coreframeslength); 

                            var listofscores = new SortedList<long, double>();
                            foreach (var kvp in coreframesrange)
                            {
                                listofscores.Add(kvp.Key, kvp.Value.Size() != 0 ? GetImageUsability(Utils.GetSparseFace(kvp.Value)) : -1); //return -1 for usabilty if no face exists
                            }

                            var avg = listofscores.Average(x => x.Value);

                            var sumOfSquaresOfDifferences = listofscores.ToList().Select(val => (val.Value - avg) * (val.Value - avg)).Sum();

                            var stdev = Math.Sqrt(sumOfSquaresOfDifferences / coreframesrange.Count);

                            Coreframesavg.Add(currenttimestamp, avg - stdev); //avg - std dev should give those with best avg score and lowest deviation 
                        }
                        else //can't use computations past this point so just need timestamps
                        {
                            Coreframesavg.Add(currenttimestamp, 0); //avg - std dev should give those with best avg score and lowest deviation 
                        }

                        //Log.Info(TAG, "moving average index" + i);
                    }
                }

                var bestframegroupkey = Coreframesavg.Aggregate((l, r) => l.Value > r.Value ? l : r).Key; //gets key of max value (avg-stdev) from dictionary

                bestTSRange = new Tuple<long, long>(bestframegroupkey, Coreframesavg.Keys[Coreframesavg.IndexOfKey(bestframegroupkey) + GetFrameTotal()] ); 

                return bestTSRange;
            }
            catch (Exception e)
            {
                Log.Error(TAG, "Frames Processing messed up", e);
                throw;
            }   
        }

        public Task<bool> ReProcessAllFrames()
        {
            if (Coreframesavg != null && Coreframesavg.Count > 0 && bestTSRange != null)
            {
                try
                {
                    var oldlistframes = Coreframesavg.ToList();
                    var oldbestrange = bestTSRange;

                    oldlistframes.RemoveRange(Coreframesavg.IndexOfKey(oldbestrange.Item1) - 5, 10);

                    var bestframegroupkey = oldlistframes.Aggregate((l, r) => l.Value > r.Value ? l : r).Key; //compute new best

                    bestTSRange = new Tuple<long, long>(bestframegroupkey, oldlistframes[Coreframesavg.IndexOfKey(bestframegroupkey) + GetFrameTotal()].Key);
                    var dict = oldlistframes.ToDictionary(x => x.Key, x => x.Value);
                    Coreframesavg = new SortedList<long, double>(dict);

                    return Task.FromResult(true);
                }
                catch (Exception e)
                {
                    return Task.FromResult(false);
                }
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        private Tuple<long, long> ProcessAllFramesold()
        {
            var coreframeslength = _fps * 2; //core sample of frames will be two seconds of video 

            try
            {
                if (_framelist != null)
                {
                    for (var i = 0; i < _framelist.Count - coreframeslength; i++)
                    {
                        var currenttimestamp = _framelist.Keys[i];
                        var range = _framelist.ToList().GetRange(i, coreframeslength);
                        var listofscores = new SortedList<long, double>();
                        foreach (var kvp in range)
                        {
                            listofscores.Add(kvp.Key, kvp.Value.Size() != 0 ? GetImageUsability(Utils.GetSparseFace(kvp.Value)) : -1); //return -1 for usabilty if no face exists
                        }

                        var avg = listofscores.Average(x => x.Value);

                        var sumOfSquaresOfDifferences = listofscores.ToList().Select(val => (val.Value - avg) * (val.Value - avg)).Sum();

                        var stdev = Math.Sqrt(sumOfSquaresOfDifferences / range.Count);

                        Coreframesavg.Add(currenttimestamp, avg - stdev); //avg - std dev should give those with best avg score and lowest deviation //todo can make this leaner later
                        //Log.Info(TAG, "moving average index" + i);
                    }
                }

                var bestframegroupkey = Coreframesavg.Aggregate((l, r) => l.Value > r.Value ? l : r).Key; //gets key of max value (avg-stdev) from dictionary

                var frameoffset = coreframeslength / 2; //will get offset to put coreframes in middle of total frames for entire video.

                if (frameoffset > Coreframesavg.IndexOfKey(bestframegroupkey)) //fix for when best frame is at beggining of video
                    frameoffset = 0;

                var bestlist = Coreframesavg.ToList().GetRange(Coreframesavg.IndexOfKey(bestframegroupkey) - frameoffset, GetFrameTotal());//.ToDictionary(x => x.Key, y => y.Value); 

                bestTSRange = new Tuple<long, long>(bestlist.Select(x => x.Key).First(), bestlist.Select(x => x.Key).Last());

                return bestTSRange;
            }
            catch (Exception e)
            {
                Log.Error(TAG, "Frames Processing messed up", e);
                throw;
            }

        }




        //private List<byte[]> ProcessFrames()
        //{
        //    var coreframesavg = new Dictionary<int, double>();
        //    var coreframeslength = _fps * 2; //core sample of frames will be two seconds of video 
        //    lock (obj) //one thread at a time
        //    {
        //        try
        //        {
        //            if (_allFrameData != null)
        //            {
        //                for (var i = 0; i < _allFrameData.Count - coreframeslength; i++)
        //                {
        //                    var range = _allFrameData.GetRange(i, coreframeslength).Select(f => GetImageUsability(Utils.GetSparseFace(f._sparsearray))).ToList();

        //                    var avg = range.Average();

        //                    var sumOfSquaresOfDifferences = range.Select(val => (val - avg) * (val - avg)).Sum();

        //                    var stdev = Math.Sqrt(sumOfSquaresOfDifferences / range.Count);

        //                    coreframesavg.Add(i, avg - stdev); //avg - std dev should give those with best avg score and lowest deviation
        //                    //Log.Info(TAG, "moving average index" + i);
        //                }
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            Log.Error(TAG, "Frames Processing messed up", e);
        //            throw;
        //        }
        //    }

        //    var bestframegroupindex = coreframesavg.Aggregate((l, r) => l.Value > r.Value ? l : r).Key; //gets keys of max value (avg-stdev) from dictionary
        //    beststartindexes.Add(bestframegroupindex); //used if video is reparsed by user.

        //    var frameoffset = (GetFrameTotal() - coreframeslength) / 2; //will get offset to put coreframes in middle of total frames for entire video.

        //    if (frameoffset > bestframegroupindex) //fix for when best frame is at beggining of video
        //        frameoffset = 0;


        //    return _allFrameData.GetRange(bestframegroupindex - frameoffset, GetFrameTotal()).Select(f => f._yuv).ToList();
        //}


        private int GetFrameTotal()
        {
            return _fps * _vidlengthseconds; //total frames will always be frames per second * number of seconds
        }




        public string MakeBufferVideo(List<byte[]> imagesinfo, String filename)
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
            return outputfilepath;
        }


        private float GetImageUsability(Face face)
        {
            try
            {
                if (face != null && System.Math.Abs(face.EulerY) <= 18) //forward facing
                {
                    return ((face.IsSmilingProbability < 0 ? 0 : face.IsSmilingProbability) + //if not smiling make that less important by returning 0 instead of -1
                        face.IsRightEyeOpenProbability + face.IsLeftEyeOpenProbability) / 3; 
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
