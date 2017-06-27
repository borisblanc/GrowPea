using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Opengl;
using Android.Test;
using Android.Util;
using Android.Views;
using Java.Lang;
using Java.Nio;
using Javax.Microedition.Khronos.Opengles;
using Environment = Android.OS.Environment;
using Exception = System.Exception;
using File = Java.IO.File;
using IOException = Java.IO.IOException;
using String = System.String;

// 20131106: removed hard-coded "/sdcard"
// 20131205: added alpha to EGLConfig



namespace GrowPea.Droid
{
    public class SurfaceEncoderMuxer
    { 

        private static String TAG = "EncoderMuxer";


        private static string _Filepath;

        //  parameters for the encoder
        private static String MIME_TYPE = "video/avc";

        //  H.264 Advanced Video Coding
        private static int FRAME_RATE = 10;

        //  15fps
        private static int IFRAME_INTERVAL = 10;

        private static long DURATION_SEC = 3;             // 3 seconds of video

        private static String SWAPPED_FRAGMENT_SHADER =
        "#extension GL_OES_EGL_image_external : require\n" +
        "precision mediump float;\n" +
        "varying vec2 vTextureCoord;\n" +
        "uniform samplerExternalOES sTexture;\n" +
        "void main() {\n" +
        "  gl_FragColor = texture2D(sTexture, vTextureCoord).gbra;\n" +
        "}\n";

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

        private CodecInputSurface mInputSurface;

        //  allocate one of these up front so we don't need to do it every time
        private MediaCodec.BufferInfo mBufferInfo;

        private List<ByteBuffer> _ByteBuffers;

        public SurfaceEncoderMuxer(int width, int height, int bitRate, string oFilePath, List<ByteBuffer> byteBuffers)
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
                mInputSurface.makeCurrent();


                for (int i = 0; i < _ByteBuffers.Count; i++)
                {

                    drainEncoder(false);
                    // Generate a new frame of input.
                    //Bitmap b = GetBitmap(_ByteBuffers[i]);
                    //encodeFrame(b);
                    generateSurfaceFrame(i);
                    mInputSurface.setPresentationTime(computePresentationTimeNsec(i));

                    // Submit it to the encoder.  The eglSwapBuffers call will block if the input
                    // is full, which would be bad if it stayed full until we dequeued an output
                    // buffer (which we can't do, since we're stuck here).  So long as we fully drain
                    // the encoder before supplying additional input, the system guarantees that we
                    // can supply another frame without blocking.
                    mInputSurface.swapBuffers();
                    //b.Recycle();
                }
                drainEncoder(true);
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

            int colorFormat = (int)MediaCodecInfo.CodecCapabilities.COLORFormatSurface;

            format.SetInteger(MediaFormat.KeyColorFormat, colorFormat);
            format.SetInteger(MediaFormat.KeyBitRate, _BitRate);
            format.SetInteger(MediaFormat.KeyFrameRate, FRAME_RATE);
            format.SetInteger(MediaFormat.KeyIFrameInterval, IFRAME_INTERVAL);


            _Encoder = MediaCodec.CreateByCodecName(codecInfo.Name);
            _Encoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);
            mInputSurface = new CodecInputSurface(_Encoder.CreateInputSurface());
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

