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
using System.IO;
using Android.Gms.Vision;
using Android.Graphics;
using Java.IO;
using Java.Util;
using Org.Opencv.Core;
using Org.Opencv.Highgui;
using Org.Opencv.Imgproc;


namespace GrowPea.Droid
{
    public class Encoder
    {

        //  lots of logging parameters for the encoder  
        private static String MIME_TYPE = "video/avc";

        //  H.264 Advanced Video  Coding  
        private static int FRAME_RATE = 10;

        //  10fps  
        private static int IFRAME_INTERVAL = 10;

        private string outputPath;
        private int mWidth = -1;

        private int mHeight = -1;

        // bit rate, in bits per second
        private int mBitRate = -1;

        public Encoder(int width, int height, int bitRate, string oPath)
        {
            if ((width % 16) != 0 || (height % 16) != 0)
            {
                //Log.w(TAG, "WARNING: width or height not multiple of 16");
            }
            mWidth = width;
            mHeight = height;
            mBitRate = bitRate;
            outputPath = oPath;
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
                for (int j = 0; j < types.Length; j++)
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
                case (int) MediaCodecCapabilities.Formatyuv420planar:
                case (int) MediaCodecCapabilities.Formatyuv420packedplanar:
                case (int) MediaCodecCapabilities.Formatyuv420semiplanar:
                case (int) MediaCodecCapabilities.Formatyuv420packedsemiplanar:
                case (int) MediaCodecCapabilities.TiFormatyuv420packedsemiplanar:
                    return true;
                default:
                    return false;
            }
        }

        private static long computePresentationTime(int frameIndex)
        {
            long value = frameIndex;
            return (132 + (value * (1000000 / FRAME_RATE)));
        }

        public void EncodeAll(List<ByteBuffer> bmaps)
        {
            //MediaMuxer muxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
            MediaCodec encoder = null;

            try
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
                    colorFormat = (int) MediaCodecCapabilities.Formatyuv420semiplanar;
                }

                MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, mWidth, mHeight);
                format.SetInteger(MediaFormat.KeyColorFormat, colorFormat);
                format.SetInteger(MediaFormat.KeyBitRate, mBitRate);
                format.SetInteger(MediaFormat.KeyFrameRate, FRAME_RATE);
                format.SetInteger(MediaFormat.KeyIFrameInterval, IFRAME_INTERVAL);

                encoder = MediaCodec.CreateByCodecName(codecInfo.Name);

                encoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);

                var videoTrackIndex = 0;

                foreach (var bmap in bmaps)
                {
                    Encode(bmap, encoder, videoTrackIndex);
                    videoTrackIndex++;
                }

            }
            catch(Exception e)
            {
                
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
                    yuvimage.CompressToJpeg(new Android.Graphics.Rect(0, 0, mWidth, mHeight), 100, baos); // Where 90 is the quality of the generated jpeg
                    byte[] jpegArray = baos.ToArray();
                    var bitmapoptions = new BitmapFactory.Options { InSampleSize = 2 };
                    b = BitmapFactory.DecodeByteArray(jpegArray, 0, jpegArray.Length, bitmapoptions);
                    //b = Resize(bitmap, 640, 480);
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                throw;
            }

            return b;
        }

        private YuvImage GetYUVImage(ByteBuffer framebuff)
        {
            byte[] barray = new byte[framebuff.Remaining()];
            framebuff.Get(barray);

            return new YuvImage(barray, ImageFormatType.Nv21, mWidth, mHeight, null);
        }


        private void Encode(ByteBuffer bb, MediaCodec encoder, int track_indx)
        {
            MediaMuxer muxer = null;
            MediaCodec.BufferInfo enc_info = new MediaCodec.BufferInfo();
            var enc_outputDone = false;
            var enc_inputDone = false;

            const int TIMEOUT_USEC = 10000;

            ByteBuffer[] encoderInputBuffers = encoder.GetInputBuffers();
            ByteBuffer[] enc_outputBuffers = encoder.GetOutputBuffers();

            try
            {

                while (!enc_outputDone)
                {
                    if (!enc_inputDone)
                    {
                        int inputBufIndex = encoder.DequeueInputBuffer(TIMEOUT_USEC);
                        if (inputBufIndex >= 0)
                        {
                            ByteBuffer inputBuf = encoderInputBuffers[inputBufIndex];
                            int chunkSize = 0;

                            if (bb == null)
                            {
                            }
                            else
                            {
                                Bitmap b = GetBitmap(bb);

                                int mWidth = b.Width;
                                int mHeight = b.Height;

                                byte[] yuv = new byte[mWidth * mHeight * 3 / 2];
                                int[] argb = new int[mWidth * mHeight];

                                b.GetPixels(argb, 0, mWidth, 0, 0, mWidth, mHeight);
                                encodeYUV420SP(yuv, argb, mWidth, mHeight);

                                b.Recycle();
                                b = null;
                                inputBuf.Put(yuv);
                                chunkSize = yuv.Length;
                            }

                            if (chunkSize < 0)
                            {
                                encoder.QueueInputBuffer(inputBufIndex, 0, 0, 0L, MediaCodecBufferFlags.EndOfStream);
                            }
                            else
                            {
                                long presentationTimeUs = computePresentationTime(track_indx);
                                System.Diagnostics.Debug.WriteLine("Encode", "Encode Time: " + presentationTimeUs);
                                encoder.QueueInputBuffer(inputBufIndex, 0, chunkSize, presentationTimeUs, 0);
                                inputBuf.Clear();

                                encoderInputBuffers[inputBufIndex].Clear();
                                enc_inputDone = true;
                            }
                        }
                    }
                    if (!enc_outputDone)
                    {
                        int enc_decoderStatus = encoder.DequeueOutputBuffer(enc_info, TIMEOUT_USEC);
                        if (enc_decoderStatus == (int) MediaCodecInfoState.TryAgainLater)
                        {
                        }
                        else if (enc_decoderStatus == (int) MediaCodecInfoState.OutputBuffersChanged)
                        {
                            enc_outputBuffers = encoder.GetOutputBuffers();
                        }
                        else if (enc_decoderStatus == (int) MediaCodecInfoState.OutputFormatChanged)
                        {
                            //MediaFormat newFormat = encoder.GetOutputFormat(); not used
                        }
                        else if (enc_decoderStatus < 0)
                        {
                        }
                        else
                        {
                            if ((enc_info.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                            {
                                enc_outputDone = true;
                            }

                            bool enc_doRender = (enc_info.Size != 0);
                            encoder.ReleaseOutputBuffer(enc_decoderStatus, false);
                            if (enc_doRender)
                            {
                                enc_outputDone = true;
                                ByteBuffer enc_buffer = enc_outputBuffers[enc_decoderStatus];

                                try
                                {
                                    muxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
                                    muxer.WriteSampleData(track_indx, enc_buffer, enc_info);
                                }
                                catch (Exception e)
                                {
                                    //e.printStackTrace();
                                }
                                finally
                                {

                                }

                                enc_buffer.Clear();
                                enc_outputBuffers[enc_decoderStatus].Clear();
                            }
                        }
                    }
                }
            }
            finally
            {
                if (encoder != null)
                {
                    encoder.Stop();
                    encoder.Release();
                }
                if (muxer != null)
                {
                    muxer.Stop();
                    muxer.Release();
                }
            }

        }

        private void encodeYUV420SP(byte[] yuv420sp, int[] argb, int width, int height)
        {
            int frameSize = width * height;
            ;

            int yIndex = 0;
            int uvIndex = frameSize;

            int a, R, G, B, Y, U, V;
            int index = 0;
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {

                    //a = (argb[index] & 0xff000000) >> 24; // a is not used obviously
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
                    yuv420sp[yIndex++] = (byte) ((Y < 0) ? 0 : ((Y > 255) ? 255 : Y));
                    if (j % 2 == 0 && index % 2 == 0)
                    {
                        yuv420sp[uvIndex++] = (byte) ((V < 0) ? 0 : ((V > 255) ? 255 : V));
                        yuv420sp[uvIndex++] = (byte) ((U < 0) ? 0 : ((U > 255) ? 255 : U));
                    }

                    index++;
                }
            }
        }
    }
}