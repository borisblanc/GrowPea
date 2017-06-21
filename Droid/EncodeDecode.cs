using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.Annotation;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
//using Java.IO;
using Java.Lang;
using Java.Nio;
using Exception = System.Exception;
using String = System.String;
using System.Diagnostics;
using Java.IO;
using Java.Util;
using Org.Opencv.Core;
using Org.Opencv.Highgui;
using Org.Opencv.Imgproc;


//using Java.IO;

namespace GrowPea.Droid
{
    public class EncodeDecode
    {

        private static String TAG = "EncodeDecode";

        private static bool VERBOSE = false;

        //  lots of logging  
        //  parameters for the encoder  
        private static String MIME_TYPE = "video/avc";

        //  H.264 Advanced Video  
        //  Coding  
        private static int FRAME_RATE = 10;

        //  10fps  
        private static int IFRAME_INTERVAL = 10;

        //  10 seconds between  
        //  I-frames  
        //  size of a frame, in pixels  
        private int mWidth = -1;

        private int mHeight = -1;

        //  bit rate, in bits per second  
        private int mBitRate = -1;

        //  largest color component delta seen (i.e. actual vs. expected)  
        private int mLargestColorDelta;

        private File _outputFile = null;

        private MediaCodec mEncoder;

        private MediaMuxer mMuxer;

        private int mTrackIndex;

        private bool mMuxerStarted;

        private List<File> _frames;

        public EncodeDecode(List<File> frames, File outputFile)
        {
            this._frames = frames;
            this._outputFile = outputFile;
        }

        public bool encodeDecodeVideoFromBufferToSurface(int width, int height, int bitRate)
        {
            setParameters(width, height, bitRate);
            return encodeDecodeVideoFromBuffer();
        }

        private void setParameters(int width, int height, int bitRate)
        {
            if ((width % 16 != 0) || (height % 16 != 0))
            {
                System.Diagnostics.Debug.WriteLine("WARNING: width or height not multiple of 16");
            }

            this.mWidth = width;
            this.mHeight = height;
            this.mBitRate = bitRate;
        }


        public bool encodeDecodeVideoFromBuffer()
        {
            mLargestColorDelta = -1;
            bool result = true;
            try
            {
                MediaCodecInfo codecInfo = selectCodec(MIME_TYPE);
                if ((codecInfo == null))
                {
                    //  Don't fail CTS if they don't have an AVC codec  
                    System.Diagnostics.Debug.WriteLine("Unable to find an appropriate codec for " + MIME_TYPE);
                    return false;
                }

                if (VERBOSE)
                {
                    System.Diagnostics.Debug.WriteLine("found codec: " + codecInfo.Name);
                }

                int colorFormat;
                try
                {
                    colorFormat = selectColorFormat(codecInfo, MIME_TYPE);
                }
                catch (Exception)
                {

                    colorFormat = (int)MediaCodecCapabilities.Formatyuv420semiplanar;
                }

                if (VERBOSE)
                {
                    System.Diagnostics.Debug.WriteLine("found colorFormat: " + colorFormat);
                }

                //  We avoid the device-specific limitations on width and height by using values that  
                //  are multiples of 16, which all tested devices seem to be able to handle.  
                MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, this.mWidth, this.mHeight);

                //  Set some properties. Failing to specify some of these can cause the MediaCodec  
                //  configure() call to throw an unhelpful exception.  
                format.SetInteger(MediaFormat.KeyColorFormat, colorFormat);
                format.SetInteger(MediaFormat.KeyBitRate, this.mBitRate);
                format.SetInteger(MediaFormat.KeyFrameRate, FRAME_RATE);
                format.SetInteger(MediaFormat.KeyIFrameInterval, IFRAME_INTERVAL);

                if (VERBOSE)
                {
                    System.Diagnostics.Debug.WriteLine("format: " + format);
                }

                //  Create a MediaCodec for the desired codec, then configure it as an encoder with our desired properties.  
                mEncoder = MediaCodec.CreateByCodecName(codecInfo.Name);
                mEncoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);
                mEncoder.Start();

                //  Create a MediaCodec for the decoder, just based on the MIME type.  
                //  The various format details will be passed through the csd-0 meta-data later on.  
                String outputPath = this._outputFile.AbsolutePath;
                try
                {
                    mMuxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
                }
                catch (IOException ioe)
                {
                    //  throw new RuntimeException("MediaMuxer creation failed",  
                    //  ioe);  
                    ioe.PrintStackTrace();
                }

                result = doEncodeDecodeVideoFromBuffer(mEncoder, colorFormat);
            }
            finally
            {
                if (this.mEncoder != null)
                {
                    mEncoder.Stop();
                    mEncoder.Release();
                }

                if (mMuxer != null)
                {
                    mMuxer.Stop();
                    mMuxer.Release();
                }

                if (VERBOSE)
                {
                    System.Diagnostics.Debug.WriteLine("Largest color delta: " + this.mLargestColorDelta);
                }

            }

