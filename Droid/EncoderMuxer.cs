using System;
using System.Collections.Generic;
using Android.Graphics;
using Android.Media;
using Android.Util;
using Java.Lang;
using Java.Nio;
using Exception = System.Exception;
using String = System.String;

//See http://b.android.com/37769 for a discussion of input format pitfalls.
//See http://b.android.com/37769 for a discussion of input format pitfalls.
//See http://b.android.com/37769 for a discussion of input format pitfalls.
//See http://b.android.com/37769 for a discussion of input format pitfalls.


namespace GrowPea.Droid
{

    public class EncoderMuxer 
    {

    private static String TAG = "EncoderMuxer";


    private static string _Filepath;

    //  parameters for the encoder
    private static String MIME_TYPE = "video/avc";

    //  H.264 Advanced Video Coding
    private static int _frameRate;

    //  15fps
    private static int IFRAME_INTERVAL = 1;

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

    private readonly List<byte[]> _ByteBuffers;

    private static MediaCodecCapabilities _SelectedCodecColor;

    private static ImageFormatType _CameraColorFormat = ImageFormatType.Nv21; //ImageFormatType NV21 or YV12 should be the image formats all Android cameras save under ?nv21 should always work i think?


    public EncoderMuxer(int width, int height, int bitRate, int framerate, string oFilePath, List<byte[]> byteBuffers)
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
        _frameRate = framerate;
    }

    public void EncodeVideoToMp4()
    {
        try
        {
            PrepareEncoder();
            EncodeMux();
        }
        catch (Exception e)
        {
            Log.Error(TAG, "Encoder & Mux failed", e);
            throw;
        }
        finally
        {
            //  release encoder, muxer
            releaseEncoder();
        }
    }

    private void PrepareEncoder()
    {
        MediaCodecInfo codecInfo = selectCodec(MIME_TYPE);

        if (codecInfo == null)
        {
            return;
        }

        int colorFormat;
        try
        {
            colorFormat = selectColorFormat(codecInfo, MIME_TYPE);
        }
        catch
        {
            colorFormat = (int)MediaCodecCapabilities.Formatyuv420semiplanar;
        }

        var format = MediaFormat.CreateVideoFormat(MIME_TYPE, _Width, _Height);
        format.SetInteger(MediaFormat.KeyColorFormat, colorFormat);
        format.SetInteger(MediaFormat.KeyBitRate, _BitRate);
        format.SetInteger(MediaFormat.KeyFrameRate, _frameRate);
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
        catch (Exception e)
        {
            Log.Error(TAG, e.Message, e);
            throw;
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
                _SelectedCodecColor = (MediaCodecCapabilities)System.Enum.ToObject(typeof(MediaCodecCapabilities), colorFormat);
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
            case (int)MediaCodecCapabilities.Formatyuv420planar: //I420
            case (int)MediaCodecCapabilities.Formatyuv420packedplanar: //I420
            case (int)MediaCodecCapabilities.Formatyuv420semiplanar: //NV12
            case (int)MediaCodecCapabilities.Formatyuv420packedsemiplanar: //NV12
            case (int)MediaCodecCapabilities.TiFormatyuv420packedsemiplanar: //NV12
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

        if (_Muxer != null && _MuxerStarted)
        {
            _Muxer.Stop();
            _Muxer.Release();
            _Muxer = null;
        }
    }

    private static long computePresentationTime(int frameIndex)
    {
        long value = frameIndex;
        return 132 + (value * (1000000 / _frameRate));
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
                                    //old way don't need to do this anymore.
                                    //Bitmap b = GetBitmap(imagedata);
                                    //byte[] yuv = new byte[b.Width * b.Height * 3 / 2];
                                    //int[] argb = new int[b.Width * b.Height];
                                    //b.GetPixels(argb, 0, b.Width, 0, 0, b.Width, b.Height);
                                    //encodeYUV420SP(yuv, argb, b.Width, b.Height);
                                    //b.Recycle();
                                    //old way don't need to do this anymore?

                                    //int[] argb = new int[imagedata.Width * imagedata.Height];
                                    //imagedata.GetPixels(argb, 0, imagedata.Width, 0, 0, imagedata.Width, imagedata.Height);
                                    //byte[] yuv = new byte[imagedata.Width * imagedata.Height * 3 / 2];
                                    //encodeYUV420SP(yuv, argb, imagedata.Width, imagedata.Height);
                                    //YuvImage yuv = GetYUVImage(imagedata);

                                    //byte[] decomB = Utils.DecompressFast(imagedata);

                                    //var yuv = new YuvImage(decomB, _CameraColorFormat, _Width, _Height, null);
                                    //Bitmap b = BitmapFactory.DecodeByteArray(imagedata, 0, imagedata.Length);
                                    //byte[] yuv = new byte[b.Width * b.Height * 3 / 2];
                                    //int[] argb = new int[b.Width * b.Height];
                                    //b.GetPixels(argb, 0, b.Width, 0, 0, b.Width, b.Height);
                                    //encodeYUV420SP(yuv, argb, b.Width, b.Height);

                                Bitmap b = BitmapFactory.DecodeByteArray(imagedata, 0, imagedata.Length);
                                byte[] yuv = new byte[b.Width * b.Height * 3 / 2];
                                int[] argb = new int[b.Width * b.Height];
                                b.GetPixels(argb, 0, b.Width, 0, 0, b.Width, b.Height);
                                encodeYUV420SP(yuv, argb, b.Width, b.Height);
                                var yuvimage = new YuvImage(yuv, _CameraColorFormat, _Width, _Height, null);
                                var yuvarray = yuvimage.GetYuvData();
                                colorcorrection(ref yuvarray); //method for fixing common color matching issues see below for comments

                                inputBuf.Put(yuvarray);

                                chunkSize = yuvarray.Length;
                                //yuv = null;
                                //GC.Collect(); //essential to fix memory leak from new YuvImage allocation above
                                b.Recycle();
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
                var mBufferInfo = new MediaCodec.BufferInfo();

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

                        //  adjust the ByteBuffer values to match BufferInfo (not needed?) old
                        //encodedData.Position(mBufferInfo.Offset);
                        //encodedData.Limit(mBufferInfo.Offset + this.mBufferInfo.Size);

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
            throw;
        }
    }

