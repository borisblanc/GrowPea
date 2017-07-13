
/*
 * Copyright (C) 2012 The Android Open Source Project
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
// Modified example based on mp4parser google code open source project.
// http://code.google.com/p/mp4parser/source/browse/trunk/examples/src/main/java/com/googlecode/mp4parser/ShortenExample.java

using System.Collections.Generic;
using Android.Graphics;
using Android.Media;

using Android.Util;



//import com.coremedia.iso.IsoFile;
//import com.coremedia.iso.boxes.TimeToSampleBox;
//import com.googlecode.mp4parser.authoring.Movie;
//import com.googlecode.mp4parser.authoring.Track;
//import com.googlecode.mp4parser.authoring.builder.DefaultMp4Builder;
//import com.googlecode.mp4parser.authoring.container.mp4.MovieCreator;
//import com.googlecode.mp4parser.authoring.tracks.CroppedTrack;


using Java.IO;
using Java.Lang;
using Java.Nio;

using Java.Util;
using String = System.String;


namespace GrowPea
{

    public class SaveVideoFileInfo
    {
        public File mFile = null;
        public String mFileName = null;
        // This the full directory path.
        public File mDirectory = null;
        // This is just the folder's name.
        public String mFolderName = null;
    }
    public class VideoUtils
    {
        private static  String LOGTAG = "VideoUtils";
        private static int DEFAULT_BUFFER_SIZE = 1 * 1024 * 1024;
        /**
         * Remove the sound track.
         */
        public static void startMute(String filePath, SaveVideoFileInfo dstFileInfo)
        {
            genVideoUsingMuxer(filePath, dstFileInfo.mFile.Path, -1, -1, false, true);
        }
        /**
         * Shortens/Crops tracks
         */
        public static bool startTrim(File src, File dst, long startMicroSeconds, long endMicroSeconds)
        {
            return genVideoUsingMuxer(src.Path, dst.Path, startMicroSeconds, endMicroSeconds, true, true);
        }

        private static bool genVideoUsingMuxer(String srcPath, String dstPath, long startMicroSeconds, long endMicroSeconds, bool useAudio, bool useVideo)
        {
        // Set up MediaExtractor to read from the source.
            MediaExtractor extractor = new MediaExtractor();
            extractor.SetDataSource(srcPath);
            int trackCount = extractor.TrackCount;
            // Set up MediaMuxer for the destination.
            var muxer = new MediaMuxer(dstPath, MediaMuxer.OutputFormat.MuxerOutputMpeg4);
            // Set up the tracks and retrieve the max buffer size for selected
            // tracks.
            Dictionary<int, int> indexMap = new Dictionary<int,int>(trackCount);
            int bufferSize = -1;
            for (int i = 0; i<trackCount; i++)
            {
                MediaFormat format = extractor.GetTrackFormat(i);
                String mime = format.GetString(MediaFormat.KeyMime);
                bool selectCurrentTrack = false;

                if (mime.StartsWith("audio/") && useAudio)
                {
                    selectCurrentTrack = true;
                }
                else if (mime.StartsWith("video/") && useVideo)
                {
                    selectCurrentTrack = true;
                }

                if (selectCurrentTrack)
                {
                    extractor.SelectTrack(i);
                    int dstIndex = muxer.AddTrack(format);
                    indexMap.Add(i, dstIndex);

                    if (format.ContainsKey(MediaFormat.KeyMaxInputSize))
                    {
                        int newSize = format.GetInteger(MediaFormat.KeyMaxInputSize);
                        bufferSize = newSize > bufferSize? newSize : bufferSize;
                    }
                }
            }

            if (bufferSize< 0) {
                bufferSize = DEFAULT_BUFFER_SIZE;
            }
            // Set up the orientation and starting time for extractor.
            MediaMetadataRetriever retrieverSrc = new MediaMetadataRetriever();
            retrieverSrc.SetDataSource(srcPath);
            String degreesString = retrieverSrc.ExtractMetadata(MediaMetadataRetriever.MetadataKeyVideoRotation);

            if (degreesString != null)
            {
                int degrees = Integer.ParseInt(degreesString);
                if (degrees >= 0)
                {
                    muxer.SetOrientationHint(degrees);
                }
            }

            if (startMicroSeconds > 0)
            {
                extractor.SeekTo(startMicroSeconds, MediaExtractor.SeekToClosestSync);
            }
            // Copy the samples from MediaExtractor to MediaMuxer. We will loop
            // for copying each sample and stop when we get to the end of the source
            // file or exceed the end time of the trimming.
            int offset = 0;
            int trackIndex = -1;
            ByteBuffer dstBuf = ByteBuffer.Allocate(bufferSize);
            MediaCodec.BufferInfo bufferInfo = new MediaCodec.BufferInfo();

            try
            {
                muxer.Start();
                while (true)
                {
                    bufferInfo.Offset = offset;
                    bufferInfo.Size = extractor.ReadSampleData(dstBuf, offset);
                    if (bufferInfo.Size < 0)
                    {
                        Log.Info(LOGTAG, "Saw input EOS.");
                        bufferInfo.Size = 0;
                        break;
                    }
                    else
                    {
                        bufferInfo.PresentationTimeUs = extractor.SampleTime;
                        if (endMicroSeconds > 0 && bufferInfo.PresentationTimeUs > endMicroSeconds)
                        {
                            Log.Info(LOGTAG, "The current sample is over the trim end time.");
                            break;
                        }
                        else
                        {

                            bufferInfo.Flags = GetSyncsampleflags(extractor.SampleFlags); //had to map this shit not sure if its right
                            trackIndex = extractor.SampleTrackIndex;
                            muxer.WriteSampleData(indexMap[trackIndex], dstBuf,bufferInfo);
                            extractor.Advance();
                        }
                    }
                }
                muxer.Stop();

            }
            catch (IllegalStateException e)
            {
                // Swallow the exception due to malformed source.
                Log.Info(LOGTAG, "The source video file is malformed");
                return false;
            }
            finally
            {
                muxer.Release();
            }
            return true;
        }

        private static MediaCodecBufferFlags GetSyncsampleflags(MediaExtractorSampleFlags mxflag)
        {
            switch (mxflag)
            {
                case MediaExtractorSampleFlags.None:
                    return MediaCodecBufferFlags.None;
                case MediaExtractorSampleFlags.Encrypted:
                    return MediaCodecBufferFlags.None;

                case MediaExtractorSampleFlags.Sync:
                    return MediaCodecBufferFlags.SyncFrame;
                default:
                    return MediaCodecBufferFlags.None;

            }
        }
    }
}