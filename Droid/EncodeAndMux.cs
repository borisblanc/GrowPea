


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
//using Android.Media;
//using Android.OS;
//using Android.Runtime;
//using Android.Util;
//using Android.Views;
//using Android.Widget;
//using Java.Lang;
//using String = System.String;

//// 20131106: removed hard-coded "/sdcard"
//// 20131205: added alpha to EGLConfig
//public class EncodeAndMux //: AndroidTestCase
//{

//    private static String TAG = "EncodeAndMuxTest";

//    //private static bool VERBOSE = false;

//    //  lots of logging
//    //  where to put the output file (note: /sdcard requires WRITE_EXTERNAL_STORAGE permission)
//    private static File OUTPUT_DIR = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);

//    //  parameters for the encoder
//    private static String MIME_TYPE = "video/avc";

//    //  H.264 Advanced Video Coding
//    private static int FRAME_RATE = 15;

//    //  15fps
//    private static int IFRAME_INTERVAL = 10;

//    //  10 seconds between I-frames
//    private static int NUM_FRAMES = 30;

//    //  two seconds of video
//    //  RGB color values for generated frames
//    //private static int TEST_R0 = 0;

//    //private static int TEST_G0 = 136;

//    //private static int TEST_B0 = 0;

//    //private static int TEST_R1 = 236;

//    //private static int TEST_G1 = 50;

//    //private static int TEST_B1 = 186;

//    //  size of a frame, in pixels
//    private int mWidth = -1;

//    private int mHeight = -1;

//    //  bit rate, in bits per second
//    private int mBitRate = -1;

//    // largest color component delta seen (i.e. actual vs. expected)
//    private int mLargestColorDelta;

//    //  encoder / muxer state
//    private MediaCodec mEncoder;

//    //private CodecInputSurface mInputSurface;

//    private MediaMuxer mMuxer;

//    private int mTrackIndex;

//    private bool mMuxerStarted;

//    private string _outputPath;

//    //  allocate one of these up front so we don't need to do it every time
//    private MediaCodec.BufferInfo mBufferInfo;

//    private void setParameters(int width, int height, int bitRate)
//    {
//        //if ((width % 16) != 0 || (height % 16) != 0)
//        //{
//        //    Log.w(TAG, "WARNING: width or height not multiple of 16");
//        //}
//        mWidth = width;
//        mHeight = height;
//        mBitRate = bitRate;
//    }

//    public void testEncodeDecodeVideoFromBufferToBuffer720p()
//    {
//        setParameters(1280, 720, 6000000);
//        encodeDecodeVideoFromBuffer(false);
//    }

//    /**
//    * Tests encoding and subsequently decoding video from frames generated into a buffer.
//    * <p>
//    * We encode several frames of a video test pattern using MediaCodec, then decode the
//    * output with MediaCodec and do some simple checks.
//    * <p>
//    * See http://b.android.com/37769 for a discussion of input format pitfalls.
//    */
//    private void encodeDecodeVideoFromBuffer(bool toSurface)
//    {
//        MediaCodec encoder = null;
//        MediaCodec decoder = null;
//        mLargestColorDelta = -1;
//        try
//        {
//            MediaCodecInfo codecInfo = selectCodec(MIME_TYPE);
//            if (codecInfo == null)
//            {
//                // Don't fail CTS if they don't have an AVC codec (not here, anyway).
//                //Log.e(TAG, "Unable to find an appropriate codec for " + MIME_TYPE);
//                return;
//            }

//            int colorFormat = selectColorFormat(codecInfo, MIME_TYPE);

//            // We avoid the device-specific limitations on width and height by using values that
//            // are multiples of 16, which all tested devices seem to be able to handle.
//            MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, mWidth, mHeight);
//            // Set some properties.  Failing to specify some of these can cause the MediaCodec
//            // configure() call to throw an unhelpful exception.
//            format.SetInteger(MediaFormat.KeyColorFormat, colorFormat);
//            format.SetInteger(MediaFormat.KeyBitRate, mBitRate);
//            format.SetInteger(MediaFormat.KeyFrameRate, FRAME_RATE);
//            format.SetInteger(MediaFormat.KeyIFrameInterval, IFRAME_INTERVAL);


