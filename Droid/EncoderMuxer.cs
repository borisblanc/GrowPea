using System;
using System.Collections.Generic;
using System.IO;
using Android.Graphics;
using Android.Media;
using Android.Opengl;
using Android.Test;
using Android.Util;
using Android.Views;
using Java.Lang;
using Java.Nio;
using Environment = Android.OS.Environment;
using Exception = System.Exception;
using File = Java.IO.File;
using IOException = Java.IO.IOException;
using String = System.String;

// 20131106: removed hard-coded "/sdcard"
// 20131205: added alpha to EGLConfig



namespace GrowPea.Droid
{
    public class EncoderMuxer //: AndroidTestCase
    {

    private static String TAG = "EncoderMuxer";


    private static string _Filepath;

    //  parameters for the encoder
    private static String MIME_TYPE = "video/avc";

    //  H.264 Advanced Video Coding
    private static int FRAME_RATE = 10;

    //  15fps
    private static int IFRAME_INTERVAL = 10;

    //  10 seconds between I-frames
    //private static int NUM_FRAMES = 30;

    //  two seconds of video
    //  RGB color values for generated frames
    private static int TEST_R0 = 0;

    private static int TEST_G0 = 136;

    private static int TEST_B0 = 0;

    private static int TEST_R1 = 236;

    private static int TEST_G1 = 50;

    private static int TEST_B1 = 186;

    //  size of a frame, in pixels
    private int _Width = -1;

    private int _Height = -1;

    //  bit rate, in bits per second
    private int _BitRate = -1;

    //  encoder / muxer state
    private MediaCodec _Encoder;

    //private CodecInputSurface mInputSurface;

    private MediaMuxer _Muxer;

    private int _TrackIndex;

    private bool _MuxerStarted;

    //  allocate one of these up front so we don't need to do it every time
    private MediaCodec.BufferInfo mBufferInfo;

    private List<ByteBuffer> _ByteBuffers;

    public EncoderMuxer(int width, int height, int bitRate, string oFilePath, List<ByteBuffer> byteBuffers)
    {
        if ((width % 16) != 0 || (height % 16) != 0)
        {
            Log.Warn(TAG, "WARNING: width or height not multiple of 16");
        }
        _Width = width;
        _Height = height;
        _BitRate = bitRate;
        _Filepath = oFilePath;
        _ByteBuffers = byteBuffers;
    }

    public void EncodeVideoToMp4()
    {
        try
        {
            PrepareEncoder();
            //  Feed any pending encoder output into the muxer.
            EncodeMux();
            //  Submit it to the encoder.  The eglSwapBuffers call will block if the input
            //  is full, which would be bad if it stayed full until we dequeued an output
            //  buffer (which we can't do, since we're stuck here).  So long as we fully drain
            //  the encoder before supplying additional input, the system guarantees that we
            //  can supply another frame without blocking.
        }
        catch (Exception e)
        {
            Log.Error(TAG, "Encoder & Mux failed", e);
        }
        finally
        {
            //  release encoder, muxer, and input Surface
            releaseEncoder();
        }

    //  To test the result, open the file with MediaExtractor, and get the format.  Pass
    //  that into the MediaCodec decoder configuration, along with a SurfaceTexture surface,
    //  and examine the output with glReadPixels.
    }

    private void PrepareEncoder()
    {
        mBufferInfo = new MediaCodec.BufferInfo();
        MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, _Width, _Height);

        MediaCodecInfo codecInfo = selectCodec(MIME_TYPE);

        int colorFormat = selectColorFormat(codecInfo, MIME_TYPE);

        format.SetInteger(MediaFormat.KeyColorFormat, colorFormat);
        format.SetInteger(MediaFormat.KeyBitRate, _BitRate);
        format.SetInteger(MediaFormat.KeyFrameRate, FRAME_RATE);
        format.SetInteger(MediaFormat.KeyIFrameInterval, IFRAME_INTERVAL);


        _Encoder = MediaCodec.CreateEncoderByType(MIME_TYPE);
        _Encoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);

        _Encoder.Start();

        //  Create a MediaMuxer.  We can't add the video track and start() the muxer here,
        //  because our MediaFormat doesn't have the Magic Goodies.  These can only be
        //  obtained from the encoder after it has started processing data.
        try
        {
            _Muxer = new MediaMuxer(_Filepath, MuxerOutputType.Mpeg4);
        }
        catch (IOException e)
        {
            Log.Error(TAG, e.Message, e);
            throw new RuntimeException("MediaMuxer creation failed", e);
        }

