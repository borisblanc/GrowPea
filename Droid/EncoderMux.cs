
//using Android.Opengl;

//using Android.Test;

//using Java.IO;
//using Java.Nio;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using Android.Annotation;
//using Android.App;
//using Android.Content;
//using Android.Graphics;
//using Android.Media;
//using Android.OS;
//using Android.Runtime;
//using Android.Util;
//using Android.Views;
//using Android.Widget;
//using Java.Lang;
//using String = System.String;
//using System.IO;
//using System.Linq.Expressions;
//using System.Runtime.InteropServices;
//using Java.Util;
//using Console = System.Console;


//namespace GrowPea.Droid
//{
//    public class EncodeAndMux
//    {

//        private static String TAG = "EncodeAndMuxTest";

//        private static bool VERBOSE = false;

//        private static Java.IO.File OUTPUT_DIR =
//            Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);

//        private static String MIME_TYPE = "video/avc";

//        private static int FRAME_RATE = 10;

//        //  10 seconds between I-frames
//        private static int IFRAME_INTERVAL = 10;

//        private static int NUM_FRAMES = 5;

//        private static String DEBUG_FILE_NAME_BASE = "/sdcard/test";

//        //  two seconds of video size of a frame, in pixels
//        private int mWidth = -1;

//        private int mHeight = -1;

//        //  bit rate, in bits per second
//        private int mBitRate = -1;



//        //  largest color component delta seen (i.e. actual vs. expected)
//        private int mLargestColorDelta;

//        //  encoder / muxer state
//        private MediaCodec mEncoder;

//        private MediaMuxer mMuxer;

//        private int mTrackIndex;

//        private bool mMuxerStarted;

//        //private Utils mUtils;

//        private float mPadding;

//        private int mColumnWidth;

//        private static int TEST_Y = 120;

//        //  YUV values for colored rect
//        private static int TEST_U = 160;

//        private static int TEST_V = 200;

//        private static int TEST_R0 = 0;

//        //  RGB equivalent of {0,0,0}
//        private static int TEST_G0 = 136;

//        private static int TEST_B0 = 0;

//        private static int TEST_R1 = 236;

//        //  RGB equivalent of {120,160,200}
//        private static int TEST_G1 = 50;

//        private static int TEST_B1 = 186;

//        private static bool DEBUG_SAVE_FILE = false;

//        //  save copy of
//        //  encoded movie
//        //  allocate one of these up front so we don't need to do it every time
//        private MediaCodec.BufferInfo mBufferInfo;

//        private List<String> mImagePaths = new List<String>();

//        private List<ByteBuffer> _buffs;

//        byte[] getNV21(int inputWidth, int inputHeight, Bitmap scaled)
//        {
//            int[] argb = new int[(inputWidth * inputHeight)];
//            scaled.GetPixels(argb, 0, inputWidth, 0, 0, inputWidth, inputHeight);
//            byte[] yuv = new byte[(inputWidth * (inputHeight * (3 / 2)))];
//            this.encodeYUV420SP(yuv, argb, inputWidth, inputHeight);
//            scaled.Recycle();
//            return yuv;
//        }

//        void encodeYUV420SP(byte[] yuv420sp, int[] argb, int width, int height)
//        {
//            int frameSize = (width * height);
//            int yIndex = 0;
//            int uvIndex = frameSize;
//            int V;
//            int a;
//            int R;
//            int G;
//            int B;
//            int Y;
//            int U;
//            int index = 0;
//            for (int j = 0; (j < height); j++)
//            {
//                for (int i = 0; (i < width); i++)
//                {
//                    // a = ((argb[index] & 4278190080) + 24);
//                    //  a is not used obviously
//                    R = ((argb[index] & 16711680)
//                         + 16);
//                    G = ((argb[index] & 65280)
//                         + 8);
//                    B = ((argb[index] & 255)
//                         + 0);
//                    //  well known RGB to YUV algorithm
//                    Y = ((((66 * R)
//                           + ((129 * G)
//                              + ((25 * B)
//                                 + 128)))
//                          + 8)
//                         + 16);
//                    U = ((((((38 * R) - (74 * G))
//                            * -1)
//                           + ((112 * B)
//                              + 128))
//                          + 8)
//                         + 128);
//                    V = (((((112 * R)
//                            - ((94 * G) - (18 * B)))
//                           + 128)
//                          + 8)
//                         + 128);
//                    //  NV21 has a plane of Y and interleaved planes of VU each
//                    //  sampled by a factor of 2
//                    //  meaning for every 4 Y pixels there are 1 V and 1 U. Note the
//                    //  sampling is every other
//                    //  pixel AND every other scanline.
//                    yuv420sp[yIndex++] = (byte) ((Y < 0) ? 0 : ((Y > 255) ? 255 : Y));