            return result;
        }

        private static MediaCodecInfo selectCodec(String mimeType)
        {
            int numCodecs = MediaCodecList.CodecCount;
            for (int i = 0; (i < numCodecs); i++)
            {
                MediaCodecInfo codecInfo = MediaCodecList.GetCodecInfoAt(i);
                if (!codecInfo.IsEncoder)
                {
                    // TODO: Warning!!! continue If
                }

                String[] types = codecInfo.GetSupportedTypes();
                for (int j = 0; (j < types.Length); j++)
                {
                    if (types[j].ToLower().Equals(mimeType.ToLower()))
                    {
                        return codecInfo;
                    }

                }

            }

            return null;
        }

        private static int selectColorFormat(MediaCodecInfo codecInfo, String mimeType)
        {
            MediaCodecInfo.CodecCapabilities capabilities = codecInfo.GetCapabilitiesForType(mimeType);
            for (int i = 0; (i < capabilities.ColorFormats.Count); i++)
            {
                int colorFormat = capabilities.ColorFormats[i];
                if (isRecognizedFormat(colorFormat))
                {
                    return colorFormat;
                }

            }

            return 0;
            //  not reached  
        }

        private static bool isRecognizedFormat(int colorFormat)
        {
            switch (colorFormat)
            {
                case (int)MediaCodecCapabilities.Formatyuv420planar:
                case (int)MediaCodecCapabilities.Formatyuv420packedplanar:
                case (int)MediaCodecCapabilities.Formatyuv420semiplanar:
                case (int)MediaCodecCapabilities.Formatyuv420packedsemiplanar:
                case (int)MediaCodecCapabilities.TiFormatyuv420packedsemiplanar:
                    return true;
                default:
                    return false;
            }
        }

        private bool doEncodeDecodeVideoFromBuffer(MediaCodec encoder, int encoderColorFormat)
        {
            int TIMEOUT_USEC = 10000;
            ByteBuffer[] encoderInputBuffers = encoder.GetInputBuffers();
            MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
            int generateIndex = 0;
            //  yuv format  
            byte[] frameData = new byte[this.mWidth * this.mHeight * (3 / 2)];
            //  Loop until the output side is done.  
            bool inputDone = false;

            //  If we're not done submitting frames, generate a new one and submit it. By  
            //  doing this on every loop we're working to ensure that the encoder  always has work to do.  
            while (!inputDone)
            {
                int inputBufIndex = encoder.DequeueInputBuffer(TIMEOUT_USEC);
                if ((inputBufIndex >= 0))
                {
                    long ptsUsec = computePresentationTime(generateIndex);
                    if (generateIndex >= this._frames.Count)
                    {
                        //  Send an empty frame with the end-of-stream flag set. If  we set EOS  
                        //  on a frame with data, that frame data will be ignored,  and the output will be short one frame.  

                        encoder.QueueInputBuffer(inputBufIndex, 0, 0, ptsUsec, MediaCodecBufferFlags.EndOfStream);
                        inputDone = true;
                        drainEncoder(true, info);
                    }
                    else
                    {
                        try
                        {
                            generateFrame(generateIndex, frameData);
                        }
                        catch (Exception)
                        {
                            System.Diagnostics.Debug.WriteLine("meet a different type of image");
                            Arrays.Fill(frameData, 0);
                        }

                        if (VERBOSE)
                        {
                            System.Diagnostics.Debug.WriteLine("generateIndex: " + (generateIndex + (", size: " + _frames.Count)));
                        }

                        ByteBuffer inputBuf = encoderInputBuffers[inputBufIndex];

                        //  the buffer should be sized to hold one full frame  
                        inputBuf.Clear();
                        inputBuf.Put(frameData);
                        encoder.QueueInputBuffer(inputBufIndex, 0, frameData.Length, ptsUsec, 0);
                        drainEncoder(false, info);
                    }

                    generateIndex++;
                }
                else
                {
                    //  either all in use, or we timed out during initial setup  
                    if (VERBOSE)
                    {
                        System.Diagnostics.Debug.WriteLine("input buffer not available");
                    }

                }

            }

            return true;
        }

        private void drainEncoder(bool endOfStream, MediaCodec.BufferInfo mBufferInfo)
        {
            int TIMEOUT_USEC = 10000;
            if (endOfStream)
            {
                try
                {
                    mEncoder.SignalEndOfInputStream();
                }
                catch 
                {

                }

            }

            ByteBuffer[] encoderOutputBuffers = mEncoder.GetOutputBuffers();
            while (true)
            {
                int encoderStatus = mEncoder.DequeueOutputBuffer(mBufferInfo, TIMEOUT_USEC);
                if (encoderStatus == (int)MediaCodecInfoState.TryAgainLater)
                {
                    //  no output available yet  
                    if (!endOfStream)
                    {
                        break;
                        //  out of while  
                    }

                    if (VERBOSE)
                    {
                        System.Diagnostics.Debug.WriteLine("no output available, spinning to await EOS");
                    }

                }
                else if (encoderStatus == (int)MediaCodecInfoState.OutputBuffersChanged)
                {
                    //  not expected for an encoder  
                    encoderOutputBuffers = mEncoder.GetOutputBuffers();
                }
                else if (encoderStatus == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    //  should happen before receiving buffers, and should only happen once  
                    if (mMuxerStarted)
                    {
                        throw new RuntimeException("format changed twice");
                    }

                    //BORIS MODIFIED!!might need to fix this since it was not collection before my port!!!!!!!!!
                    MediaFormat newFormat = this.mEncoder.GetOutputFormat(0);
                    //BORIS MODIFIED!!might need to fix this since it was not collection before my port!!!!!!!!!

                    if (VERBOSE)
                    {
                        System.Diagnostics.Debug.WriteLine("encoder output format changed: " + newFormat);
                    }

                    //  now that we have the Magic Goodies, start the muxer  
                    mTrackIndex = mMuxer.AddTrack(newFormat);
                    mMuxer.Start();
                    mMuxerStarted = true;
                }
                else if (encoderStatus < 0)
                {
                    if (VERBOSE)
                    {
                        System.Diagnostics.Debug.WriteLine("unexpected result from encoder.dequeueOutputBuffer: " + encoderStatus);
                    }

                }
                else
                {
                    ByteBuffer encodedData = encoderOutputBuffers[encoderStatus];
                    if ((encodedData == null))
                    {
                        throw new RuntimeException("encoderOutputBuffer " + (encoderStatus + " was null"));
                    }
                    
                    if ((mBufferInfo.Flags & MediaCodecBufferFlags.CodecConfig) != 0)
                    {
                        //  The codec config data was pulled out and fed to the muxer when we got  
                        //  the INFO_OUTPUT_FORMAT_CHANGED status. Ignore it.  
                        if (VERBOSE)
                        {
                            System.Diagnostics.Debug.WriteLine("ignoring BUFFER_FLAG_CODEC_CONFIG");
                        }

                        mBufferInfo.Size = 0;
                    }

                    if (mBufferInfo.Size != 0)
                    {
                        if (!mMuxerStarted)
                        {
                            throw new RuntimeException("muxer hasn\'t started");
                        }

                        //  adjust the ByteBuffer values to match BufferInfo  
                        encodedData.Position(mBufferInfo.Offset);
                        encodedData.Limit(mBufferInfo.Offset + mBufferInfo.Size);
                        if (VERBOSE)
                        {
                            System.Diagnostics.Debug.WriteLine("BufferInfo: " + (mBufferInfo.Offset + (","+ (mBufferInfo.Size + ("," + mBufferInfo.PresentationTimeUs)))));
                        }

                        try
                        {
                            mMuxer.WriteSampleData(mTrackIndex, encodedData, mBufferInfo);
                        }
                        catch (Exception e)
                        {
                            System.Diagnostics.Debug.WriteLine("Too many frames");
                        }

                    }

                    mEncoder.ReleaseOutputBuffer(encoderStatus, false);

                    if ((mBufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                    {
                        if (!endOfStream)
                        {
                            if (VERBOSE)
                            {
                                System.Diagnostics.Debug.WriteLine("reached end of stream unexpectedly");
                            }

                        }
                        else if (VERBOSE)
                        {
                            System.Diagnostics.Debug.WriteLine("end of stream reached");
                        }

                        break;
                        //  out of while  
                    }

                }

            }

        }

        private void generateFrame(int frameIndex, byte[] frameData)
        {
            //Set to zero.In YUV this is a dull green.
            Arrays.Fill(frameData, 0);
            Mat mat = Highgui.Imread(_frames[frameIndex].AbsolutePath);
            //       Mat dst = new Mat(mWidth, mHeight * 3 / 2, CvType.CV_8UC1);  
            Mat dst = new Mat();
            Imgproc.CvtColor(mat, dst, Imgproc.ColorRgba2yuvI420);

            //  use array instead of mat to improve the speed  
            dst.Get(0, 0, frameData);

            byte[] temp = frameData.ToArray();
            int margin = this.mHeight / 4;
            int location = this.mHeight;
            int step = 0;
            for (int i = this.mHeight; (i < this.mHeight + margin); i++)
            {
                for (int j = 0; (j < this.mWidth); j++)
                {
                    byte uValue = temp[(i * this.mWidth) + j];
                    byte vValue = temp[((i + margin) * this.mWidth) + j];
                    frameData[(location * this.mWidth) + step] = uValue;
                    frameData[(location * this.mWidth) + (step + 1)] = vValue;
                    step += 2;
                    if (step >= this.mWidth)
                    {
                        location++;
                        step = 0;
                    }

                }

            }

        }

        private static long computePresentationTime(int frameIndex)
        {
            long value = frameIndex;
            return (132 + (value * (1000000 / FRAME_RATE)));
        }
    }
}