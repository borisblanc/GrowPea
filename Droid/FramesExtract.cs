using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Java.Nio;
using Java.Security;
using Exception = System.Exception;
using File = Java.IO.File;
using String = System.String;

namespace GrowPea.Droid
{
    public class FramesExtract
    {

        private static String TAG = "EncoderMuxer";


        private static string _Filepath;

        //  parameters for the encoder
        private static String MIME_TYPE = "video/avc";

        //  H.264 Advanced Video Coding
        private static int _frameRate = 30;

        //  15fps
        private static int IFRAME_INTERVAL = 10;

        //  size of a frame, in pixels
        private int _Width = 640;

        private int _Height = 360;

        //  bit rate, in bits per second
        private int _BitRate = 10000000;

        //  encoder / muxer state
        //private MediaCodec _Decoder;

        //private CodecInputSurface mInputSurface;

        private MediaMuxer _Muxer;

        private int _TrackIndex;

        private bool _MuxerStarted;

        private static MediaCodecCapabilities _SelectedCodecColor;

        private static ImageFormatType _CameraColorFormat = ImageFormatType.Nv21;

        private static Java.IO.File _downloadsfilesdir;

        public static SortedList<long, ByteBuffer> GetFrames(Context context,  String path)
        {
            try
            {
                var bArray = new SortedList<long,ByteBuffer>();
                MediaExtractor extractor = new MediaExtractor();
                extractor.SetDataSource(path);

                int trackIndex = selectTrack(extractor);
                if (trackIndex < 0)
                {
                    throw new InvalidParameterException("FUCK you no track");
                }
                extractor.SelectTrack(trackIndex);

                //var mRetriever = new MediaMetadataRetriever();
                //mRetriever.SetDataSource(path);

                //var fps = mRetriever.ExtractMetadata(Android.Media.MetadataKey.CaptureFramerate);


                //var dur = DurationMS(mRetriever);
                //ByteBuffer inputBuffer = ByteBuffer.Allocate(1280 * 720);
                //Android.Net.Uri videoFileUri = Android.Net.Uri.Parse(path);
                //MediaPlayer mp = MediaPlayer.Create(context, videoFileUri);
                //int millis = mp.Duration;

                List<ByteBuffer> buffers = new List<ByteBuffer>();
                buffers.Add(ByteBuffer.Allocate(1280 * 720));

                //for (int i = 33333; i < dur * 1000; i += 33333)
                //{
                //    Bitmap bitmap = mRetriever.GetFrameAtTime(i, Option.Closest);
                //    bArray.Add(bitmap);
                //}
                int i = 0;
                extractor.Advance();
                while (extractor.ReadSampleData(buffers[i], 0) >= 0)
                {
                    //var bbuffer = Utils.deepCopy(inputBuffer);
                    var buffcopy = buffers[i].Duplicate();
                    //int trackIndex = extractor.getSampleTrackIndex();
                    //long presentationTimeUs = extractor.SampleTime;
                    bArray.Add(extractor.SampleTime, buffcopy);
                    buffers.Add(ByteBuffer.Allocate(1280 * 720));
                    i++;
                    extractor.Advance();

                }

                //mediaExtractor.Advance();
                //var fps = 1000000f / (float)mediaExtractor.SampleTime;
                //extractor.SeekTo(0, MediaExtractorSeekTo.None);


                return bArray;
            }
            catch (Exception e)
            {
                return null; 
                
            }
        }



        public void PrepareEncoder(string path, File _downloaddir)
        {
            MediaCodec _Decoder = null;
            MediaExtractor extractor = null;
            _downloadsfilesdir = _downloaddir;


            try {


            //for (int i = 0; i < extractor.TrackCount; i++)
            //{
            //    MediaFormat Format = extractor.GetTrackFormat(i);
            //    //MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, 640, 360);
            //    String mime = Format.GetString(MediaFormat.KeyMime);
            //    if (mime.StartsWith("video/"))
            //    {

            //            extractor.SelectTrack(i);
            //            _Decoder = MediaCodec.CreateEncoderByType(mime);

            //            _Decoder.Configure(Format, null, null, 0);
            //            break;

            //    }
            //}

            extractor = new MediaExtractor();
            extractor.SetDataSource(path);
            int trackIndex = selectTrack(extractor);
            //if (trackIndex < 0)
            //{
            //    throw new RuntimeException("No video track found in " + inputFile);
            //}
            extractor.SelectTrack(trackIndex);

            MediaFormat format = extractor.GetTrackFormat(trackIndex);

            _Width = format.GetInteger(MediaFormat.KeyWidth);
            _Height = format.GetInteger(MediaFormat.KeyHeight);


            // Could use width/height from the MediaFormat to get full-size frames.

            //outputSurface = new CodecOutputSurface(saveWidth, saveHeight);

            // Create a MediaCodec decoder, and configure it with the MediaFormat from the
            // extractor.  It's very important to use the format from the extractor because
            // it contains a copy of the CSD-0/CSD-1 codec-specific data chunks.
            String mime = format.GetString(MediaFormat.KeyMime);
            _Decoder = MediaCodec.CreateDecoderByType(mime);
            _Decoder.Configure(format, null, null, 0);
            _Decoder.Start();




   
            Decode( _Decoder, extractor);
            
                
            }
            catch (Exception e)
            {
                Log.Error(TAG, e.Message, e);
                throw;
            }
            finally
            {
                // release everything we grabbed
                //if (outputSurface != null)
                //{
                //    outputSurface.release();
                //    outputSurface = null;
                //}
                if (_Decoder != null)
                {
                    _Decoder.Stop();
                    _Decoder.Release();
                    _Decoder = null;
                }
                if (extractor != null)
                {
                    extractor.Release();
                    extractor = null;
                }
            }

            _TrackIndex = -1;
            //_MuxerStarted = false;
        }