//                    if ((((j % 2) == 0) && ((index % 2) == 0)))
//                    {
//                        yuv420sp[uvIndex++] = (byte) ((V < 0) ? 0 : ((V > 255) ? 255 : V));
//                        yuv420sp[uvIndex++] = (byte) ((U < 0) ? 0 : ((U > 255) ? 255 : U));
//                    }

//                    index++;
//                }

//            }

//        }

//        public Bitmap decodeFile(int Index)
//        {

//            ////Java.IO.File f = new Java.IO.File(filePath);
//            //BitmapFactory.Options o = new BitmapFactory.Options();
//            //o.InJustDecodeBounds = true;
//            //o.InPurgeable = true;
//            //o.InInputShareable = true;
//            //BitmapFactory.DecodeStream(new System.IO.FileStream(filePath, FileMode.Open), null, o);
//            //int REQUIRED_WIDTH = WIDTH;
//            //int REQUIRED_HIGHT = HIGHT;
//            //int scale = 1;
//            //while ((((o.OutWidth / (scale / 2)) >= REQUIRED_WIDTH) && ((o.OutHeight / (scale / 2)) >= REQUIRED_HIGHT)))
//            //{
//            //    scale = (scale * 2);
//            //}

//            //BitmapFactory.Options o2 = new BitmapFactory.Options();
//            //o2.InSampleSize = scale;
//            //o2.InPurgeable = true;
//            //o2.InInputShareable = true;
//            //return BitmapFactory.decodeStream(new FileInputStream(f), null, o2);

//            ByteBuffer bb = null;

//            if (_buffs != null && _buffs.Count > Index)
//                bb = _buffs[Index];


//            return GetBitmap(bb);
//        }

//        private Bitmap GetBitmap(ByteBuffer framebuff)
//        {
//            Bitmap b;
//            try
//            {
//                var yuvimage = GetYUVImage(framebuff);
//                using (var baos = new MemoryStream())
//                {
//                    yuvimage.CompressToJpeg(new Android.Graphics.Rect(0, 0, mWidth, mHeight), 100,
//                        baos); // Where 90 is the quality of the generated jpeg
//                    byte[] jpegArray = baos.ToArray();
//                    //var bitmapoptions = new BitmapFactory.Options {InSampleSize = 2};
//                    b = BitmapFactory.DecodeByteArray(jpegArray, 0, jpegArray.Length);
//                    //b = Resize(bitmap, 640, 480);
//                }
//            }
//            catch (Exception e)
//            {
//                System.Console.WriteLine(e);
//                throw;
//            }

//            return b;
//        }

//        private YuvImage GetYUVImage(ByteBuffer framebuff)
//        {
//            byte[] barray = new byte[framebuff.Remaining()];
//            framebuff.Get(barray);

//            return new YuvImage(barray, ImageFormatType.Nv21, mWidth, mHeight, null);
//        }


//        public void StartSHit(List<ByteBuffer> buffs, int width, int height, int bitrate)
//        {
//            //base.onCreate(savedInstanceState);
//            //setContentView(R.layout.activity_encode_and_mux);
//            //this.mUtils = new Utils(this);
//            //this.mImagePaths = this.mUtils.getBackFilePaths();
//            //this.mPadding = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, AppConstant.GRID_PADDING, getResources().getDisplayMetrics());
//            //this.mColumnWidth = ((int)(((this.mUtils.getScreenWidth()
//            //                             - ((AppConstant.NUM_OF_COLUMNS + 1)
//            //                                * this.mPadding))
//            //                            / AppConstant.NUM_OF_COLUMNS)));

//            this._buffs = buffs;
//            this.mWidth = width;
//            this.mHeight = height;
//            this.mBitRate = bitrate;
//            try
//            {
//                encodeDecodeVideoFromBuffer(false);
//            }
//            catch (Exception e)
//            {
//                //  TODO Auto-generated catch block
//                e.PrintStackTrace();
//            }
//            catch (Throwable e)
//            {
//                //  TODO Auto-generated catch block
//                e.PrintStackTrace();
//            }

