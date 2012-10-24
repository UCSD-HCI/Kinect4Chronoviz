using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Structure;
using Emgu.Util;
using Emgu.CV.CvEnum;
using Microsoft.Kinect;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using System.Threading;

namespace KinectDataCapture
{

        class VideoLogger
        {
            private string filename;
            private VideoWriter writer;
            private bool isVideoRecording;
            private object lockObj = new object();
            Image<Bgr, Byte> currentFrame;
            

            public VideoLogger(string videofile)
            {
                this.filename = videofile;
                bool isColor = true;
                int fps = 25;  // or 30
                int frameW = 640; // 744 for firewire cameras
                int frameH = 480; // 480 for firewire cameras
                writer = new VideoWriter(filename,
                                     CvInvoke.CV_FOURCC('X','V','I','D'),
                                     fps, frameW, frameH, isColor);

            }

            public void AddFrame(BitmapSource source)
            {
                Bitmap bmap = BitmapFromSource(source);
                currentFrame = new Image<Bgr, Byte>(bmap);
                writer.WriteFrame<Bgr, Byte>(currentFrame);
                
            }
            private void WriteToVideo()
            {
                lock (lockObj)
                {
                    isVideoRecording = true;

                    isVideoRecording = false;
                }
            }
            public void StopRecording()
            {

                lock (lockObj)
                {
                    writer.Dispose();
                }
            }

            private Bitmap BitmapFromSource(BitmapSource bitmapsource)
            {
                Bitmap bitmap;
                using (MemoryStream outStream = new MemoryStream())
                {
                    BitmapEncoder enc = new BmpBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                    enc.Save(outStream);
                    bitmap = new System.Drawing.Bitmap(outStream);
                }
                return bitmap;
            }
        }

}
