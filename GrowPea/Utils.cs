

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Java.Nio;


namespace GrowPea
{
    public static class Utils
    {
        

        public static ByteBuffer deepCopy(ByteBuffer orig)
        {
            int pos = orig.Position(), lim = orig.Limit();
            try
            {
                orig.Position(0).Limit(orig.Capacity()); // set range to entire buffer
                ByteBuffer toReturn = deepCopyVisible(orig); // deep copy range
                toReturn.Position(pos).Limit(lim); // set range to original
                return toReturn;
            }
            finally // do in finally in case something goes wrong we don't bork the orig
            {
                orig.Position(pos).Limit(lim); // restore original
            }
        }

        public static ByteBuffer deepCopyVisible(ByteBuffer orig)
        {
            int pos = orig.Position();
            try
            {
                ByteBuffer toReturn;
                // try to maintain implementation to keep performance
                if (orig.IsDirect)
                    toReturn = ByteBuffer.AllocateDirect(orig.Remaining());
                else
                    toReturn = ByteBuffer.Allocate(orig.Remaining());

                toReturn.Put(orig);
                toReturn.Order(orig.Order());

                return (ByteBuffer)toReturn.Position(0);
            }
            finally
            {
                orig.Position(pos);
            }
        }


    }


}