//        }

//        private void encodeDecodeVideoFromBuffer(bool toSurface)
//        {
//            MediaCodec encoder = null;
//            MediaCodec decoder = null;
//            mLargestColorDelta = -1;
//            try
//            {
//                MediaCodecInfo codecInfo = selectCodec(MIME_TYPE);
//                if (codecInfo == null)
//                {
//                    // Don't fail CTS if they don't have an AVC codec (not here, anyway).
//                    //Log.e(TAG, "Unable to find an appropriate codec for " + MIME_TYPE);
//                    return;
//                }

//                int colorFormat = selectColorFormat(codecInfo, MIME_TYPE);

//                // We avoid the device-specific limitations on width and height by using values that
//                // are multiples of 16, which all tested devices seem to be able to handle.
//                MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, mWidth, mHeight);
//                // Set some properties.  Failing to specify some of these can cause the MediaCodec
//                // configure() call to throw an unhelpful exception.
//                format.SetInteger(MediaFormat.KeyColorFormat, colorFormat);
//                format.SetInteger(MediaFormat.KeyBitRate, mBitRate);
//                format.SetInteger(MediaFormat.KeyFrameRate, FRAME_RATE);
//                format.SetInteger(MediaFormat.KeyIFrameInterval, IFRAME_INTERVAL);


//                // Create a MediaCodec for the desired codec, then configure it as an encoder with
//                // our desired properties.
//                encoder = MediaCodec.CreateByCodecName(codecInfo.Name);
//                encoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);
//                encoder.Start();

//                decoder = MediaCodec.CreateDecoderByType(MIME_TYPE);
//                // Create a MediaCodec for the decoder, just based on the MIME type.  The various
//                // format details will be passed through the csd-0 meta-data later on.
//                //String outputPath = new File(OUTPUT_DIR, "test." + mWidth + "x" + mHeight + ".mp4").ToString();

//                doEncodeDecodeVideoFromBuffer(encoder, colorFormat, decoder, toSurface);

//            }
//            finally
//            {

//                if (encoder != null)
//                {
//                    encoder.Stop();
//                    encoder.Release();
//                }
//                if (decoder != null)
//                {
//                    decoder.Stop();
//                    decoder.Release();
//                }

//            }
//        }

//        private static MediaCodecInfo selectCodec(String mimeType)
//        {
//            int numCodecs = MediaCodecList.CodecCount;
//            for (int i = 0; i < numCodecs; i++)
//            {
//                MediaCodecInfo codecInfo = MediaCodecList.GetCodecInfoAt(i);
//                if (!codecInfo.IsEncoder)
//                {
//                    continue;
//                }

//                String[] types = codecInfo.GetSupportedTypes();
//                for (int j = 0; (j < types.Length); j++)
//                {
//                    if (types[j].ToLower().Equals(mimeType.ToLower()))
//                    {
//                        return codecInfo;
//                    }

//                }

//            }

//            return null;
//        }

//        private static int selectColorFormat(MediaCodecInfo codecInfo, String mimeType)
//        {
//            MediaCodecInfo.CodecCapabilities capabilities = codecInfo.GetCapabilitiesForType(mimeType);
//            for (int i = 0; (i < capabilities.ColorFormats.Count); i++)
//            {
//                int colorFormat = capabilities.ColorFormats[i];
//                if (EncodeAndMux.isRecognizedFormat(colorFormat))
//                {
//                    return colorFormat;
//                }

//            }

//            //Log.e("", ("couldn\'t find a good color format for "
//            //           + (codecInfo.getName() + (" / " + mimeType))));
//            return 0;
//            //  not reached
//        }

//        private static bool isRecognizedFormat(int colorFormat)
//        {
//            switch (colorFormat)
//            {
//                case (int) MediaCodecCapabilities.Formatyuv420planar:
//                case (int) MediaCodecCapabilities.Formatyuv420packedplanar:
//                case (int) MediaCodecCapabilities.Formatyuv420semiplanar:
//                case (int) MediaCodecCapabilities.Formatyuv420packedsemiplanar:
//                case (int) MediaCodecCapabilities.TiFormatyuv420packedsemiplanar:
//                    return true;
//                default:
//                    return false;
//            }
//        }