        private static long computePresentationTime(int frameIndex)
        {
            long value = frameIndex;
            return 132 + (value * (1000000 / _frameRate));
        }

        public void Decode(MediaCodec _Decoder, MediaExtractor extractor)
        {
            Stopwatch s = new Stopwatch();
            s.Start();
            int TIMEOUT_USEC = 10000;

            ByteBuffer[] encoderInputBuffers = _Decoder.GetInputBuffers();
            ByteBuffer[] outputBuffers = _Decoder.GetOutputBuffers();
            var mBufferInfo = new MediaCodec.BufferInfo();

            bool inputDone = false;
            var index = 0;

            try
            {
                while (true)
                {
                    if (!inputDone)
                    {
                        int inputBufIndex = _Decoder.DequeueInputBuffer(TIMEOUT_USEC);
                        if (inputBufIndex >= 0)
                        {

                            ByteBuffer buffer = encoderInputBuffers[inputBufIndex];
                            //long ptsUsec = computePresentationTime(frameIndex);

                            int sampleSize = extractor.ReadSampleData(buffer, 0);

                            if (sampleSize < 0)
                            {
                                //  Send an empty frame with the end-of-stream flag set.  If we set EOS on a frame with data, that frame data will be ignored, and the output will be short one frame.
                                _Decoder.QueueInputBuffer(inputBufIndex, 0, 0, 0, MediaCodec.BufferFlagEndOfStream);
                                inputDone = true;
                                Log.Info(TAG, "sent input EOS (with zero-length frame)");
                            }
                            else
                            {
                                Log.Info(TAG, "adding encoded video to decoder input ");
                                _Decoder.QueueInputBuffer(inputBufIndex, 0, sampleSize, extractor.SampleTime, 0);
                                extractor.Advance();
                            }
          
                        }
                        else
                        {
                            //  either all in use, or we timed out during initial setup
                            Log.Warn(TAG, "input buffer not available");
                        }

                    }

                    //ByteBuffer[] encoderOutputBuffers = _Decoder.GetOutputBuffers();
                    

                    int encoderStatus = _Decoder.DequeueOutputBuffer(mBufferInfo, TIMEOUT_USEC);

                    if (encoderStatus == (int)MediaCodecInfoState.TryAgainLater)
                    {
                        Log.Info(TAG, "no output available, spinning to await EOS");
                    }
                    else if (encoderStatus == (int)MediaCodecInfoState.OutputBuffersChanged)
                    {
                        //  not expected for an encoder
                        Log.Warn(TAG, "not expected OutputBuffersChanged happened");
                        outputBuffers = _Decoder.GetOutputBuffers();
                    }
                    else if (encoderStatus == (int)MediaCodecInfoState.OutputFormatChanged)
                    {
                        //  should happen before receiving buffers, and should only happen once
                        //if (_MuxerStarted)
                        //{
                        //    Log.Error(TAG, "format changed twice and should never happen");
                        //    throw new RuntimeException("format changed twice");
                        //}

                        //MediaFormat newFormat = _Decoder.OutputFormat;

                        //Log.Info(TAG, "format changed and starting MUX");
                        //_TrackIndex = _Muxer.AddTrack(newFormat);
                        //_Muxer.Start();
                        //_MuxerStarted = true;
                    }
                    else if (encoderStatus < 0)
                    {
                        Log.Warn(TAG, "unexpected but lets ignore");
                        //  let's ignore it
                    }
                    else
                    {
                        ByteBuffer encodedData = outputBuffers[encoderStatus];
                        if (encodedData == null)
                        {
                            Log.Error(TAG, string.Format("encoderOutputBuffer {0} was null!!", encoderStatus));
                            throw new RuntimeException(string.Format("encoderOutputBuffer {0} was null!!", encoderStatus));
                        }

                        if ((mBufferInfo.Flags & MediaCodecBufferFlags.CodecConfig) != 0)
                        {
                            //  The codec config data was pulled out and fed to the muxer when we got
                            //  the INFO_OUTPUT_FORMAT_CHANGED status.  Ignore it.
                            mBufferInfo.Size = 0;
                        }

                        if (mBufferInfo.Size != 0)
                        {
                            //if (!_MuxerStarted)
                            //{
                            //    Log.Error(TAG, "muxer hasnt started!!");
                            //    throw new RuntimeException("muxer hasnt started");
                            //}

                            //  adjust the ByteBuffer values to match BufferInfo (not needed?) old
                            //encodedData.Position(mBufferInfo.Offset);
                            //encodedData.Limit(mBufferInfo.Offset + this.mBufferInfo.Size);

                            try
                            {
                                //byte[] dst = new byte[outputBuffers[encoderStatus].Capacity()];
                                //outputBuffers[encoderStatus].Get(dst);

                                //ByteBuffer buffer = outputBuffers[encoderStatus];
                                //byte[] ba = new byte[encodedData.Remaining()];
                                //encodedData.Get(ba);
                                //ByteBuffer buffer = outputBuffers[encoderStatus];
                                //buffer.Position(mBufferInfo.Offset);
                                //buffer.Limit(mBufferInfo.Offset + mBufferInfo.Size);
                                //byte[] ba = new byte[buffer.Remaining()];
                                //buffer.Get(ba);
                                //if (index < 10)
                                //{
                                    YuvImage yuv = Utils.GetYUVImage(encodedData, _CameraColorFormat, _Width, _Height);
                                    //var imagedata = yuv.GetYuvData();

                                    //Utils.swapNV21_NV12(ref imagedata, _Width, _Height);
                                    //Image might need to be corrected later

                                    //Bitmap b = Utils.GetBitmap(yuv, _Width, _Height);
                                    //Bitmap bmp = BitmapFactory.DecodeByteArray(ba, 0, ba.Length);// this return null
                                    //var createfilepath = new File(_downloadsfilesdir, DateTime.Now.Ticks + ".png").AbsolutePath;
                                    //using (FileStream bos = new FileStream(createfilepath, FileMode.CreateNew))
                                    //{
                                    //    b.Compress(Bitmap.CompressFormat.Png, 100, bos);
                                    //}
                                    //b.Recycle();
                                //}
                                index++;
                                //writeFrameToSDCard(dst, i, dst.length);
                                //i++;
                            }
                            catch (Exception e)
                            {
                                //Log("iDecodeActivity", "Error while creating bitmap with: ");
                            }

                            _Decoder.ReleaseOutputBuffer(encoderStatus, false);
                        }


                        if ((mBufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                        {
                            Log.Info(TAG, "End of Stream Reached!!");
                            break;
                        }

                    }

                }

                s.Stop();
                Log.Info("inner STOPWATCH!!!!:", string.Format("numberofframes = {0}, totaltime = {1}", index, s.ElapsedMilliseconds));
            }
            catch (Exception e)
            {
                Log.Error(TAG, "Decode or Muxer failed", e, e.Message);
                throw;
            }
        }

        private static int DurationMS(MediaMetadataRetriever retriever)
        {
            int duration = 0;
            String dur = retriever.ExtractMetadata(MetadataKey.Duration);
            if (dur != null)
            {
                duration = int.Parse(dur);
            }
            //long h = duration / 3600;
            //long m = (duration - h * 3600) / 60;
            //long s = duration - (h * 3600 + m * 60);

            return duration;
        }

        private static int FPS(MediaMetadataRetriever retriever)
        {
            int duration = 0;
            String dur = retriever.ExtractMetadata(MetadataKey.CaptureFramerate);
            if (dur != null)
            {
                duration = int.Parse(dur);
            }
            //long h = duration / 3600;
            //long m = (duration - h * 3600) / 60;
            //long s = duration - (h * 3600 + m * 60);

            return duration;
        }


        private static int selectTrack(MediaExtractor extractor)
        {
            // Select the first video track we find, ignore the rest.
            int numTracks = extractor.TrackCount;
            for (int i = 0; i < numTracks; i++)
            {
                MediaFormat format = extractor.GetTrackFormat(i);
                String mime = format.GetString(MediaFormat.KeyMime);
                if (mime.StartsWith("video/"))
                {
                    return i;
                }
            }
            return -1;
        }



    }  

}