//            // Create a MediaCodec for the desired codec, then configure it as an encoder with
//            // our desired properties.
//            encoder = MediaCodec.CreateByCodecName(codecInfo.Name);
//            encoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);
//            encoder.Start();
//            // Create a MediaCodec for the decoder, just based on the MIME type.  The various
//            // format details will be passed through the csd-0 meta-data later on.
//            String outputPath = new File(OUTPUT_DIR, "test." + mWidth + "x" + mHeight + ".mp4").ToString();

//            doEncodeDecodeVideoFromBuffer(encoder, colorFormat,  toSurface);

//        }
//        finally
//        {

//            if (encoder != null)
//            {
//                encoder.Stop();
//                encoder.Release();
//            }


//        }
//    }

//    private static long computePresentationTimeNsec(int frameIndex)
//    {
//        const long ONE_BILLION = 1000000000;
//        return frameIndex * ONE_BILLION / FRAME_RATE;
//    }

//    private static long computePresentationTime(int frameIndex)
//    {
//        return 132 + frameIndex * 1000000 / FRAME_RATE;
//    }

//    private static int selectColorFormat(MediaCodecInfo codecInfo, String mimeType)
//    {
//        MediaCodecInfo.CodecCapabilities capabilities = codecInfo.GetCapabilitiesForType(mimeType);
//        for (int i = 0; (i < capabilities.ColorFormats.Count); i++)
//        {
//            int colorFormat = capabilities.ColorFormats[i];
//            if (isRecognizedFormat(colorFormat))
//            {
//                return colorFormat;
//            }

//        }

//        return 0;
//        //  not reached  
//    }

//    private static bool isRecognizedFormat(int colorFormat)
//    {
//        switch (colorFormat)
//        {
//            case (int)MediaCodecCapabilities.Formatyuv420planar:
//            case (int)MediaCodecCapabilities.Formatyuv420packedplanar:
//            case (int)MediaCodecCapabilities.Formatyuv420semiplanar:
//            case (int)MediaCodecCapabilities.Formatyuv420packedsemiplanar:
//            case (int)MediaCodecCapabilities.TiFormatyuv420packedsemiplanar:
//                return true;
//            default:
//                return false;
//        }
//    }

//    private static MediaCodecInfo selectCodec(String mimeType)
//    {
//        int numCodecs = MediaCodecList.CodecCount;
//        for (int i = 0; i < numCodecs; i++)
//        {
//            MediaCodecInfo codecInfo = MediaCodecList.GetCodecInfoAt(i);
//            if (!codecInfo.IsEncoder)
//            {
//                continue;
//            }
//            String[] types = codecInfo.GetSupportedTypes();
//            for (int j = 0; j < types.Length; j++)
//            {
//                if (types[j].ToLower().Equals(mimeType.ToLower()))
//                {
//                    return codecInfo;
//                }
//            }
//        }
//        return null;
//    }

//    private void doEncodeDecodeVideoFromBuffer(MediaCodec encoder, int encoderColorFormat, bool toSurface)
//    {
//        const int TIMEOUT_USEC = 10000;
//        ByteBuffer[] encoderInputBuffers = encoder.GetInputBuffers();
//        ByteBuffer[] encoderOutputBuffers = encoder.GetOutputBuffers();
//        ByteBuffer[] decoderInputBuffers = null;
//        ByteBuffer[] decoderOutputBuffers = null;
//        MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
//        MediaFormat decoderOutputFormat = null;
//        int generateIndex = 0;
//        int checkIndex = 0;
//        int badFrames = 0;
//        bool decoderConfigured = false;
//        //OutputSurface outputSurface = null;
//        // The size of a frame of video data, in the formats we handle, is stride*sliceHeight
//        // for Y, and (stride/2)*(sliceHeight/2) for each of the Cb and Cr channels.  Application
//        // of algebra and assuming that stride==width and sliceHeight==height yields:
//        byte[] frameData = new byte[mWidth * mHeight * 3 / 2];
//        // Just out of curiosity.
//        long rawSize = 0;
//        long encodedSize = 0;