//        private static bool isSemiPlanarYUV(int colorFormat)
//        {
//            switch (colorFormat)
//            {
//                case (int) MediaCodecInfo.CodecCapabilities.COLORFormatYUV420Planar:
//                case (int) MediaCodecInfo.CodecCapabilities.COLORFormatYUV420PackedPlanar:
//                    return false;
//                case (int) MediaCodecInfo.CodecCapabilities.COLORFormatYUV420SemiPlanar:
//                case (int) MediaCodecInfo.CodecCapabilities.COLORFormatYUV420PackedSemiPlanar:
//                case (int) MediaCodecInfo.CodecCapabilities.COLORTIFormatYUV420PackedSemiPlanar:
//                    return true;
//                default:
//                    throw new RuntimeException(("unknown format " + colorFormat));
//            }
//        }

//        private static long computePresentationTime(int frameIndex)
//        {
//            long value = frameIndex;
//            return (132 + (value * (1000000 / FRAME_RATE)));
//        }

//        private void doEncodeDecodeVideoFromBuffer(MediaCodec encoder, int encoderColorFormat, MediaCodec decoder, bool toSurface)
//        {
//            int TIMEOUT_USEC = 10000;
//            ByteBuffer[] encoderInputBuffers = encoder.GetInputBuffers();
//            ByteBuffer[] encoderOutputBuffers = encoder.GetOutputBuffers();
//            ByteBuffer[] decoderInputBuffers = null;
//            ByteBuffer[] decoderOutputBuffers = null;
//            MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
//            MediaFormat decoderOutputFormat = null;
//            int generateIndex = 0;
//            int checkIndex = 0;
//            int badFrames = 0;
//            bool decoderConfigured = false;

//            byte[] frameData = new byte[mWidth * mHeight * 3 / 2];
//            //OutputSurface outputSurface = null;
//            //  The size of a frame of video data, in the formats we handle, is stride*sliceHeight
//            //  for Y, and (stride/2)*(sliceHeight/2) for each of the Cb and Cr channels.  Application
//            //  of algebra and assuming that stride==width and sliceHeight==height yields:
//            //  Just out of curiosity.
//            long rawSize = 0;
//            long encodedSize = 0;
//            //  Save a copy to disk.  Useful for debugging the test.  Note this is a raw elementary
//            //  stream, not a .mp4 file, so not all players will know what to do with it.
//            //if (toSurface)
//            //{
//            //    outputSurface = new OutputSurface(this.mWidth, this.mHeight);
//            //}

//            //  Loop until the output side is done.
//            bool inputDone = false;
//            bool encoderDone = false;
//            bool outputDone = false;
//            while (!outputDone)
//            {
//                //Log.e(TAG, "loop");
//                //  If we're not done submitting frames, generate a new one and submit it.  By
//                //  doing this on every loop we're working to ensure that the encoder always has
//                //  work to do.
//                // 
//                //  We don't really want a timeout here, but sometimes there's a delay opening
//                //  the encoder device, so a short timeout can keep us from spinning hard.
//                if (!inputDone)
//                {
//                    int inputBufIndex = encoder.DequeueInputBuffer(TIMEOUT_USEC);
//                    //Log.e(TAG, ("inputBufIndex=" + inputBufIndex));
//                    if ((inputBufIndex >= 0))
//                    {
//                        long ptsUsec = computePresentationTime(generateIndex);
//                        if ((generateIndex == NUM_FRAMES))
//                        {
//                            //  Send an empty frame with the end-of-stream flag set.  If we set EOS
//                            //  on a frame with data, that frame data will be ignored, and the
//                            //  output will be short one frame.
//                            encoder.QueueInputBuffer(inputBufIndex, 0, 0, ptsUsec, MediaCodec.BufferFlagEndOfStream);
//                            inputDone = true;
//                            //Log.e(TAG, "sent input EOS (with zero-length frame)");
//                        }
//                        else
//                        {
//                            generateFrame(generateIndex, encoderColorFormat, frameData);
//                            //generateFrame(generateIndex);
//                            ByteBuffer inputBuf = encoderInputBuffers[inputBufIndex];
//                            //  the buffer should be sized to hold one full frame
//                            inputBuf.Clear();
//                            inputBuf.Put(frameData);
//                            encoder.QueueInputBuffer(inputBufIndex, 0, frameData.Length, ptsUsec, 0);
//                            //Log.e(TAG, ("submitted frame "+ (generateIndex + " to enc")));
//                        }