        private static long computePresentationTimeNsec(int frameIndex)
        {
            long ONE_BILLION = 1000000000;
            return (frameIndex * (ONE_BILLION / FRAME_RATE));
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

            Log.Warn(TAG, string.Format("couldn\'t find a good color format for codec {0} and mime {1}", codecInfo.Name, mimeType));
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

            if (mInputSurface != null)
            {
                mInputSurface.release();
                mInputSurface = null;
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

        private void drainEncoder(bool endofstream)
        {
            int TIMEOUT_USEC = 10000;

            bool inputDone = false;
            int frameIndex = 0;
            try
            {
                if (endofstream)
                {
                    Log.Info(TAG, "sending EOS to encoder");
                    _Encoder.SignalEndOfInputStream();
                }

                while (true)
                {
                    ByteBuffer[] encoderOutputBuffers = _Encoder.GetOutputBuffers();

                    int encoderStatus = _Encoder.DequeueOutputBuffer(mBufferInfo, TIMEOUT_USEC);

                    if (encoderStatus == (int)MediaCodecInfoState.TryAgainLater)
                    {
                        //if (!endofstream)
                        //{
                        //    break; // out of while
                        //}
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

        private void generateSurfaceFrame(int frameIndex)
        {
            frameIndex %= 8;
            int startX, startY;
            if (frameIndex < 4)
            {
                // (0,0) is bottom-left in GL
                startX = frameIndex * (_Width / 4);
                startY = _Height / 2;
            }
            else
            {
                startX = (7 - frameIndex) * (_Width / 4);
                startY = 0;
            }
            GLES20.GlDisable(GLES20.GlScissorTest);
            GLES20.GlClearColor(TEST_R0 / 255.0f, TEST_G0 / 255.0f, TEST_B0 / 255.0f, 1.0f);
            GLES20.GlClear(GLES20.GlColorBufferBit);
            GLES20.GlEnable(GLES20.GlScissorTest);
            GLES20.GlScissor(startX, startY, _Width / 4, _Height / 2);
            GLES20.GlClearColor(TEST_R1 / 255.0f, TEST_G1 / 255.0f, TEST_B1 / 255.0f, 1.0f);
            GLES20.GlClear(GLES20.GlColorBufferBit);
        }

        // This is called for each frame to be rendered into the video file
        private void encodeFrame(Bitmap bitmap)
        {
            int textureId = 0;

            try
            {
                textureId = loadTexture(bitmap);

                // render the texture here?
            }
            finally
            {
                unloadTexture(textureId);
            }
        }

        // Loads a texture into OpenGL
        private int loadTexture(Bitmap bitmap)
        {
            int[] textures = new int[1];
            GLES20.GlGenTextures(1, textures, 0);

            int textureWidth = bitmap.Width;
            int textureHeight = bitmap.Height;

            GLES20.GlBindTexture(GLES20.GlTexture2d, textures[0]);
            GLUtils.TexImage2D(GLES20.GlTexture2d, 0, bitmap, 0);

            GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureMinFilter, GLES20.GlLinear);
            GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureMagFilter, GLES20.GlNearest);
            GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapS, GLES20.GlClampToEdge);
            GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapT, GLES20.GlClampToEdge);

            return textures[0];
        }

        // Unloads a texture from OpenGL
        private void unloadTexture(int textureId)
        {
            int[] textures = new int[1];
            textures[0] = textureId;

            GLES20.GlDeleteTextures(1, textures, 0);
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


    }


    /**
     * Holds state associated with a Surface used for MediaCodec encoder input.
     * <p>
     * The constructor takes a Surface obtained from MediaCodec.createInputSurface(), and uses
     * that to create an EGL window surface.  Calls to eglSwapBuffers() cause a frame of data to
     * be sent to the video encoder.
     * <p>
     * This object owns the Surface -- releasing this will release the Surface too.
     */
    public class CodecInputSurface
    {

        private const int EGL_RECORDABLE_ANDROID = 12610;

        private EGLDisplay mEGLDisplay = EGL14.EglNoDisplay;

        private EGLContext mEGLContext = EGL14.EglNoContext;

        private EGLSurface mEGLSurface = EGL14.EglNoSurface;

        private Surface mSurface;

        public CodecInputSurface(Surface surface)
        {
            if (surface == null)
            {
                throw new NullPointerException();
            }

            this.mSurface = surface;
            this.eglSetup();

        }

        private void eglSetup()
        {
            this.mEGLDisplay = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
            if (this.mEGLDisplay == EGL14.EglNoDisplay)
            {
                throw new RuntimeException("unable to get EGL14 display");
            }

            int[] version = new int[2];
            if (!EGL14.EglInitialize(this.mEGLDisplay, version, 0, version, 1))
            {
                throw new RuntimeException("unable to initialize EGL14");
            }

            //  Configure EGL for recording and OpenGL ES 2.0.
            int[] attribList = new int[] { EGL14.EglRedSize,8, EGL14.EglGreenSize, 8,EGL14.EglBlueSize,8,
                EGL14.EglAlphaSize,8, EGL14.EglRenderableType, EGL14.EglOpenglEs2Bit,
                EGL_RECORDABLE_ANDROID, 1, EGL14.EglNone};

            EGLConfig[] configs = new EGLConfig[1];
            int[] numConfigs = new int[1];
            EGL14.EglChooseConfig(this.mEGLDisplay, attribList, 0, configs, 0, configs.Length, numConfigs, 0);
            this.checkEglError("eglCreateContext RGB888+recordable ES2");
            //  Configure context for OpenGL ES 2.0.
            int[] attrib_list = new int[] { EGL14.EglContextClientVersion,2, EGL14.EglNone};
            this.mEGLContext = EGL14.EglCreateContext(this.mEGLDisplay, configs[0], EGL14.EglNoContext, attrib_list, 0);
            this.checkEglError("eglCreateContext");
            //  Create a window surface, and attach it to the Surface we received.
            int[] surfaceAttribs = new int[] {EGL14.EglNone};
            this.mEGLSurface = EGL14.EglCreateWindowSurface(this.mEGLDisplay, configs[0], this.mSurface, surfaceAttribs, 0);
            this.checkEglError("eglCreateWindowSurface");
        }

        public void release()
        {
            if (this.mEGLDisplay != EGL14.EglNoDisplay)
            {
                EGL14.EglMakeCurrent(this.mEGLDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext);
                EGL14.EglDestroySurface(this.mEGLDisplay, this.mEGLSurface);
                EGL14.EglDestroyContext(this.mEGLDisplay, this.mEGLContext);
                EGL14.EglReleaseThread();
                EGL14.EglTerminate(this.mEGLDisplay);
            }

            this.mSurface.Release();
            this.mEGLDisplay = EGL14.EglNoDisplay;
            this.mEGLContext = EGL14.EglNoContext;
            this.mEGLSurface = EGL14.EglNoSurface;
            this.mSurface = null;
        }

        public void makeCurrent()
        {
            EGL14.EglMakeCurrent(this.mEGLDisplay, this.mEGLSurface, this.mEGLSurface, this.mEGLContext);
            this.checkEglError("eglMakeCurrent");
        }

        public bool swapBuffers()
        {
            bool result = EGL14.EglSwapBuffers(this.mEGLDisplay, this.mEGLSurface);
            this.checkEglError("eglSwapBuffers");
            return result;
        }

        public void setPresentationTime(long nsecs)
        {
            EGLExt.EglPresentationTimeANDROID(this.mEGLDisplay, this.mEGLSurface, nsecs);
            this.checkEglError("eglPresentationTimeANDROID");
        }

        private void checkEglError(String msg)
        {
            int error = 0;
            if ((EGL14.EglGetError() != EGL14.EglSuccess))
            {
                throw new RuntimeException((msg + (": EGL error: 0x" + Integer.ToHexString(error))));
            }

        }
    }


    public class Texture
    {

        protected String name;
        protected int textureID = -1;
        protected String filename;

        public Texture()
        {
            //this.filename = filename;
        }

        public void loadTexture(int index, Bitmap b)
        {

            //String[] filenamesplit = filename.Split("\\.");

            //name = filenamesplit[filenamesplit.Length - 2];

            int[] textures = new int[1];
            //Generate one texture pointer...
            //GLES20.glGenTextures(1, textures, 0);

            // texturecount is just a public int in MyActivity extends Activity
            // I use this because I have issues with glGenTextures() not working                
            textures[0] = index;
            //((MyActivity)context).texturecount++;

            GLES20.GlBindTexture(GLES20.GlTexture2d, textures[0]);

            //Create Nearest Filtered Texture
            GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureMinFilter, GLES20.GlNearest);
            GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureMagFilter, GLES20.GlLinear);

            //Different possible texture parameters, e.g. GLES20.GL_CLAMP_TO_EDGE
            GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureWrapS, GLES20.GlRepeat);
            GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureWrapT, GLES20.GlRepeat);



            GLUtils.TexImage2D(GLES20.GlTexture2d, 0, b, 0);

            //bitmap.Recycle();

            textureID = textures[0];

        }

        //public String getName()
        //{
        //    return name;
        //}

        //public void setName(String name)
        //{
        //    this.name = name;
        //}

        public int getTextureID()
        {
            return textureID;
        }

        public void setTextureID(int textureID)
        {
            this.textureID = textureID;
        }


    }

}