//        // Save a copy to disk.  Useful for debugging the test.  Note this is a raw elementary
//        // stream, not a .mp4 file, so not all players will know what to do with it.
//        FileOutputStream outputStream = null;
//        String fileName = "test" + mWidth + "x" + mHeight + ".mp4";
//        try
//        {
//            outputStream = new FileOutputStream(fileName);
//            //Log.d(TAG, "encoded output will be saved as " + fileName);
//        }
//        catch (IOException ioe)
//        {
//            //Log.w(TAG, "Unable to create debug output file " + fileName);
//            throw new RuntimeException(ioe);
//        }

//        //if (toSurface)
//        //{
//        //    outputSurface = new OutputSurface(mWidth, mHeight);
//        //}
//        // Loop until the output side is done.
//        bool inputDone = false;
//        var encoderDone = false;
//        var outputDone = false;

//        while (!outputDone)
//        {
//            // If we're not done submitting frames, generate a new one and submit it.  By
//            // doing this on every loop we're working to ensure that the encoder always has
//            // work to do.
//            //
//            // We don't really want a timeout here, but sometimes there's a delay opening
//            // the encoder device, so a short timeout can keep us from spinning hard.
//            if (!inputDone)
//            {
//                int inputBufIndex = encoder.DequeueInputBuffer(TIMEOUT_USEC);
//                if (inputBufIndex >= 0)
//                {
//                    long ptsUsec = computePresentationTime(generateIndex);
//                    if (generateIndex == NUM_FRAMES)
//                    {
//                        // Send an empty frame with the end-of-stream flag set.  If we set EOS
//                        // on a frame with data, that frame data will be ignored, and the
//                        // output will be short one frame.
//                        encoder.QueueInputBuffer(inputBufIndex, 0, 0, ptsUsec, MediaCodec.BufferFlagEndOfStream);
//                        inputDone = true;
//                        //if (VERBOSE) Log.d(TAG, "sent input EOS (with zero-length frame)");
//                    }
//                    else
//                    {
//                        generateFrame(generateIndex, encoderColorFormat, frameData);
//                        ByteBuffer inputBuf = encoderInputBuffers[inputBufIndex];
//                        // the buffer should be sized to hold one full frame
//                        //assertTrue(inputBuf.capacity() >= frameData.length);
//                        inputBuf.Clear();
//                        inputBuf.Put(frameData);
//                        encoder.QueueInputBuffer(inputBufIndex, 0, frameData.Length, ptsUsec, 0);
//                        //if (VERBOSE) Log.d(TAG, "submitted frame " + generateIndex + " to enc");
//                    }
//                    generateIndex++;
//                }
//                else
//                {
//                    // either all in use, or we timed out during initial setup
//                    //if (VERBOSE) Log.d(TAG, "input buffer not available");
//                }
//            }
//            // Check for output from the encoder.  If there's no output yet, we either need to
//            // provide more input, or we need to wait for the encoder to work its magic.  We
//            // can't actually tell which is the case, so if we can't get an output buffer right
//            // away we loop around and see if it wants more input.
//            //
//            // Once we get EOS from the encoder, we don't need to do this anymore.
//            if (!encoderDone)
//            {
//                int encoderStatus = encoder.DequeueOutputBuffer(info, TIMEOUT_USEC);
//                if (encoderStatus == (int)MediaCodec.InfoTryAgainLater)
//                {
//                    // no output available yet
//                    //if (VERBOSE) Log.d(TAG, "no output from encoder available");
//                }
//                else if (encoderStatus == (int)MediaCodec.InfoOutputBuffersChanged)
//                {
//                    // not expected for an encoder
//                    encoderOutputBuffers = encoder.GetOutputBuffers();
//                    //if (VERBOSE) Log.d(TAG, "encoder output buffers changed");
//                }
//                else if (encoderStatus == (int)MediaCodec.InfoOutputFormatChanged)
//                {
//                    // not expected for an encoder
//                    MediaFormat newFormat = encoder.OutputFormat;
//                    // if (VERBOSE) Log.d(TAG, "encoder output format changed: " + newFormat);
//                }
//                else if (encoderStatus < 0)
//                {
//                    //fail("unexpected result from encoder.dequeueOutputBuffer: " + encoderStatus);
//                }
//                else
//                { // encoderStatus >= 0
//                    ByteBuffer encodedData = encoderOutputBuffers[encoderStatus];
//                    if (encodedData == null)
//                    {
//                        //fail("encoderOutputBuffer " + encoderStatus + " was null");
//                    }
//                    // It's usually necessary to adjust the ByteBuffer values to match BufferInfo.
//                    encodedData.Position(info.Offset);
//                    encodedData.Limit(info.Offset + info.Size);
//                    encodedSize += info.Size;
//                    if (outputStream != null)
//                    {
//                        byte[] data = new byte[info.Size];
//                        encodedData.Get(data);
//                        encodedData.Position(info.Offset);
//                        try
//                        {
//                            outputStream.Write(data);
//                        }
//                        catch (IOException ioe)
//                        {
//                            //Log.w(TAG, "failed writing debug data to file");
//                            throw new RuntimeException(ioe);
//                        }
//                    }
//                    if ((info.Flags & MediaCodec.BufferFlagCodecConfig) != 0)
//                    {
//                        // Codec config info.  Only expected on first packet.  One way to
//                        // handle this is to manually stuff the data into the MediaFormat
//                        // and pass that to configure().  We do that here to exercise the API.
//                        //assertFalse(decoderConfigured);
//                        MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, mWidth, mHeight);
//                        format.SetByteBuffer("csd-0", encodedData);
//                        decoder.configure(format, toSurface ? outputSurface.getSurface() : null, null, 0);
//                        decoder.start();
//                        decoderInputBuffers = decoder.getInputBuffers();
//                        decoderOutputBuffers = decoder.getOutputBuffers();
//                        decoderConfigured = true;
//                        if (VERBOSE) Log.d(TAG, "decoder configured (" + info.size + " bytes)");
//                    }
//                    else
//                    {
//                        // Get a decoder input buffer, blocking until it's available.
//                        assertTrue(decoderConfigured);
//                        int inputBufIndex = decoder.dequeueInputBuffer(-1);
//                        ByteBuffer inputBuf = decoderInputBuffers[inputBufIndex];
//                        inputBuf.clear();
//                        inputBuf.put(encodedData);
//                        decoder.queueInputBuffer(inputBufIndex, 0, info.size,
//                            info.presentationTimeUs, info.flags);
//                        encoderDone = (info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0;
//                        if (VERBOSE) Log.d(TAG, "passed " + info.size + " bytes to decoder"
//                                                + (encoderDone ? " (EOS)" : ""));
//                    }
//                    encoder.ReleaseOutputBuffer(encoderStatus, false);
//                }
//            }
//            // Check for output from the decoder.  We want to do this on every loop to avoid
//            // the possibility of stalling the pipeline.  We use a short timeout to avoid
//            // burning CPU if the decoder is hard at work but the next frame isn't quite ready.
//            //
//            // If we're decoding to a Surface, we'll get notified here as usual but the
//            // ByteBuffer references will be null.  The data is sent to Surface instead.
//            //if (decoderConfigured)
//            //{
//            //int decoderStatus = decoder.dequeueOutputBuffer(info, TIMEOUT_USEC);
//            //if (decoderStatus == MediaCodec.INFO_TRY_AGAIN_LATER)
//            //{
//            //    // no output available yet
//            //    if (VERBOSE) Log.d(TAG, "no output from decoder available");
//            //}
//            //else if (decoderStatus == MediaCodec.INFO_OUTPUT_BUFFERS_CHANGED)
//            //{
//            //    // The storage associated with the direct ByteBuffer may already be unmapped,
//            //    // so attempting to access data through the old output buffer array could
//            //    // lead to a native crash.
//            //    if (VERBOSE) Log.d(TAG, "decoder output buffers changed");
//            //    decoderOutputBuffers = decoder.getOutputBuffers();
//            //}
//            //else if (decoderStatus == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED)
//            //{
//            //    // this happens before the first frame is returned
//            //    decoderOutputFormat = decoder.getOutputFormat();
//            //    if (VERBOSE) Log.d(TAG, "decoder output format changed: " +
//            //                            decoderOutputFormat);
//            //}
//            //else if (decoderStatus < 0)
//            //{
//            //    fail("unexpected result from deocder.dequeueOutputBuffer: " + decoderStatus);
//            //}
//            //else
//            //{  // decoderStatus >= 0
//            //    if (!toSurface)
//            //    {
//            //        ByteBuffer outputFrame = decoderOutputBuffers[decoderStatus];
//            //        outputFrame.position(info.offset);
//            //        outputFrame.limit(info.offset + info.size);
//            //        rawSize += info.size;
//            //        if (info.size == 0)
//            //        {
//            //            if (VERBOSE) Log.d(TAG, "got empty frame");
//            //        }
//            //        else
//            //        {
//            //            if (VERBOSE) Log.d(TAG, "decoded, checking frame " + checkIndex);
//            //            assertEquals("Wrong time stamp", computePresentationTime(checkIndex),
//            //                info.presentationTimeUs);
//            //            if (!checkFrame(checkIndex++, decoderOutputFormat, outputFrame))
//            //            {
//            //                badFrames++;
//            //            }
//            //        }
//            //        if ((info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0)
//            //        {
//            //            if (VERBOSE) Log.d(TAG, "output EOS");
//            //            outputDone = true;
//            //        }
//            //        decoder.releaseOutputBuffer(decoderStatus, false /*render*/);
//            //    }
//            //    else
//            //    {
//            //        if (VERBOSE) Log.d(TAG, "surface decoder given buffer " + decoderStatus +
//            //                                " (size=" + info.size + ")");
//            //        rawSize += info.size;
//            //        if ((info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0)
//            //        {
//            //            if (VERBOSE) Log.d(TAG, "output EOS");
//            //            outputDone = true;
//            //        }
//            //        boolean doRender = (info.size != 0);
//            //        // As soon as we call releaseOutputBuffer, the buffer will be forwarded
//            //        // to SurfaceTexture to convert to a texture.  The API doesn't guarantee
//            //        // that the texture will be available before the call returns, so we
//            //        // need to wait for the onFrameAvailable callback to fire.
//            //        decoder.releaseOutputBuffer(decoderStatus, doRender);
//            //        if (doRender)
//            //        {
//            //            if (VERBOSE) Log.d(TAG, "awaiting frame " + checkIndex);
//            //            assertEquals("Wrong time stamp", computePresentationTime(checkIndex),
//            //                info.presentationTimeUs);
//            //            outputSurface.awaitNewImage();
//            //            outputSurface.drawImage();
//            //            if (!checkSurfaceFrame(checkIndex++))
//            //            {
//            //                badFrames++;
//            //            }
//            //        }
//            //    }
//            //}
//            //}
//        }