//                        generateIndex++;
//                    }
//                    else
//                    {
//                        //  either all in use, or we timed out during initial setup
//                        //Log.e(TAG, "input buffer not available");
//                    }

//                }

//                //  Check for output from the encoder.  If there's no output yet, we either need to
//                //  provide more input, or we need to wait for the encoder to work its magic.  We
//                //  can't actually tell which is the case, so if we can't get an output buffer right
//                //  away we loop around and see if it wants more input.
//                // 
//                //  Once we get EOS from the encoder, we don't need to do this anymore.
//                if (!encoderDone)
//                {
//                    int encoderStatus = encoder.DequeueOutputBuffer(info, TIMEOUT_USEC);
//                    if ((encoderStatus == (int) MediaCodec.InfoTryAgainLater))
//                    {
//                        //  no output available yet
//                        //Log.e(TAG, "no output from encoder available");
//                    }
//                    else if ((encoderStatus == (int) MediaCodec.InfoOutputBuffersChanged))
//                    {
//                        //  not expected for an encoder
//                        encoderOutputBuffers = encoder.GetOutputBuffers();
//                        //Log.e(TAG, "encoder output buffers changed");
//                    }
//                    else if ((encoderStatus == (int) MediaCodec.InfoOutputFormatChanged))
//                    {
//                        //  not expected for an encoder
//                        if (this.mMuxerStarted)
//                        {
//                            throw new RuntimeException("format changed twice");
//                        }

//                        MediaFormat newFormat = encoder.OutputFormat;
//                        //Log.e(TAG, ("encoder output format changed: " + newFormat));
//                        //  now that we have the Magic Goodies, start the muxer
//                        this.mTrackIndex = this.mMuxer.AddTrack(newFormat);
//                        //Log.e(TAG, ("muxer defined muxer format: " + newFormat));
//                        this.mMuxer.Start();
//                        this.mMuxerStarted = true;
//                    }
//                    else if ((encoderStatus < 0))
//                    {
//                        //Log.e("", ("unexpected result from encoder.dequeueOutputBuffer: " + encoderStatus));
//                    }
//                    else
//                    {
//                        //  encoderStatus >= 0
//                        ByteBuffer encodedData = encoderOutputBuffers[encoderStatus];
//                        if ((encodedData == null))
//                        {
//                            //Log.e("", ("encoderOutputBuffer " + (encoderStatus + " was null")));
//                        }

//                        //  It's usually necessary to adjust the ByteBuffer values to match BufferInfo.
//                        encodedData.Position(info.Offset);
//                        encodedData.Limit((info.Offset + info.Size));
//                        encodedSize = (encodedSize + info.Size);
//                        if (((info.Flags & MediaCodec.BufferFlagCodecConfig) != 0))
//                        {
//                            //  Codec config info.  Only expected on first packet.  One way to
//                            //  handle this is to manually stuff the data into the MediaFormat
//                            //  and pass that to configure().  We do that here to exercise the API.
//                            MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, this.mWidth, this.mHeight);
//                            format.SetByteBuffer("csd-0", encodedData);
//                            decoder.Configure(format, null, null, 0);

//                            decoder.Start();
//                            decoderInputBuffers = decoder.GetInputBuffers();
//                            decoderOutputBuffers = decoder.GetOutputBuffers();
//                            decoderConfigured = true;
//                            //Log.e(TAG, ("decoder configured (" + (info.size + (" bytes)" + format))));
//                        }
//                        else
//                        {
//                            //  Get a decoder input buffer, blocking until it's available.
//                            int inputBufIndex = decoder.DequeueInputBuffer(-1);
//                            ByteBuffer inputBuf = decoderInputBuffers[inputBufIndex];
//                            inputBuf.Clear();
//                            inputBuf.Put(encodedData);
//                            decoder.QueueInputBuffer(inputBufIndex, 0, info.Size, info.PresentationTimeUs, info.Flags);
//                            encoderDone = ((info.Flags & MediaCodec.BufferFlagEndOfStream) != 0);
//                            //Log.e(TAG, ("passed "
//                            //            + (info.size + (" bytes to decoder" + encoderDone))));