    //used for all possible cases of color correction accounting for discrepencies between android camera saved images and codec color formats
    //navigate to http://bigflake.com/mediacodec/ & see question 5 at the bottom

    private void colorcorrection(ref byte[] yuv)
    {
        string codecformat;

        switch (_SelectedCodecColor)
        {
            case MediaCodecCapabilities.Formatyuv420semiplanar: //NV12
            case MediaCodecCapabilities.Formatyuv420packedsemiplanar: //NV12
            case MediaCodecCapabilities.TiFormatyuv420packedsemiplanar: //NV12
                codecformat = "NV12";
                break;
            case MediaCodecCapabilities.Formatyuv420planar: //I420
            case MediaCodecCapabilities.Formatyuv420packedplanar: //I420
                codecformat = "I420";
                break;
            default:
                codecformat = null;
                break;
        }

        if (codecformat == "NV12" &&  _CameraColorFormat == ImageFormatType.Nv21) //works as tested on pixel
        {
            swapNV21_NV12(ref yuv);
        }
        else if (codecformat == "I420" && _CameraColorFormat == ImageFormatType.Nv21) //not tested on device that has this config so not sure if it works
        {
            //if codeec is I420 it might be easier to convert from yv12 as seen below, maybe try to switch cam output to YV12? and do below conversion?
            throw new NotImplementedException();
        }
        else if (codecformat == "I420" && _CameraColorFormat == ImageFormatType.Yv12) //not tested on device that has this config so not sure if it works
        {
            yuv = swapYV12toI420(yuv);
        }
        else if (codecformat == "NV12" && _CameraColorFormat == ImageFormatType.Yv12) //not tested on device that has this config so not sure if it works
        {
            //find conversion and put it here you shit
            throw new NotImplementedException();
        }

    }



    private YuvImage GetYUVImage(ByteBuffer framebuff)
    {
        byte[] barray = new byte[framebuff.Remaining()];
        framebuff.Get(barray);

        return new YuvImage(barray, _CameraColorFormat, _Width, _Height, null);
    }



    public void swapNV21_NV12(ref byte[] yuv)
    {
        int length = 0;
        if (yuv.Length % 2 == 0)
            length = yuv.Length;
        else
            length = yuv.Length - 1; //for uneven we need to shorten loop because it will go out of bounds because of i1 += 2

        for (int i1 = 0; i1 < length; i1 += 2)
        {
            if (i1 >= _Width * _Height)
            {
                byte tmp = yuv[i1];
                yuv[i1] = yuv[i1 + 1];
                yuv[i1 + 1] = tmp;
            }
        }
    }



    public byte[] swapYV12toI420(byte[] yv12bytes)
    {
        byte[] i420bytes = new byte[yv12bytes.Length];
        for (int i = 0; i < _Width * _Height; i++)
            i420bytes[i] = yv12bytes[i];
        for (int i = _Width * _Height; i < _Width * _Height + (_Width / 2 * _Height / 2); i++)
            i420bytes[i] = yv12bytes[i + (_Width / 2 * _Height / 2)];
        for (int i = _Width * _Height + (_Width / 2 * _Height / 2); i < _Width * _Height + 2 * (_Width / 2 * _Height / 2); i++)
            i420bytes[i] = yv12bytes[i - (_Width / 2 * _Height / 2)];
        return i420bytes;
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





    }

}
