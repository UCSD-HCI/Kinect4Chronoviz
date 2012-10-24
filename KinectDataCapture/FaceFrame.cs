using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace KinectDataCapture
{
    class FaceFrame
    {

        public Rectangle rect;
        public int depth;
        public int player;

        public FaceFrame(Rectangle rect_arg, int depth_arg, int player_arg)
        {
            rect = rect_arg;
            depth = depth_arg;
            player = player_arg;
        }
    
    }
}