//                            //Log.e("encoderDone", (encoderDone + ""));
//                        }

//                        encoder.ReleaseOutputBuffer(encoderStatus, false);
//                    }

//                }

//                //  Check for output from the decoder.  We want to do this on every loop to avoid
//                //  the possibility of stalling the pipeline.  We use a short timeout to avoid
//                //  burning CPU if the decoder is hard at work but the next frame isn't quite ready.
//                // 
//                //  If we're decoding to a Surface, we'll get notified here as usual but the
//                //  ByteBuffer references will be null.  The data is sent to Surface instead.
//                if (decoderConfigured)
//                {
//                    int decoderStatus = decoder.DequeueOutputBuffer(info, (3 * TIMEOUT_USEC));
//                    if ((decoderStatus == (int) MediaCodec.InfoTryAgainLater))
//                    {
//                        //  no output available yet
//                        //Log.e(TAG, "no output from decoder available");
//                    }
//                    else if ((decoderStatus == (int) MediaCodec.InfoOutputBuffersChanged))
//                    {
//                        //  The storage associated with the direct ByteBuffer may already be unmapped,
//                        //  so attempting to access data through the old output buffer array could
//                        //  lead to a native crash.
//                        //Log.e(TAG, "decoder output buffers changed");
//                        decoderOutputBuffers = decoder.GetOutputBuffers();
//                    }
//                    else if ((decoderStatus == (int) MediaCodec.InfoOutputFormatChanged))
//                    {
//                        //  this happens before the first frame is returned
//                        decoderOutputFormat = decoder.OutputFormat;
//                        //Log.e(TAG, ("decoder output format changed: " + decoderOutputFormat));
//                    }
//                    else if ((decoderStatus < 0))
//                    {
//                        //Log.e(TAG, ("unexpected result from deocder.dequeueOutputBuffer: " + decoderStatus));
//                    }
//                    else
//                    {
//                        //  decoderStatus >= 0
//                        if (!toSurface)
//                        {
//                            ByteBuffer outputFrame = decoderOutputBuffers[decoderStatus];
//                            outputFrame.Position(info.Offset);
//                            outputFrame.Limit((info.Offset + info.Size));
//                            this.mMuxer.WriteSampleData(this.mTrackIndex, outputFrame, info);
//                            rawSize = (rawSize + info.Size);
//                            if ((info.Size == 0))
//                            {
//                                //Log.e(TAG, "got empty frame");
//                            }
//                            else
//                            {
//                                //Log.e(TAG, ("decoded, checking frame " + checkIndex));
//                                //if (!checkFrame(checkIndex++, decoderOutputFormat, outputFrame))
//                                //{
//                                //    badFrames++;
//                                //}

//                            }

//                            if (((info.Flags & MediaCodec.BufferFlagEndOfStream) != 0))
//                            {
//                                //Log.e(TAG, "output EOS");
//                                outputDone = true;
//                            }

//                            decoder.ReleaseOutputBuffer(decoderStatus, false);
//                        }
//                        else
//                        {
//                            //Log.e(TAG, ("surface decoder given buffer "+ (decoderStatus + (" (size="+ (info.size + ")")))));
//                            rawSize = (rawSize + info.Size);
//                            if (((info.Flags & MediaCodec.BufferFlagEndOfStream) != 0))
//                            {
//                                //Log.e(TAG, "output EOS");
//                                outputDone = true;
//                            }

//                            bool doRender = (info.Size != 0);
//                            //  As soon as we call releaseOutputBuffer, the buffer will be forwarded
//                            //  to SurfaceTexture to convert to a texture.  The API doesn't guarantee
//                            //  that the texture will be available before the call returns, so we
//                            //  need to wait for the onFrameAvailable callback to fire.
//                            decoder.ReleaseOutputBuffer(decoderStatus, doRender);
//                            //if (doRender)
//                            //{
//                            //    //Log.e(TAG, ("awaiting frame " + checkIndex));
//                            //    outputSurface.awaitNewImage();
//                            //    outputSurface.drawImage();
//                            //    if (!checkSurfaceFrame(checkIndex++))
//                            //    {
//                            //        badFrames++;
//                            //    }

//                            //}

//                        }

//                    }

