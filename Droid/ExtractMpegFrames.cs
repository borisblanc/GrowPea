/*
 * Copyright 2013 The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Gms.Vision;
using Android.Gms.Vision.Faces;
using Android.Media;

using Android.Graphics;

using Android.Opengl;
using Android.Util;
using Android.Views;

using Java.IO;
using Java.Lang;
using Java.Nio;
using Console = System.Console;
using Environment = Android.OS.Environment;
using File = Java.IO.File;
using FileNotFoundException = Java.IO.FileNotFoundException;
using Object = System.Object;
using String = System.String;



//20131122: minor tweaks to saveFrame() I/O
//20131205: add alpha to EGLConfig (huge glReadPixels speedup); pre-allocate pixel buffers;
//          log time to run saveFrame()
//20140123: correct error checks on glGet*Location() and program creation (they don't set error)
//20140212: eliminate byte swap

/**
 * Extract frames from an MP4 using MediaExtractor, MediaCodec, and GLES.  Put a .mp4 file
 * in "/sdcard/source.mp4" and look for output files named "/sdcard/frame-XX.png".
 * <p>
 * This uses various features first available in Android "Jellybean" 4.1 (API 16).
 * <p>
 * (This was derived from bits and pieces of CTS tests, and is packaged as such, but is not
 * currently part of CTS.)
 */
namespace GrowPea.Droid
{

    public class ExtractMpegFrames
    {
        private const String TAG = "ExtractMpegFrames";
        private const bool VERBOSE = false;   // lots of logging

        // where to find files (note: requires WRITE_EXTERNAL_STORAGE permission)
        private static File FILES_DIR = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);

        private static String INPUT_FILE;

        public Dictionary<int, Tuple<long, Face>> _framelist;

        private static readonly Object obj = new Object();

        //private List<Task> _FaceFetchDataTasks;

        protected static int _width;
        protected static int _height;


        /** test entry point */
        public ExtractMpegFrames(string inputfilename, ref Dictionary<int, Tuple<long, Face>> framelist, int width, int height)
        {
            INPUT_FILE = inputfilename;
            _framelist = framelist;
            //_FaceFetchDataTasks = FaceFetchDataTasks;
            _width = width;
            _height = height;
            ExtractMpegFramesWrapper.runTest(this);
        }

        /**
         * Wraps extractMpegFrames().  This is necessary because SurfaceTexture will try to use
         * the looper in the current thread if one exists, and the CTS tests create one on the
         * test thread.
         *
         * The wrapper propagates exceptions thrown by the worker thread back to the caller.
         */

        private class ExtractMpegFramesWrapper: Java.Lang.Object, IRunnable
        {
            private Throwable mThrowable;
            private ExtractMpegFrames mTest;

            public IntPtr Handle => base.Handle;

            private ExtractMpegFramesWrapper(ExtractMpegFrames test)
            {
                mTest = test;
            }

            //@Override
            public void Run()
            {
                try
                {
                    mTest.extractMpegFrames(INPUT_FILE, _width, _height);
                }
                catch (Throwable th)
                {
                    mThrowable = th;
                }
            }

