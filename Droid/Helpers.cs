using System.IO;
using Android.Util;
using Java.Nio;
using Android.Graphics;
using static Android.Media.MediaCodec;
using Android.Content;
using Android.Runtime;
using Android.Views;

namespace GrowPea.Droid
{
    public class BmFace
    {
        public float Iuse;

        public float TS;

        public ByteBuffer bytebuff;

        public BmFace(float timestamp, ByteBuffer bbyte, float ImageUsability)
        {
            TS = timestamp;
            bytebuff = bbyte;
            Iuse = ImageUsability;
        }
    }

    public class FrameData
    {
        public byte[] _yuv;

        public SparseArray _sparsearray;

        public float _timestamp;

        public FrameData(float timestamp, byte[] yuv, SparseArray sparsearray)
        {
            _timestamp = timestamp;
            _yuv = yuv;
            _sparsearray = sparsearray;
        }
    }

    public class EncodedforMux
    {
        public byte[] data;

        public BufferInfo bufferinfo;

        public int trackindex;

        public EncodedforMux(int Trackindex, byte[] Data, BufferInfo Buffinfo)
        {
            trackindex = Trackindex;
            data = Data;
            bufferinfo = Buffinfo;
        }
    }

    public static class Helpers
    {

        /// <summary>
        ///   Will obtain an instance of a LayoutInflater for the specified Context.
        /// </summary>
        /// <param name="context"> </param>
        /// <returns> </returns>
        public static LayoutInflater GetLayoutInflater(this Context context)
        {
            return context.GetSystemService(Context.LayoutInflaterService).JavaCast<LayoutInflater>();
        }

        /// <summary>
        ///   This method will tell us if the given FileSystemInfo instance is a directory.
        /// </summary>
        /// <param name="fsi"> </param>
        /// <returns> </returns>
        public static bool IsDirectory(this FileSystemInfo fsi)
        {
            if (fsi == null || !fsi.Exists)
            {
                return false;
            }

            return (fsi.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
        }

        /// <summary>
        ///   This method will tell us if the the given FileSystemInfo instance is a file.
        /// </summary>
        /// <param name="fsi"> </param>
        /// <returns> </returns>
        public static bool IsFile(this FileSystemInfo fsi)
        {
            if (fsi == null || !fsi.Exists)
            {
                return false;
            }
            return !IsDirectory(fsi);
        }

        public static bool IsVisible(this FileSystemInfo fsi)
        {
            if (fsi == null || !fsi.Exists)
            {
                return false;
            }

            var isHidden = (fsi.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
            return !isHidden;
        }
    }

}