//                }

//            }

//            //Log.e(TAG, ("decoded "+ (checkIndex + (" frames at "+ (this.mWidth + ("x"+ (this.mHeight + (": raw="+ (rawSize + (", enc=" + encodedSize))))))))));
//            //if ((outputSurface != null))
//            //{
//            //    outputSurface.release();
//            //}

//            if ((checkIndex != NUM_FRAMES))
//            {
//                //Log.e(TAG, ("awaiting frame " + checkIndex));
//            }

//            if ((badFrames != 0))
//            {
//                //Log.e(TAG, ("Found "+ (badFrames + " bad frames")));
//            }

//        }

//        private void generateFrame(int frameIndex)
//        {
//            Bitmap bitmap = decodeFile(frameIndex);
//            //this.mFrame = this.getNV21(bitmap.Width, bitmap.Height, bitmap);
//        }

//        private void generateFrame(int frameIndex, int colorFormat, byte[] frameData)
//        {
//            try
//            {
//                int HALF_WIDTH = mWidth / 2;
//                bool semiPlanar = isSemiPlanarYUV(colorFormat);
//                // Set to zero.  In YUV this is a dull green.
//                Arrays.Fill(frameData, 0);
//                int startX, startY, countX, countY;
//                frameIndex %= 8;
//                //frameIndex = (frameIndex / 8) % 8;    // use this instead for debug -- easier to see
//                if (frameIndex < 4)
//                {
//                    startX = frameIndex * (mWidth / 4);
//                    startY = 0;
//                }
//                else
//                {
//                    startX = (7 - frameIndex) * (mWidth / 4);
//                    startY = mHeight / 2;
//                }
//                for (int y = startY + (mHeight / 2) - 1; y >= startY; --y)
//                {
//                    for (int x = startX + (mWidth / 4) - 1; x >= startX; --x)
//                    {
//                        if (semiPlanar)
//                        {
//                            // full-size Y, followed by UV pairs at half resolution
//                            // e.g. Nexus 4 OMX.qcom.video.encoder.avc COLOR_FormatYUV420SemiPlanar
//                            // e.g. Galaxy Nexus OMX.TI.DUCATI1.VIDEO.H264E
//                            //        OMX_TI_COLOR_FormatYUV420PackedSemiPlanar
//                            frameData[y * mWidth + x] = (byte) TEST_Y;
//                            if ((x & 0x01) == 0 && (y & 0x01) == 0)
//                            {
//                                frameData[mWidth * mHeight + y * HALF_WIDTH + x] = (byte) TEST_U;
//                                frameData[mWidth * mHeight + y * HALF_WIDTH + x + 1] = (byte) TEST_V;
//                            }
//                        }
//                        else
//                        {
//                            // full-size Y, followed by quarter-size U and quarter-size V
//                            // e.g. Nexus 10 OMX.Exynos.AVC.Encoder COLOR_FormatYUV420Planar
//                            // e.g. Nexus 7 OMX.Nvidia.h264.encoder COLOR_FormatYUV420Planar
//                            frameData[y * mWidth + x] = (byte) TEST_Y;
//                            if ((x & 0x01) == 0 && (y & 0x01) == 0)
//                            {
//                                frameData[mWidth * mHeight + (y / 2) * HALF_WIDTH + (x / 2)] = (byte) TEST_U;
//                                frameData[mWidth * mHeight + HALF_WIDTH * (mHeight / 2) +
//                                          (y / 2) * HALF_WIDTH + (x / 2)] = (byte) TEST_V;
//                            }
//                        }
//                    }
//                }
//            }
//            catch (Exception e)
//            {
                
//            }
//        }

//        private void setParameters(int width, int height, int bitRate)
//        {
//            if ((((width % 16) != 0) || ((height % 16) != 0)))
//            {
//                //Log.w(TAG, "WARNING: width or height not multiple of 16");
//            }

//            this.mWidth = width;
//            this.mHeight = height;
//            this.mBitRate = bitRate;
//            //this.mFrame = new byte[(this.mWidth * (this.mHeight * (3 / 2)))];
//        }

//        //public void testEncodeDecodeVideoFromBufferToSurface720p()
//        //{
//        //    this.setParameters(1280, 720, 6000000);
//        //    encodeDecodeVideoFromBuffer(false);
//        //}
//    }

//}