//        if (outputStream != null)
//        {
//            try
//            {
//                outputStream.Close();
//            }
//            catch (IOException ioe)
//            {
//                //Log.w(TAG, "failed closing debug file");
//                throw new RuntimeException(ioe);
//            }
//        }

//    }


//    private void prepareEncoder()
//    {
//        this.mBufferInfo = new MediaCodec.BufferInfo();
//        MediaFormat format = MediaFormat.CreateVideoFormat(MIME_TYPE, mWidth, mHeight);
//        //  Set some properties.  Failing to specify some of these can cause the MediaCodec
//        //  configure() call to throw an unhelpful exception.

//        MediaCodecInfo codecInfo = selectCodec(MIME_TYPE);

//        int colorFormat;
//        try
//        {
//            colorFormat = selectColorFormat(codecInfo, MIME_TYPE);
//        }
//        catch
//        {
//            colorFormat = (int)MediaCodecCapabilities.Formatyuv420semiplanar;
//        }

//        format.SetInteger(MediaFormat.KeyColorFormat, colorFormat);
//        format.SetInteger(MediaFormat.KeyBitRate, mBitRate);
//        format.SetInteger(MediaFormat.KeyFrameRate, FRAME_RATE);
//        format.SetInteger(MediaFormat.KeyIFrameInterval, IFRAME_INTERVAL);

