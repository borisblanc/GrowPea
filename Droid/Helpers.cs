using Android.Util;
using Java.Nio;
using Android.Graphics;
using static Android.Media.MediaCodec;

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


}