        _TrackIndex = -1;
        _MuxerStarted = false;
    }

    private static MediaCodecInfo selectCodec(String mimeType)
    {
        int numCodecs = MediaCodecList.CodecCount;
        for (int i = 0; i < numCodecs; i++)
        {
            MediaCodecInfo codecInfo = MediaCodecList.GetCodecInfoAt(i);
            if (!codecInfo.IsEncoder)
            {
                continue;
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

        Log.Warn(TAG, string.Format("couldn\'t find a good color format for codec {0} and mime {1}",codecInfo.Name, mimeType));
        return 0;
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

    private void releaseEncoder()
    {
        if (_Encoder != null)
        {
            _Encoder.Stop();
            _Encoder.Release();
            _Encoder = null;
        }

        if (_Muxer != null)
        {
            _Muxer.Stop();
            _Muxer.Release();
            _Muxer = null;
        }
    }

    private static long computePresentationTime(int frameIndex)
    {
        long value = frameIndex;
        return 132 + (value * (1000000 / FRAME_RATE));
    }

    private void EncodeMux()
    {
        int TIMEOUT_USEC = 10000;

        ByteBuffer[] encoderInputBuffers = _Encoder.GetInputBuffers();

        bool inputDone = false;
        int frameIndex = 0;
        try
        {
            while (true)
            {
                if (!inputDone)
                {
                    int inputBufIndex = _Encoder.DequeueInputBuffer(TIMEOUT_USEC);
                    if (inputBufIndex >= 0)
                    {
                        long ptsUsec = computePresentationTime(frameIndex);
                        if (frameIndex == _ByteBuffers.Count)
                        {
                            //  Send an empty frame with the end-of-stream flag set.  If we set EOS on a frame with data, that frame data will be ignored, and the output will be short one frame.
                            _Encoder.QueueInputBuffer(inputBufIndex, 0, 0, ptsUsec, MediaCodec.BufferFlagEndOfStream);
                            inputDone = true;
                            Log.Info(TAG, "sent input EOS (with zero-length frame)");
                        }
                        else
                        {
                            Log.Info(TAG, string.Format("Adding _ByteBuffers image index {0} to encoder", frameIndex));
                            ByteBuffer inputBuf = encoderInputBuffers[inputBufIndex];
                            var imagedata = _ByteBuffers[frameIndex];
                            int chunkSize = 0;

                            if (imagedata == null)
                            {
                                Log.Warn(TAG, string.Format("Adding _ByteBuffers image index {0} to encoder", frameIndex));
                            }
                            else
                            {
                                Bitmap b = GetBitmap(imagedata);

                                byte[] yuv = new byte[b.Width * b.Height * 3 / 2];
                                int[] argb = new int[b.Width * b.Height];

                                b.GetPixels(argb, 0, b.Width, 0, 0, b.Width, b.Height);
                                encodeYUV420SP(yuv, argb, b.Width, b.Height);

                                b.Recycle();
                                inputBuf.Put(yuv);
                                chunkSize = yuv.Length;
                            }


                            //  the buffer should be sized to hold one full frame
                            inputBuf.Clear();
                            _Encoder.QueueInputBuffer(inputBufIndex, 0, chunkSize, ptsUsec, 0);
                            frameIndex++;
                        }
                    }
                    else
                    {
                        //  either all in use, or we timed out during initial setup
                        Log.Warn(TAG, "input buffer not available");
                    }

                }

                ByteBuffer[] encoderOutputBuffers = _Encoder.GetOutputBuffers();

                int encoderStatus = _Encoder.DequeueOutputBuffer(mBufferInfo, TIMEOUT_USEC);

                if (encoderStatus == (int)MediaCodecInfoState.TryAgainLater)
                {
                    Log.Info(TAG, "no output available, spinning to await EOS");
                }
                else if (encoderStatus == (int)MediaCodecInfoState.OutputBuffersChanged)
                {
                    //  not expected for an encoder
                    Log.Warn(TAG, "not expected OutputBuffersChanged happened");
                    encoderOutputBuffers = _Encoder.GetOutputBuffers();
                }
                else if (encoderStatus == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    //  should happen before receiving buffers, and should only happen once
                    if (_MuxerStarted)
                    {
                        Log.Error(TAG, "format changed twice and should never happen");
                        throw new RuntimeException("format changed twice");
                    }

                    MediaFormat newFormat = _Encoder.OutputFormat;

                    Log.Info(TAG, "format changed and starting MUX");
                    _TrackIndex = _Muxer.AddTrack(newFormat);
                    _Muxer.Start();
                    _MuxerStarted = true;
                }
                else if (encoderStatus < 0)
                {
                    Log.Warn(TAG, "unexpected but lets ignore");
                    //  let's ignore it
                }
                else
                {
                    ByteBuffer encodedData = encoderOutputBuffers[encoderStatus];
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
                        if (!_MuxerStarted)
                        {
                            Log.Error(TAG, "muxer hasnt started!!");
                            throw new RuntimeException("muxer hasnt started");
                        }

                        //  adjust the ByteBuffer values to match BufferInfo (not needed?)
                        encodedData.Position(mBufferInfo.Offset);
                        encodedData.Limit(mBufferInfo.Offset + this.mBufferInfo.Size);

                        _Muxer.WriteSampleData(_TrackIndex, encodedData, mBufferInfo);
                        Log.Info(TAG, string.Format("{0} bytes to muxer", mBufferInfo.Size));
                    }

                    _Encoder.ReleaseOutputBuffer(encoderStatus, false);
                    if ((mBufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                    {
                        Log.Info(TAG, "End of Stream Reached!!");
                        break;
                    }

                }

            }
        }
        catch (Exception e)
        {
            Log.Error(TAG, "Decode or Muxer failed", e, e.Message);
            throw new RuntimeException("Decode or Muxer failed");
        }
    }

    private void encodeYUV420SP(byte[] yuv420sp, int[] argb, int width, int height)
    {
        int frameSize = width * height;
        
        int yIndex = 0;
        int uvIndex = frameSize;

        int R, G, B, Y, U, V;
        int index = 0;
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {

                R = (argb[index] & 0xff0000) >> 16;
                G = (argb[index] & 0xff00) >> 8;
                B = (argb[index] & 0xff) >> 0;

                // well known RGB to YUV algorithm
                Y = ((66 * R + 129 * G + 25 * B + 128) >> 8) + 16;
                U = ((-38 * R - 74 * G + 112 * B + 128) >> 8) + 128;
                V = ((112 * R - 94 * G - 18 * B + 128) >> 8) + 128;

                // NV21 has a plane of Y and interleaved planes of VU each sampled by a factor of 2
                //    meaning for every 4 Y pixels there are 1 V and 1 U.  Note the sampling is every other
                //    pixel AND every other scanline.
                yuv420sp[yIndex++] = (byte)((Y < 0) ? 0 : ((Y > 255) ? 255 : Y));
                if (j % 2 == 0 && index % 2 == 0)
                {
                    yuv420sp[uvIndex++] = (byte)((V < 0) ? 0 : ((V > 255) ? 255 : V));
                    yuv420sp[uvIndex++] = (byte)((U < 0) ? 0 : ((U > 255) ? 255 : U));
                }

                index++;
            }
        }
    }

    private Bitmap GetBitmap(ByteBuffer framebuff)
    {
        Bitmap b;
        try
        {
            var yuvimage = GetYUVImage(framebuff);
            using (var baos = new MemoryStream())
            {
                yuvimage.CompressToJpeg(new Android.Graphics.Rect(0, 0, _Width, _Height), 100, baos); // Where 100 is the quality of the generated jpeg
                byte[] jpegArray = baos.ToArray();
                //var bitmapoptions = new BitmapFactory.Options { InSampleSize = 2 };
                b = BitmapFactory.DecodeByteArray(jpegArray, 0, jpegArray.Length); //, bitmapoptions);
            }
        }
        catch (Exception e)
        {
            Log.Error(TAG, "could not get bitmap", e, e.Message);
            throw new RuntimeException("could not get bitmap");
        }

        return b;
    }

    private YuvImage GetYUVImage(ByteBuffer framebuff)
    {
        byte[] barray = new byte[framebuff.Remaining()];
        framebuff.Get(barray);

        return new YuvImage(barray, ImageFormatType.Nv21, _Width, _Height, null);
    }


    //private static long computePresentationTimeNsec(int frameIndex)
    //{
    //    long ONE_BILLION = 1000000000;
    //    return (frameIndex * (ONE_BILLION / FRAME_RATE));
    //}
    
    }


}