using Android.Util;
using Java.Nio;
using Android.Graphics;


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
        public ByteBuffer _yuv;

        public SparseArray _sparsearray;

        public float _timestamp;

        public FrameData(float timestamp, ByteBuffer yuv, SparseArray sparsearray)
        {
            _timestamp = timestamp;
            _yuv = yuv;
            _sparsearray = sparsearray;
        }
    }


}