            /** Entry point. */
            public static void runTest(ExtractMpegFrames obj) 
            {
                try
                {

                    var wrapper = new ExtractMpegFramesWrapper(obj);
                    Java.Lang.Thread th = new Java.Lang.Thread(wrapper, "codec test");
                    th.Start();
                    th.Join();
                    if (wrapper.mThrowable != null)
                    {
                        throw wrapper.mThrowable;
                    }
                }
                catch (System.Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            public void Dispose()
            {
                base.Dispose();
            }
        }

    /**
     * Tests extraction from an MP4 to a series of PNG files.
     * <p>
     * We scale the video to 640x480 for the PNG just to demonstrate that we can scale the
     * video with the GPU.  If the input video has a different aspect ratio, we could preserve
     * it by adjusting the GL viewport to get letterboxing or pillarboxing, but generally if
     * you're extracting frames you don't want black bars.
     */
    public void extractMpegFrames(string filename, int saveWidth, int saveHeight) 
    {
        MediaCodec decoder = null;
        CodecOutputSurface outputSurface = null;
        MediaExtractor extractor = null;
        INPUT_FILE = filename;

        try
        {
            File inputFile = new File(FILES_DIR, INPUT_FILE);   // must be an absolute path
                                                                // The MediaExtractor error messages aren't very useful.  Check to see if the input
                                                                // file exists so we can throw a better one if it's not there.
            if (!inputFile.CanRead())
            {
                throw new FileNotFoundException("Unable to read " + inputFile);
            }

            extractor = new MediaExtractor();
            extractor.SetDataSource(inputFile.ToString());
            int trackIndex = selectTrack(extractor);
            if (trackIndex < 0)
            {
                throw new RuntimeException("No video track found in " + inputFile);
            }
            extractor.SelectTrack(trackIndex);

            MediaFormat format = extractor.GetTrackFormat(trackIndex);
            //if (VERBOSE)
            //{
            //    Log.d(TAG, "Video size is " + format.getInteger(MediaFormat.KEY_WIDTH) + "x" +
            //            format.getInteger(MediaFormat.KEY_HEIGHT));
            //}

            // Could use width/height from the MediaFormat to get full-size frames.

            outputSurface = new CodecOutputSurface(saveWidth, saveHeight);

            // Create a MediaCodec decoder, and configure it with the MediaFormat from the
            // extractor.  It's very important to use the format from the extractor because
            // it contains a copy of the CSD-0/CSD-1 codec-specific data chunks.
            String mime = format.GetString(MediaFormat.KeyMime);
            decoder = MediaCodec.CreateDecoderByType(mime);
            decoder.Configure(format, outputSurface.getSurface(), null, 0);
            decoder.Start();

            doExtract(extractor, trackIndex, decoder, outputSurface);
        }
        finally
        {
            // release everything we grabbed
            if (outputSurface != null)
            {
                outputSurface.release();
                outputSurface = null;
            }
            if (decoder != null)
            {
                decoder.Stop();
                decoder.Release();
                decoder = null;
            }
            if (extractor != null)
            {
                extractor.Release();
                extractor = null;
            }
        }
    }

    /**
     * Selects the video track, if any.
     *
     * @return the track index, or -1 if no video track is found.
     */
    private int selectTrack(MediaExtractor extractor)
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

/**
 * Work loop.
 */
    private void doExtract(MediaExtractor extractor, int trackIndex, MediaCodec decoder, CodecOutputSurface outputSurface) 
    {
        //Stopwatch stopWatch = new Stopwatch();
        const int TIMEOUT_USEC = 10000;
        ByteBuffer []
        decoderInputBuffers = decoder.GetInputBuffers();
        MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
        int inputChunk = 0;
        int decodeCount = 0;
        var frameTimestamps = new List<long>();

        bool outputDone = false;
        bool inputDone = false;


        //speed vs accuracy tradeoffs https://stackoverflow.com/questions/34132444/google-mobile-vision-poor-facedetector-performance-without-camerasource
        //reducing bitmap resolution helps the most and thats ok because i'm not using them after
        var detector = new Android.Gms.Vision.Faces.FaceDetector.Builder(Application.Context)
        .SetTrackingEnabled(true) //tracking enables false makes it much slow wtf?!?!
        .SetClassificationType(ClassificationType.All)
        .SetProminentFaceOnly(true) // no diff
        .SetMinFaceSize((float)0.2) //small performance gain when removed
        //.SetMode(FaceDetectionMode.Fast) // tiny small performance gain 
        .Build();

        

        while (!outputDone)
        {
            //stopWatch.Start();
            // Feed more data to the decoder.
            if (!inputDone)
            {
                int inputBufIndex = decoder.DequeueInputBuffer(TIMEOUT_USEC);
                if (inputBufIndex >= 0)
                {
                    ByteBuffer inputBuf = decoderInputBuffers[inputBufIndex];
                    // Read the sample data into the ByteBuffer.  This neither respects nor
                    // updates inputBuf's position, limit, etc.
                    int chunkSize = extractor.ReadSampleData(inputBuf, 0);
                    if (chunkSize< 0)
                    {
                        // End of stream -- send empty frame with EOS flag set.
                        decoder.QueueInputBuffer(inputBufIndex, 0, 0, 0L, MediaCodec.BufferFlagEndOfStream);
                        inputDone = true;
                        //if (VERBOSE) Log.d(TAG, "sent input EOS");
                    }
                    else
                    {
                        if (extractor.SampleTrackIndex != trackIndex)
                        {
                                //Log.w(TAG, "WEIRD: got sample from track " + extractor.getSampleTrackIndex() + ", expected " + trackIndex);
                        }

                        frameTimestamps.Add(extractor.SampleTime); //might need to play with offset here to get right sync from decoder
                        decoder.QueueInputBuffer(inputBufIndex, 0, chunkSize, extractor.SampleTime, 0 /*flags*/);
                            //if (VERBOSE) {
                            //    Log.d(TAG, "submitted frame " + inputChunk + " to dec, size=" +
                            //            chunkSize);
                            //}
                        inputChunk++;
                        extractor.Advance();
                    }
                }
                else {
                    //if (VERBOSE) Log.d(TAG, "input buffer not available");
                }
            }

            if (!outputDone)
            {
                int decoderStatus = decoder.DequeueOutputBuffer(info, TIMEOUT_USEC);
                if (decoderStatus == (int)MediaCodecInfoState.TryAgainLater)
                {
                    // no output available yet
                    //if (VERBOSE) Log.d(TAG, "no output from decoder available");
                }
                else if (decoderStatus == (int)MediaCodecInfoState.OutputBuffersChanged)
                {
                    // not important for us, since we're using Surface
                    //if (VERBOSE) Log.d(TAG, "decoder output buffers changed");
                }
                else if (decoderStatus == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    //MediaFormat newFormat = decoder.OutputFormat;
                    //if (VERBOSE) Log.d(TAG, "decoder output format changed: " + newFormat);
                }
                else if (decoderStatus< 0)
                {
                    //fail("unexpected result from decoder.dequeueOutputBuffer: " + decoderStatus);
                    throw new InvalidOperationException();
                }
                else
                { 
                    //if (VERBOSE) Log.d(TAG, "surface decoder given buffer " + decoderStatus + " (size=" + info.size + ")");
                    if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                    {
                        //if (VERBOSE) Log.d(TAG, "output EOS");
                        outputDone = true;
                    }

                    bool doRender = (info.Size != 0);

                    //could not get this working!!!
                    // As soon as we call releaseOutputBuffer, the buffer will be forwarded
                    // to SurfaceTexture to convert to a texture.  The API doesn't guarantee
                    // that the texture will be available before the call returns, so we
                    // need to wait for the onFrameAvailable callback to fire.

                    decoder.ReleaseOutputBuffer(decoderStatus, doRender);

                    if (doRender) {
                        //outputSurface.awaitNewImage(); //could not get callback to work and even so do not want to wait 2.5 seconds for each frame, might need to revist
                        
                        outputSurface.mTextureRender.checkGlError("before updateTexImage");
                        outputSurface.mSurfaceTexture.UpdateTexImage();
                        outputSurface.drawImage(true);
                        //Log.Info("innerSTOPWATCH_begin!!!!:", stopWatch.ElapsedMilliseconds.ToString());
                        //can't call face detector this way its too slow or maybe there is a busy loop???
                        //_FaceFetchDataTasks.Add(Task.Run(() => CreateFaceframes(detector, outputSurface.GetFramebitmap(), decodeCount, frameTimestamps[decodeCount])));
                        CreateFaceframes(detector, outputSurface.GetFramebitmap(), decodeCount, frameTimestamps[decodeCount]);
                        //Log.Info("innerSTOPWATCH_end!!!!:", stopWatch.ElapsedMilliseconds.ToString());

                        decodeCount++;
                    }
                }
            }
        }
        
        //stopWatch.Stop();
        //Log.Info("STOPWATCH!!!!:", stopWatch.ElapsedMilliseconds.ToString());
        detector.Release();
    }

    private void CreateFaceframes(Android.Gms.Vision.Faces.FaceDetector detector, Bitmap b, int index, long timestamp)
    {
        try
        {
            Frame newframe = new Frame.Builder().SetBitmap(b).Build();
            SparseArray faces = detector.Detect(newframe); //takes longest

            lock (obj)
            {
                if (!_framelist.ContainsKey(index))
                _framelist.Add(index, new Tuple<long, Face>(timestamp, Utils.GetSparseFace(faces)));
            }
        }
        catch (System.Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            b.Recycle();
            Log.Info("CreateFaceframes!!!", string.Format("Frame number {0} Processed!!!!!!!", index));
        }
    }


    /**
     * Holds state associated with a Surface used for MediaCodec decoder output.
     * <p>
     * The constructor for this class will prepare GL, create a SurfaceTexture,
     * and then create a Surface for that SurfaceTexture.  The Surface can be passed to
     * MediaCodec.configure() to receive decoder output.  When a frame arrives, we latch the
     * texture with updateTexImage(), then render the texture with GL to a pbuffer.
     * <p>
     * By default, the Surface will be using a BufferQueue in asynchronous mode, so we
     * can potentially drop frames.
     */
    private class CodecOutputSurface: Java.Lang.Object //,SurfaceTexture.IOnFrameAvailableListener //could not get callback to work and even so do not want to wait 2.5 seconds for each frame, might need to revist
    {
        public STextureRender mTextureRender;
        public SurfaceTexture mSurfaceTexture;
        private Surface mSurface;

        private EGLDisplay mEGLDisplay = EGL14.EglNoDisplay;
        private EGLContext mEGLContext = EGL14.EglNoContext;
        private EGLSurface mEGLSurface = EGL14.EglNoSurface;
        int mWidth;
        int mHeight;

        //private Object mFrameSyncObject = new Object();   // guards mFrameAvailable
        private bool mFrameAvailable;

        private ByteBuffer mPixelBuf;                       // used by saveFrame()

        public CodecOutputSurface() { }

        /**
         * Creates a CodecOutputSurface backed by a pbuffer with the specified dimensions.  The
         * new EGL context and surface will be made current.  Creates a Surface that can be passed
         * to MediaCodec.configure().
         */
        public CodecOutputSurface(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new IllegalArgumentException();
            }
            mWidth = width;
            mHeight = height;

            eglSetup();
            makeCurrent();
            setup();
        }

        /**
         * Creates interconnected instances of TextureRender, SurfaceTexture, and Surface.
         */
        private void setup()
        {
            try
            {
                mTextureRender = new STextureRender();
                mTextureRender.surfaceCreated();

                //if (VERBOSE) Log.d(TAG, "textureID=" + mTextureRender.getTextureId());
                mSurfaceTexture = new SurfaceTexture(mTextureRender.getTextureId());

                // This doesn't work if this object is created on the thread that CTS started for
                // these test cases.
                //
                // The CTS-created thread has a Looper, and the SurfaceTexture constructor will
                // create a Handler that uses it.  The "frame available" message is delivered
                // there, but since we're not a Looper-based thread we'll never see it.  For
                // this to do anything useful, CodecOutputSurface must be created on a thread without
                // a Looper, so that SurfaceTexture uses the main application Looper instead.
                //
                // Java language note: passing "this" out of a constructor is generally unwise,
                // but we should be able to get away with it here.

                //removed because could not get callback to work and even so do not want to wait 2.5 seconds for each frame, might need to revist
                //mSurfaceTexture.SetOnFrameAvailableListener(this);

                mSurface = new Surface(mSurfaceTexture);

                mPixelBuf = ByteBuffer.AllocateDirect(mWidth * mHeight * 4);
                mPixelBuf.Order(ByteOrder.LittleEndian);
            }
            catch (System.Exception e)
            {
                var x = e;
            }
        }

        /**
         * Prepares EGL.  We want a GLES 2.0 context and a surface that supports pbuffer.
         */
        private void eglSetup()
        {
            mEGLDisplay = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
            if (mEGLDisplay == EGL14.EglNoDisplay)
            {
                throw new RuntimeException("unable to get EGL14 display");
            }
            int[] version = new int[2];
            if (!EGL14.EglInitialize(mEGLDisplay, version, 0, version, 1))
            {
                mEGLDisplay = null;
                throw new RuntimeException("unable to initialize EGL14");
            }

            // Configure EGL for pbuffer and OpenGL ES 2.0, 24-bit RGB.
            int[] attribList = {
                            EGL14.EglRedSize, 8,
                            EGL14.EglGreenSize, 8,
                            EGL14.EglBlueSize, 8,
                            EGL14.EglAlphaSize, 8,
                            EGL14.EglRenderableType, EGL14.EglOpenglEs2Bit,
                            EGL14.EglSurfaceType, EGL14.EglPbufferBit,
                            EGL14.EglNone };

            EGLConfig[] configs = new EGLConfig[1];
            int[] numConfigs = new int[1];
            if (!EGL14.EglChooseConfig(mEGLDisplay, attribList, 0, configs, 0, configs.Length, numConfigs, 0))
            {
                throw new RuntimeException("unable to find RGB888+recordable ES2 EGL config");
            }

            // Configure context for OpenGL ES 2.0.
            int[] attrib_list = {
                            EGL14.EglContextClientVersion, 2,
                            EGL14.EglNone
                    };

            mEGLContext = EGL14.EglCreateContext(mEGLDisplay, configs[0], EGL14.EglNoContext, attrib_list, 0);
            checkEglError("eglCreateContext");

            if (mEGLContext == null)
            {
                throw new RuntimeException("null context");
            }

            // Create a pbuffer surface.
            int[] surfaceAttribs = {
                            EGL14.EglWidth, mWidth,
                            EGL14.EglHeight, mHeight,
                            EGL14.EglNone
                    };

            mEGLSurface = EGL14.EglCreatePbufferSurface(mEGLDisplay, configs[0], surfaceAttribs, 0);

            checkEglError("eglCreatePbufferSurface");
            if (mEGLSurface == null)
            {
                throw new RuntimeException("surface was null");
            }
        }

        /**
         * Discard all resources held by this class, notably the EGL context.
         */
        public void release()
        {
            if (mEGLDisplay != EGL14.EglNoDisplay)
            {
                EGL14.EglDestroySurface(mEGLDisplay, mEGLSurface);
                EGL14.EglDestroyContext(mEGLDisplay, mEGLContext);
                EGL14.EglReleaseThread();
                EGL14.EglTerminate(mEGLDisplay);
            }
            mEGLDisplay = EGL14.EglNoDisplay;
            mEGLContext = EGL14.EglNoContext;
            mEGLSurface = EGL14.EglNoSurface;

            mSurface.Release();

            // this causes a bunch of warnings that appear harmless but might confuse someone:
            //  W BufferQueue: [unnamed-3997-2] cancelBuffer: BufferQueue has been abandoned!
            //mSurfaceTexture.release();

            mTextureRender = null;
            mSurface = null;
            mSurfaceTexture = null;
        }

        /**
         * Makes our EGL context and surface current.
         */
        public void makeCurrent()
        {
            if (!EGL14.EglMakeCurrent(mEGLDisplay, mEGLSurface, mEGLSurface, mEGLContext))
            {
                throw new RuntimeException("eglMakeCurrent failed");
            }
        }

        /**
         * Returns the Surface.
         */
        public Surface getSurface()
        {
            return mSurface;
        }

        /**
        * Latches the next buffer into the texture.  Must be called from the thread that created
        * the CodecOutputSurface object.  (More specifically, it must be called on the thread
        * with the EGLContext that contains the GL texture object used by SurfaceTexture.)
        */
        //[MethodImpl(MethodImplOptions.Synchronized)]
        //public void awaitNewImage() //could not get callback to work and even so do not want to wait 2.5 seconds for each frame, might need to revist
        //{
        //    Monitor.Enter(mFrameSyncObject);
        //    try
        //    {
        //        while (!mFrameAvailable)
        //        {

        //            // Wait for onFrameAvailable() to signal us.  Use a timeout to avoid
        //            // stalling the test if it doesn't arrive.
        //            Monitor.Wait(mFrameSyncObject, 2500);
        //            if (!mFrameAvailable)
        //            {
        //                // TODO: if "spurious wakeup", continue while loop
        //                throw new System.Exception("frame wait timed out");
        //            }


        //        }
        //        mFrameAvailable = false;
        //    }
        //    finally
        //    {
        //        Monitor.Exit(mFrameSyncObject);
        //    }

        //        //Latch the data.
        //   mTextureRender.checkGlError("before updateTexImage");
        //    mSurfaceTexture.UpdateTexImage();
        //}

        /**
         * Draws the data from SurfaceTexture onto the current EGL surface.
         *
         * @param invert if set, render the image with Y inverted (0,0 in top left)
         */
        public void drawImage(bool invert)
        {
            mTextureRender.drawFrame(mSurfaceTexture, invert);
        }

        //// SurfaceTexture callback
        //[MethodImpl(MethodImplOptions.Synchronized)]
        //public void OnFrameAvailable(SurfaceTexture st)
        //{
        //    //if (VERBOSE) Log.d(TAG, "new frame available");
        //    Monitor.Enter(mFrameSyncObject);
        //    try
        //    {
        //        if (mFrameAvailable)
        //        {
        //            throw new System.Exception("mFrameAvailable already set, frame could be dropped");
        //        }

        //        mFrameAvailable = true;
        //        Monitor.PulseAll(mFrameSyncObject);

        //    }
        //    finally
        //    {
        //        Monitor.Exit(mFrameSyncObject);
        //    }
        //}

        /**
         * Saves the current frame to disk as a PNG image.
         */
        public void saveFrame(String filename) 
        {
            // glReadPixels gives us a ByteBuffer filled with what is essentially big-endian RGBA
            // data (i.e. a byte of red, followed by a byte of green...).  To use the Bitmap
            // constructor that takes an int[] array with pixel data, we need an int[] filled
            // with little-endian ARGB data.
            //
            // If we implement this as a series of buf.get() calls, we can spend 2.5 seconds just
            // copying data around for a 720p frame.  It's better to do a bulk get() and then
            // rearrange the data in memory.  (For comparison, the PNG compress takes about 500ms
            // for a trivial frame.)
            //
            // So... we set the ByteBuffer to little-endian, which should turn the bulk IntBuffer
            // get() into a straight memcpy on most Android devices.  Our ints will hold ABGR data.
            // Swapping B and R gives us ARGB.  We need about 30ms for the bulk get(), and another
            // 270ms for the color swap.
            //
            // We can avoid the costly B/R swap here if we do it in the fragment shader (see
            // http://stackoverflow.com/questions/21634450/ ).
            //
            // Having said all that... it turns out that the Bitmap#copyPixelsFromBuffer()
            // method wants RGBA pixels, not ARGB, so if we create an empty bitmap and then
            // copy pixel data in we can avoid the swap issue entirely, and just copy straight
            // into the Bitmap from the ByteBuffer.
            //
            // Making this even more interesting is the upside-down nature of GL, which means
            // our output will look upside-down relative to what appears on screen if the
            // typical GL conventions are used.  (For ExtractMpegFrameTest, we avoid the issue
            // by inverting the frame when we render it.)
            //
            // Allocating large buffers is expensive, so we really want mPixelBuf to be
            // allocated ahead of time if possible.  We still get some allocations from the
            // Bitmap / PNG creation.

            mPixelBuf.Rewind();
            GLES20.GlReadPixels(0, 0, mWidth, mHeight, GLES20.GlRgba, GLES20.GlUnsignedByte, mPixelBuf);

            var createfilepath = new Java.IO.File(FILES_DIR, filename + ".bmp").AbsolutePath;
            using (FileStream bos = new FileStream(createfilepath, FileMode.CreateNew))
            {
                try
                {
                    Bitmap bmp = Bitmap.CreateBitmap(mWidth, mHeight, Bitmap.Config.Argb8888);
                    mPixelBuf.Rewind();
                    bmp.CopyPixelsFromBuffer(mPixelBuf);
                    bmp.Compress(Bitmap.CompressFormat.Png, 90, bos);
                    bmp.Recycle();
                }
                finally
                {
                    bos.Close();
                }
            }
            //if (VERBOSE) {
            //    Log.d(TAG, "Saved " + mWidth + "x" + mHeight + " frame as '" + filename + "'");
            //}
        }

        public Bitmap GetFramebitmap() //try to speed this up later
        {
            mPixelBuf.Rewind();
            GLES20.GlReadPixels(0, 0, mWidth, mHeight, GLES20.GlRgba, GLES20.GlUnsignedByte, mPixelBuf);
            Bitmap bmp = Bitmap.CreateBitmap(mWidth, mHeight, Bitmap.Config.Argb8888);
            mPixelBuf.Rewind();
            bmp.CopyPixelsFromBuffer(mPixelBuf);
            //bmp.Compress(Bitmap.CompressFormat.Png, 90, bos);

            return bmp;
            //bmp.Recycle();
        }

        /**
         * Checks for EGL errors.
         */
        private void checkEglError(String msg)
        {
            int error;
            if ((error = EGL14.EglGetError()) != EGL14.EglSuccess)
            {
                throw new RuntimeException(msg + ": EGL error: 0x" + Integer.ToHexString(error));
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public IntPtr Handle { get; }

    }


    /**
     * Code for rendering a texture onto a surface using OpenGL ES 2.0.
     */
    private class STextureRender
    {
        private static int FLOAT_SIZE_BYTES = 4;
        private static int TRIANGLE_VERTICES_DATA_STRIDE_BYTES = 5 * FLOAT_SIZE_BYTES;
        private static int TRIANGLE_VERTICES_DATA_POS_OFFSET = 0;
        private static int TRIANGLE_VERTICES_DATA_UV_OFFSET = 3;

        private static float[] mTriangleVerticesData = {
                    // X, Y, Z, U, V
                    -1.0f, -1.0f, 0, 0.0f, 0.0f,
                     1.0f, -1.0f, 0, 1.0f, 0.0f,
                    -1.0f,  1.0f, 0, 0.0f, 1.0f,
                     1.0f,  1.0f, 0, 1.0f, 1.0f,
            };

        private FloatBuffer mTriangleVertices;

        private static String VERTEX_SHADER =
                "uniform mat4 uMVPMatrix;\n" +
                "uniform mat4 uSTMatrix;\n" +
                "attribute vec4 aPosition;\n" +
                "attribute vec4 aTextureCoord;\n" +
                "varying vec2 vTextureCoord;\n" +
                "void main() {\n" +
                "    gl_Position = uMVPMatrix * aPosition;\n" +
                "    vTextureCoord = (uSTMatrix * aTextureCoord).xy;\n" +
                "}\n";

        private static String FRAGMENT_SHADER =
                "#extension GL_OES_EGL_image_external : require\n" +
                "precision mediump float;\n" +      // highp here doesn't seem to matter
                "varying vec2 vTextureCoord;\n" +
                "uniform samplerExternalOES sTexture;\n" +
                "void main() {\n" +
                "    gl_FragColor = texture2D(sTexture, vTextureCoord);\n" +
                "}\n";

        private float[] mMVPMatrix = new float[16];
        private float[] mSTMatrix = new float[16];

        private int mProgram;
        private int mTextureID = -12345;
        private int muMVPMatrixHandle;
        private int muSTMatrixHandle;
        private int maPositionHandle;
        private int maTextureHandle;

        public STextureRender()
        {
            mTriangleVertices = ByteBuffer.AllocateDirect(
                    mTriangleVerticesData.Length * FLOAT_SIZE_BYTES)
                    .Order(ByteOrder.NativeOrder()).AsFloatBuffer();
            mTriangleVertices.Put(mTriangleVerticesData).Position(0);

            Android.Opengl.Matrix.SetIdentityM(mSTMatrix, 0);
        }

        public int getTextureId()
        {
            return mTextureID;
        }

        /**
         * Draws the external texture in SurfaceTexture onto the current EGL surface.
         */
        public void drawFrame(SurfaceTexture st, bool invert)
        {
            checkGlError("onDrawFrame start");
            st.GetTransformMatrix(mSTMatrix);
            if (invert)
            {
                mSTMatrix[5] = -mSTMatrix[5];
                mSTMatrix[13] = 1.0f - mSTMatrix[13];
            }

            // (optional) clear to green so we can see if we're failing to set pixels
            GLES20.GlClearColor(0.0f, 1.0f, 0.0f, 1.0f);
            GLES20.GlClear(GLES20.GlColorBufferBit);

            GLES20.GlUseProgram(mProgram);
            checkGlError("glUseProgram");

            GLES20.GlActiveTexture(GLES20.GlTexture0);
            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, mTextureID);

            mTriangleVertices.Position(TRIANGLE_VERTICES_DATA_POS_OFFSET);
            GLES20.GlVertexAttribPointer(maPositionHandle, 3, GLES20.GlFloat, false,
                    TRIANGLE_VERTICES_DATA_STRIDE_BYTES, mTriangleVertices);
            checkGlError("glVertexAttribPointer maPosition");
            GLES20.GlEnableVertexAttribArray(maPositionHandle);
            checkGlError("glEnableVertexAttribArray maPositionHandle");

            mTriangleVertices.Position(TRIANGLE_VERTICES_DATA_UV_OFFSET);
            GLES20.GlVertexAttribPointer(maTextureHandle, 2, GLES20.GlFloat, false,
                    TRIANGLE_VERTICES_DATA_STRIDE_BYTES, mTriangleVertices);
            checkGlError("glVertexAttribPointer maTextureHandle");
            GLES20.GlEnableVertexAttribArray(maTextureHandle);
            checkGlError("glEnableVertexAttribArray maTextureHandle");

            Android.Opengl.Matrix.SetIdentityM(mMVPMatrix, 0);
            GLES20.GlUniformMatrix4fv(muMVPMatrixHandle, 1, false, mMVPMatrix, 0);
            GLES20.GlUniformMatrix4fv(muSTMatrixHandle, 1, false, mSTMatrix, 0);

            GLES20.GlDrawArrays(GLES20.GlTriangleStrip, 0, 4);
            checkGlError("glDrawArrays");

            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, 0);
        }

        /**
         * Initializes GL state.  Call this after the EGL surface has been created and made current.
         */
        public void surfaceCreated()
        {
            mProgram = createProgram(VERTEX_SHADER, FRAGMENT_SHADER);
            if (mProgram == 0)
            {
                throw new RuntimeException("failed creating program");
            }

            maPositionHandle = GLES20.GlGetAttribLocation(mProgram, "aPosition");
            checkLocation(maPositionHandle, "aPosition");
            maTextureHandle = GLES20.GlGetAttribLocation(mProgram, "aTextureCoord");
            checkLocation(maTextureHandle, "aTextureCoord");

            muMVPMatrixHandle = GLES20.GlGetUniformLocation(mProgram, "uMVPMatrix");
            checkLocation(muMVPMatrixHandle, "uMVPMatrix");
            muSTMatrixHandle = GLES20.GlGetUniformLocation(mProgram, "uSTMatrix");
            checkLocation(muSTMatrixHandle, "uSTMatrix");

            int[] textures = new int[1];
            GLES20.GlGenTextures(1, textures, 0);

            mTextureID = textures[0];
            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, mTextureID);
            checkGlError("glBindTexture mTextureID");

            GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMinFilter,
                    GLES20.GlNearest);
            GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMagFilter,
                    GLES20.GlLinear);
            GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapS,
                    GLES20.GlClampToEdge);
            GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapT,
                    GLES20.GlClampToEdge);
            checkGlError("glTexParameter");
        }

        /**
         * Replaces the fragment shader.  Pass in null to reset to default.
         */
        public void changeFragmentShader(String fragmentShader)
        {
            if (fragmentShader == null)
            {
                fragmentShader = FRAGMENT_SHADER;
            }
            GLES20.GlDeleteProgram(mProgram);
            mProgram = createProgram(VERTEX_SHADER, fragmentShader);
            if (mProgram == 0)
            {
                throw new RuntimeException("failed creating program");
            }
        }

        private int loadShader(int shaderType, String source)
        {
            int shader = GLES20.GlCreateShader(shaderType);
            checkGlError("glCreateShader type=" + shaderType);
            GLES20.GlShaderSource(shader, source);
            GLES20.GlCompileShader(shader);
            int[] compiled = new int[1];
            GLES20.GlGetShaderiv(shader, GLES20.GlCompileStatus, compiled, 0);
            if (compiled[0] == 0)
            {
                //Log.e(TAG, "Could not compile shader " + shaderType + ":");
                //Log.e(TAG, " " + GLES20.glGetShaderInfoLog(shader));
                GLES20.GlDeleteShader(shader);
                shader = 0;
            }
            return shader;
        }

        private int createProgram(String vertexSource, String fragmentSource)
        {
            int vertexShader = loadShader(GLES20.GlVertexShader, vertexSource);
            if (vertexShader == 0)
            {
                return 0;
            }
            int pixelShader = loadShader(GLES20.GlFragmentShader, fragmentSource);
            if (pixelShader == 0)
            {
                return 0;
            }

            int program = GLES20.GlCreateProgram();
            //if (program == 0)
            //{
            //    Log.e(TAG, "Could not create program");
            //}
            GLES20.GlAttachShader(program, vertexShader);
            checkGlError("glAttachShader");
            GLES20.GlAttachShader(program, pixelShader);
            checkGlError("glAttachShader");
            GLES20.GlLinkProgram(program);
            int[] linkStatus = new int[1];
            GLES20.GlGetProgramiv(program, GLES20.GlLinkStatus, linkStatus, 0);
            if (linkStatus[0] != GLES20.GlTrue)
            {
                //Log.e(TAG, "Could not link program: ");
                //Log.e(TAG, GLES20.glGetProgramInfoLog(program));
                GLES20.GlDeleteProgram(program);
                program = 0;
            }
            return program;
        }

        public void checkGlError(String op)
        {
            int error;
            while ((error = GLES20.GlGetError()) != GLES20.GlNoError)
            {
                Log.Error(TAG, "glError: " + error);
                throw new RuntimeException(op + ": glError " + error);
            }
        }

        public static void checkLocation(int location, String label)
        {
            if (location < 0)
            {
                throw new RuntimeException("Unable to locate '" + label + "' in program");
            }
        }
    }
}

}