//        //format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface);

//        //if (VERBOSE)
//        //{
//        //Log.d(TAG, ("format: " + format));
//        //}

//        //  Create a MediaCodec encoder, and configure it with our format.  Get a Surface
//        //  we can use for input and wrap it with a class that handles the EGL work.
//        // 
//        //  If you want to have two EGL contexts -- one for display, one for recording --
//        //  you will likely want to defer instantiation of CodecInputSurface until after the
//        //  "display" EGL context is created, then modify the eglCreateContext call to
//        //  take eglGetCurrentContext() as the share_context argument.
//        this.mEncoder = MediaCodec.CreateEncoderByType(MIME_TYPE);
//        this.mEncoder.Configure(format, null, null, MediaCodecConfigFlags.Encode);
//        //this.mInputSurface = new CodecInputSurface(this.mEncoder.CreateInputSurface());
//        this.mEncoder.Start();
//        //  Output filename.  Ideally this would use Context.getFilesDir() rather than a
//        //  hard-coded output directory.
//        //String outputPath = new File(OUTPUT_DIR, ("test."+ (this.mWidth + ("x"+ (this.mHeight + ".mp4"))))).ToString();
//        //Log.d(TAG, ("output file is " + outputPath));
//        //  Create a MediaMuxer.  We can't add the video track and start() the muxer here,
//        //  because our MediaFormat doesn't have the Magic Goodies.  These can only be
//        //  obtained from the encoder after it has started processing data.
//        // 
//        //  We're not actually interested in multiplexing audio.  We just want to convert
//        //  the raw H.264 elementary stream we get from MediaCodec into a .mp4 file.
//        try
//        {
//            this.mMuxer = new MediaMuxer(_outputPath, MuxerOutputType.Mpeg4);
//        }
//        catch (IOException ioe)
//        {
//            throw new RuntimeException("MediaMuxer creation failed", ioe);
//        }

