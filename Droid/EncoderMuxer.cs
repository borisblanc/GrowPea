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

    private static bool VERBOSE = false;

    //  lots of logging
    //  where to put the output file (note: /sdcard requires WRITE_EXTERNAL_STORAGE permission)
    private static File OUTPUT_DIR = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);

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
    private int mWidth = -1;

    private int mHeight = -1;

    //  bit rate, in bits per second
    private int mBitRate = -1;

    //  encoder / muxer state
    private MediaCodec mEncoder;

    //private CodecInputSurface mInputSurface;

    private MediaMuxer mMuxer;

    private int mTrackIndex;

    private bool mMuxerStarted;

    //  allocate one of these up front so we don't need to do it every time
    private MediaCodec.BufferInfo mBufferInfo;

    private List<ByteBuffer> _bmaps;

    public void EncodeVideoToMp4(List<ByteBuffer> bmaps)
    {
        //  QVGA at 2Mbps
        this.mWidth = 640;
        this.mHeight = 420;
        this.mBitRate = 4000000;
        _bmaps = bmaps;
        try
        {
            this.prepareEncoder();
            //this.mInputSurface.makeCurrent();
            //for (int i = 0; (i < bmaps.Count); i++)
            //{
            //  Feed any pending encoder output into the muxer.
            this.drainEncoder(false);
            //  Generate a new frame of input.
            //this.generateSurfaceFrame(i);
            //this.mInputSurface.setPresentationTime(computePresentationTimeNsec(i));
            //  Submit it to the encoder.  The eglSwapBuffers call will block if the input
            //  is full, which would be bad if it stayed full until we dequeued an output
            //  buffer (which we can't do, since we're stuck here).  So long as we fully drain
            //  the encoder before supplying additional input, the system guarantees that we
            //  can supply another frame without blocking.
            //if (VERBOSE)
            //{
            //Log.d(TAG, ("sending frame "+ (i + " to encoder")));
            //}

            //this.mInputSurface.swapBuffers();
            //}

            //  send end-of-stream to encoder, and drain remaining output
            this.drainEncoder(true);
        }
        catch (Exception e)
        {
            
        }
        finally
        {
            //  release encoder, muxer, and input Surface
            this.releaseEncoder();
        }

    //  To test the result, open the file with MediaExtractor, and get the format.  Pass
    //  that into the MediaCodec decoder configuration, along with a SurfaceTexture surface,
    //  and examine the output with glReadPixels.
    }

    private void prepareEncoder()
    {
        this.mBufferInfo = new MediaCodec.BufferInfo();
        MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, this.mWidth, this.mHeight);

        MediaCodecInfo codecInfo = selectCodec(MIME_TYPE);
            //  Set some properties.  Failing to specify some of these can cause the MediaCodec
            //  configure() call to throw an unhelpful exception.
        //    format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface);
        //format.setInteger(MediaFormat.KEY_BIT_RATE, this.mBitRate);
        //format.setInteger(MediaFormat.KEY_FRAME_RATE, FRAME_RATE);
        //format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, IFRAME_INTERVAL);

        int colorFormat = selectColorFormat(codecInfo, MIME_TYPE);

        format.SetInteger(MediaFormat.KeyColorFormat, colorFormat);
        format.SetInteger(MediaFormat.KeyBitRate, mBitRate);
        format.SetInteger(MediaFormat.KeyFrameRate, FRAME_RATE);
        format.SetInteger(MediaFormat.KeyIFrameInterval, IFRAME_INTERVAL);


        //  Create a MediaCodec encoder, and configure it with our format.  Get a Surface
        //  we can use for input and wrap it with a class that handles the EGL work.
        // 
        //  If you want to have two EGL contexts -- one for display, one for recording --
        //  you will likely want to defer instantiation of CodecInputSurface until after the
        //  "display" EGL context is created, then modify the eglCreateContext call to
        //  take eglGetCurrentContext() as the share_context argument.
        this.mEncoder = MediaCodec.CreateEncoderByType(MIME_TYPE);
        this.mEncoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);
        //this.mInputSurface = new CodecInputSurface(this.mEncoder.createInputSurface());

        this.mEncoder.Start();
        //  Output filename.  Ideally this would use Context.getFilesDir() rather than a
        //  hard-coded output directory.
        String outputPath = new File(OUTPUT_DIR, ("test."+ (this.mWidth + ("x"+ (this.mHeight + ".mp4"))))).ToString();
        //Log.d(TAG, ("output file is " + outputPath));
        //  Create a MediaMuxer.  We can't add the video track and start() the muxer here,
        //  because our MediaFormat doesn't have the Magic Goodies.  These can only be
        //  obtained from the encoder after it has started processing data.
        // 
        //  We're not actually interested in multiplexing audio.  We just want to convert
        //  the raw H.264 elementary stream we get from MediaCodec into a .mp4 file.
        try
        {
            this.mMuxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
        }
        catch (IOException ioe)
        {
            throw new RuntimeException("MediaMuxer creation failed", ioe);
        }

        this.mTrackIndex = -1;
        this.mMuxerStarted = false;
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

        //Log.e("", ("couldn\'t find a good color format for "
        //           + (codecInfo.getName() + (" / " + mimeType))));
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

    private void releaseEncoder()
    {
        //if (VERBOSE)
        //{
        //Log.d(TAG, "releasing encoder objects");
        //}

        if ((this.mEncoder != null))
        {
            this.mEncoder.Stop();
            this.mEncoder.Release();
            this.mEncoder = null;
        }

        //if ((this.mInputSurface != null))
        //{
        //    this.mInputSurface.release();
        //    this.mInputSurface = null;
        //}

        if ((this.mMuxer != null))
        {
            this.mMuxer.Stop();
            this.mMuxer.Release();
            this.mMuxer = null;
        }

    }

    private static long computePresentationTime(int frameIndex)
    {
        long value = frameIndex;
        return (132 + (value * (1000000 / FRAME_RATE)));
    }

    private void drainEncoder(bool endOfStream)
    {
        int TIMEOUT_USEC = 10000;
        //if (VERBOSE)
        //{
        //    //Log.d(TAG, ("drainEncoder("+ (endOfStream + ")")));
        //}

        if (endOfStream)
        {
            //if (VERBOSE)
            //{
            //Log.d(TAG, "sending EOS to encoder");
            //}

            this.mEncoder.SignalEndOfInputStream();
        }


        ByteBuffer[] encoderInputBuffers = mEncoder.GetInputBuffers();

        bool inputDone = false;
        int generateIndex = 0;
        try
        {
            while (true)
            {

                if (!inputDone)
                {
                    int inputBufIndex = mEncoder.DequeueInputBuffer(TIMEOUT_USEC);
                    //Log.e(TAG, ("inputBufIndex=" + inputBufIndex));
                    if (inputBufIndex >= 0)
                    {
                        long ptsUsec = computePresentationTime(generateIndex);
                        if (generateIndex == _bmaps.Count)
                        {
                            //  Send an empty frame with the end-of-stream flag set.  If we set EOS
                            //  on a frame with data, that frame data will be ignored, and the
                            //  output will be short one frame.
                            mEncoder.QueueInputBuffer(inputBufIndex, 0, 0, ptsUsec, MediaCodec.BufferFlagEndOfStream);
                            inputDone = true;
                            //Log.e(TAG, "sent input EOS (with zero-length frame)");
                        }
                        else
                        {
                            //generateFrame(generateIndex, encoderColorFormat, frameData);
                            //generateFrame(generateIndex);
                            ByteBuffer inputBuf = encoderInputBuffers[inputBufIndex];
                            var imagedata = _bmaps[generateIndex];
                            int chunkSize = 0;

                            if (imagedata == null)
                            {
                            }
                            else
                            {
                                Bitmap b = GetBitmap(imagedata);

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


                            //  the buffer should be sized to hold one full frame
                            inputBuf.Clear();
                            mEncoder.QueueInputBuffer(inputBufIndex, 0, chunkSize, ptsUsec, 0);
                            //Log.e(TAG, ("submitted frame "+ (generateIndex + " to enc")));
                        }

                        generateIndex++;
                    }
                    else
                    {
                        //  either all in use, or we timed out during initial setup
                        //Log.e(TAG, "input buffer not available");
                    }

                }

                ByteBuffer[] encoderOutputBuffers = mEncoder.GetOutputBuffers();

                int encoderStatus = this.mEncoder.DequeueOutputBuffer(this.mBufferInfo, TIMEOUT_USEC);

                if ((encoderStatus == (int) MediaCodec.InfoTryAgainLater))
                {
                    //  no output available yet
                    if (!endOfStream)
                    {
                        break;
                        //  out of while
                    }
                    else if (VERBOSE)
                    {
                        //Log.d(TAG, "no output available, spinning to await EOS");
                    }

                }
                else if ((encoderStatus == (int) MediaCodec.InfoOutputBuffersChanged))
                {
                    //  not expected for an encoder
                    encoderOutputBuffers = this.mEncoder.GetOutputBuffers();
                }
                else if ((encoderStatus == (int) MediaCodec.InfoOutputFormatChanged))
                {
                    //  should happen before receiving buffers, and should only happen once
                    if (this.mMuxerStarted)
                    {
                        throw new RuntimeException("format changed twice");
                    }

                    MediaFormat newFormat = this.mEncoder.OutputFormat;
                    //Log.d(TAG, ("encoder output format changed: " + newFormat));
                    //  now that we have the Magic Goodies, start the muxer
                    this.mTrackIndex = this.mMuxer.AddTrack(newFormat);
                    this.mMuxer.Start();
                    this.mMuxerStarted = true;
                }
                else if ((encoderStatus < 0))
                {
                    //Log.w(TAG, ("unexpected result from encoder.dequeueOutputBuffer: " + encoderStatus));
                    //  let's ignore it
                }
                else
                {
                    ByteBuffer encodedData = encoderOutputBuffers[encoderStatus];
                    if ((encodedData == null))
                    {
                        throw new RuntimeException(("encoderOutputBuffer " + (encoderStatus + " was null")));
                    }

                    if (((this.mBufferInfo.Flags & MediaCodec.BufferFlagCodecConfig) != 0))
                    {
                        //  The codec config data was pulled out and fed to the muxer when we got
                        //  the INFO_OUTPUT_FORMAT_CHANGED status.  Ignore it.
                        //if (VERBOSE)
                        //{
                        //Log.d(TAG, "ignoring BUFFER_FLAG_CODEC_CONFIG");
                        //}

                        this.mBufferInfo.Size = 0;
                    }

                    if (this.mBufferInfo.Size != 0)
                    {
                        if (!this.mMuxerStarted)
                        {
                            throw new RuntimeException("muxer hasnt started");
                        }

                        //  adjust the ByteBuffer values to match BufferInfo (not needed?)
                        encodedData.Position(this.mBufferInfo.Offset);
                        encodedData.Limit((this.mBufferInfo.Offset + this.mBufferInfo.Size));
                        this.mMuxer.WriteSampleData(this.mTrackIndex, encodedData, this.mBufferInfo);
                        //if (VERBOSE)
                        //{
                        //Log.d(TAG, ("sent "
                        //+ (this.mBufferInfo.size + " bytes to muxer")));
                        //}

                    }

                    this.mEncoder.ReleaseOutputBuffer(encoderStatus, false);
                    if (((this.mBufferInfo.Flags & MediaCodec.BufferFlagEndOfStream) != 0))
                    {
                        if (!endOfStream)
                        {
                            //Log.w(TAG, "reached end of stream unexpectedly");
                        }
                        else if (VERBOSE)
                        {
                            //Log.d(TAG, "end of stream reached");
                        }

                        break;
                        //  out of while
                    }

                }

            }
        }
        catch (Exception e)
        {
            
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

    private void generateSurfaceFrame(int frameIndex)
    {
        
        int startY;
        int startX;
        if ((frameIndex < 4))
        {
            //  (0,0) is bottom-left in GL
            startX = (frameIndex * (this.mWidth / 4));
            startY = (this.mHeight / 2);
        }
        else
        {
            startX = ((7 - frameIndex) * (this.mWidth / 4));
            startY = 0;
        }

        GLES20.GlClearColor((TEST_R0 / 255), (TEST_G0 / 255), (TEST_B0 / 255), 1);
        GLES20.GlClear(GLES20.GlColorBufferBit);
        GLES20.GlEnable(GLES20.GlScissorTest);
        GLES20.GlScissor(startX, startY, (this.mWidth / 4), (this.mHeight / 2));
        GLES20.GlClearColor((TEST_R1 / 255), (TEST_G1 / 255), (TEST_B1 / 255), 1);
        GLES20.GlClear(GLES20.GlColorBufferBit);
        GLES20.GlDisable(GLES20.GlScissorTest);
    }

    private static long computePresentationTimeNsec(int frameIndex)
    {
        long ONE_BILLION = 1000000000;
        return (frameIndex * (ONE_BILLION / FRAME_RATE));
    }

    //private class CodecInputSurface
    //{

    //    private static int EGL_RECORDABLE_ANDROID = 12610;

    //    private EGLDisplay mEGLDisplay = EGL14.EGL_NO_DISPLAY;

    //    private EGLContext mEGLContext = EGL14.EGL_NO_CONTEXT;

    //    private EGLSurface mEGLSurface = EGL14.EGL_NO_SURFACE;

    //    private Surface mSurface;

    //    public CodecInputSurface(Surface surface)
    //    {
    //    if ((surface == null))
    //    {
    //    throw new NullPointerException();
    //    }

    //    this.mSurface = surface;
    //    this.eglSetup();
    //    }

    //    private void eglSetup()
    //    {
    //    this.mEGLDisplay = EGL14.eglGetDisplay(EGL14.EGL_DEFAULT_DISPLAY);
    //    if ((this.mEGLDisplay == EGL14.EGL_NO_DISPLAY))
    //    {
    //    throw new RuntimeException("unable to get EGL14 display");
    //    }

    //    int[] version = new int[2];
    //    if (!EGL14.eglInitialize(this.mEGLDisplay, version, 0, version, 1))
    //    {
    //    throw new RuntimeException("unable to initialize EGL14");
    //    }

    //    //  Configure EGL for recording and OpenGL ES 2.0.
    //    int[] attribList = new int[] {
    //    EGL14.EGL_RED_SIZE,
    //    8,
    //    EGL14.EGL_GREEN_SIZE,
    //    8,
    //    EGL14.EGL_BLUE_SIZE,
    //    8,
    //    EGL14.EGL_ALPHA_SIZE,
    //    8,
    //    EGL14.EGL_RENDERABLE_TYPE,
    //    EGL14.EGL_OPENGL_ES2_BIT,
    //    EGL_RECORDABLE_ANDROID,
    //    1,
    //    EGL14.EGL_NONE};
    //    EGLConfig[] configs = new EGLConfig[1];
    //    int[] numConfigs = new int[1];
    //    EGL14.eglChooseConfig(this.mEGLDisplay, attribList, 0, configs, 0, configs.length, numConfigs, 0);
    //    this.checkEglError("eglCreateContext RGB888+recordable ES2");
    //    //  Configure context for OpenGL ES 2.0.
    //    int[] attrib_list = new int[] {
    //    EGL14.EGL_CONTEXT_CLIENT_VERSION,
    //    2,
    //    EGL14.EGL_NONE};
    //    this.mEGLContext = EGL14.eglCreateContext(this.mEGLDisplay, configs[0], EGL14.EGL_NO_CONTEXT, attrib_list, 0);
    //    this.checkEglError("eglCreateContext");
    //    //  Create a window surface, and attach it to the Surface we received.
    //    int[] surfaceAttribs = new int[] {
    //    EGL14.EGL_NONE};
    //    this.mEGLSurface = EGL14.eglCreateWindowSurface(this.mEGLDisplay, configs[0], this.mSurface, surfaceAttribs, 0);
    //    this.checkEglError("eglCreateWindowSurface");
    //    }

    //    public void release()
    //    {
    //    if ((this.mEGLDisplay != EGL14.EGL_NO_DISPLAY))
    //    {
    //    EGL14.eglMakeCurrent(this.mEGLDisplay, EGL14.EGL_NO_SURFACE, EGL14.EGL_NO_SURFACE, EGL14.EGL_NO_CONTEXT);
    //    EGL14.eglDestroySurface(this.mEGLDisplay, this.mEGLSurface);
    //    EGL14.eglDestroyContext(this.mEGLDisplay, this.mEGLContext);
    //    EGL14.eglReleaseThread();
    //    EGL14.eglTerminate(this.mEGLDisplay);
    //    }

    //    this.mSurface.release();
    //    this.mEGLDisplay = EGL14.EGL_NO_DISPLAY;
    //    this.mEGLContext = EGL14.EGL_NO_CONTEXT;
    //    this.mEGLSurface = EGL14.EGL_NO_SURFACE;
    //    this.mSurface = null;
    //    }

    //    public void makeCurrent()
    //    {
    //    EGL14.eglMakeCurrent(this.mEGLDisplay, this.mEGLSurface, this.mEGLSurface, this.mEGLContext);
    //    this.checkEglError("eglMakeCurrent");
    //    }

    //    public bool swapBuffers()
    //    {
    //    bool result = EGL14.eglSwapBuffers(this.mEGLDisplay, this.mEGLSurface);
    //    this.checkEglError("eglSwapBuffers");
    //    return result;
    //    }

    //    public void setPresentationTime(long nsecs)
    //    {
    //    EGLExt.eglPresentationTimeANDROID(this.mEGLDisplay, this.mEGLSurface, nsecs);
    //    this.checkEglError("eglPresentationTimeANDROID");
    //    }

    //    private void checkEglError(String msg)
    //    {
    //    int error;
    //    if ((EGL14.eglGetError() != EGL14.EGL_SUCCESS))
    //    {
    //    throw new RuntimeException((msg + (": EGL error: 0x" + Integer.toHexString(error))));
    //    }

    //    }
    //}
    }


}