//        this.mTrackIndex = -1;
//        this.mMuxerStarted = false;
//    }

//    //private void releaseEncoder()
//    //{
//    //    //if (VERBOSE)
//    //    //{
//    //    //Log.d(TAG, "releasing encoder objects");
//    //    //}

//    //    if ((this.mEncoder != null))
//    //    {
//    //        this.mEncoder.Stop();
//    //        this.mEncoder.Release();
//    //        this.mEncoder = null;
//    //    }

//    //    //if ((this.mInputSurface != null))
//    //    //{
//    //    //    this.mInputSurface.release();
//    //    //    this.mInputSurface = null;
//    //    //}

//    //    if ((this.mMuxer != null))
//    //    {
//    //        this.mMuxer.Stop();
//    //        this.mMuxer.Release();
//    //        this.mMuxer = null;
//    //    }

//    //}

//    //private void drainEncoder(bool endOfStream)
//    //{
//    //    int TIMEOUT_USEC = 10000;
//    //    //if (VERBOSE)
//    //    //{
//    //    //Log.d(TAG, ("drainEncoder("//+ (endOfStream + ")")));
//    //    //}

//    //    if (endOfStream)
//    //    {
//    //        //if (VERBOSE)
//    //        //{
//    //        //Log.d(TAG, "sending EOS to encoder");
//    //        //}

//    //        this.mEncoder.SignalEndOfInputStream();
//    //    }

//    //    ByteBuffer[] encoderOutputBuffers = this.mEncoder.GetOutputBuffers();
//    //    while (true)
//    //    {
//    //        int encoderStatus = this.mEncoder.DequeueOutputBuffer(this.mBufferInfo, TIMEOUT_USEC);
//    //        if (encoderStatus == (int)MediaCodec.InfoTryAgainLater)
//    //        {
//    //    //  no output available yet
//    //            if (!endOfStream)
//    //            {
//    //                break;
//    //                //  out of while
//    //            }
//    //            //else if (VERBOSE)
//    //            //{
//    //            //Log.d(TAG, "no output available, spinning to await EOS");
//    //            //}

//    //        }
//    //        else if (encoderStatus == (int)MediaCodec.InfoOutputBuffersChanged)
//    //        {
//    //            //  not expected for an encoder
//    //            encoderOutputBuffers = this.mEncoder.GetOutputBuffers();
//    //        }
//    //        else if (encoderStatus == (int)MediaCodec.InfoOutputFormatChanged)
//    //        {
//    //        //  should happen before receiving buffers, and should only happen once
//    //            if (this.mMuxerStarted)
//    //            {
//    //                throw new RuntimeException("format changed twice");
//    //            }

//    //            MediaFormat newFormat = this.mEncoder.GetOutputFormat(0); //!!!!!changed to index!!!!!

//    //            //Log.d(TAG, ("encoder output format changed: " + newFormat));
//    //            //  now that we have the Magic Goodies, start the muxer
//    //            this.mTrackIndex = this.mMuxer.AddTrack(newFormat);
//    //            this.mMuxer.Start();
//    //            this.mMuxerStarted = true;
//    //        }
//    //        //else if ((encoderStatus < 0))
//    //        //{
//    //        //Log.w(TAG, ("unexpected result from encoder.dequeueOutputBuffer: " + encoderStatus));
//    //        ////  let's ignore it
//    //        //}
//    //        else
//    //        {
//    //            ByteBuffer encodedData = encoderOutputBuffers[encoderStatus];
//    //            if ((encodedData == null))
//    //            {
//    //                throw new RuntimeException(("encoderOutputBuffer "+ (encoderStatus + " was null")));
//    //            }

//    //            if ((this.mBufferInfo.Flags & MediaCodec.BufferFlagCodecConfig) != 0)
//    //            {
//    //            //  The codec config data was pulled out and fed to the muxer when we got
//    //            //  the INFO_OUTPUT_FORMAT_CHANGED status.  Ignore it.
//    //            //if (VERBOSE)
//    //            //{
//    //            //Log.d(TAG, "ignoring BUFFER_FLAG_CODEC_CONFIG");
//    //            //}

//    //                this.mBufferInfo.Size = 0;
//    //            }

//    //            if ((this.mBufferInfo.Size != 0))
//    //            {
//    //                if (!this.mMuxerStarted)
//    //                {
//    //                    throw new RuntimeException("muxer hasn\'t started");
//    //                }

//    //            //  adjust the ByteBuffer values to match BufferInfo (not needed?)
//    //                encodedData.Position(this.mBufferInfo.Offset);
//    //                encodedData.Limit((this.mBufferInfo.Offset + this.mBufferInfo.Size));
//    //                this.mMuxer.WriteSampleData(this.mTrackIndex, encodedData, this.mBufferInfo);
//    //                //if (VERBOSE)
//    //                //{
//    //                //Log.d(TAG, ("sent "
//    //                //+ (this.mBufferInfo.size + " bytes to muxer")));
//    //                //}

//    //            }

//    //            this.mEncoder.ReleaseOutputBuffer(encoderStatus, false);
//    //            if ((this.mBufferInfo.Flags & MediaCodec.BufferFlagEndOfStream) != 0)
//    //            {
//    //                //if (!endOfStream)
//    //                //{
//    //                //Log.w(TAG, "reached end of stream unexpectedly");
//    //                //}
//    //                //else if (VERBOSE)
//    //                //{
//    //                //Log.d(TAG, "end of stream reached");
//    //                //}

//    //                break;
//    //                //  out of while
//    //            }

//    //        }   

//    //    }

//    //}


//}