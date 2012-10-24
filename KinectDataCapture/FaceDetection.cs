using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Markup;


using System.Threading;
using System.Runtime.InteropServices;


namespace KinectDataCapture
{
    class FaceDetection
    {

        MainWindow mainWindow;
        HaarCascade haarFaces;
        HaarCascade haarEyes;


        public FaceDetection(MainWindow mainWindow_arg) {

            mainWindow = mainWindow_arg;
            string curDir = Directory.GetCurrentDirectory();
            try
            {
                //haar = new HaarCascade("C:\\Emgu\\emgucv-windows-x86 2.3.0.1416\\opencv\\data\\haarcascades\\haarcascade_frontalface_alt.xml");
                //haar = new HaarCascade(@"C:\Emgu\emgucv-windows-x86 2.3.0.1416\opencv\data\haarcascades\haarcascade_frontalface_alt.xml");
                //haar = new HaarCascade(curDir + "\\haarcascade_frontalface_alt2.xml");
                haarFaces = new HaarCascade("haarcascade_frontalface_alt_tree.xml");
                haarEyes = new HaarCascade("haarcascade_eye.xml");
                //haar = new HaarCascade("haarcascade_frontalface_alt2.xml");

            }
            catch(Exception e){
                Console.WriteLine(e.StackTrace);
            }
        }

        public ArrayList detectFaces(PlanarImage image) { 
        
            BitmapSource bmapSrc = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null, image.Bits, image.Width * image.BytesPerPixel);
            Bitmap bmap = BitmapFromSource(bmapSrc);

            //BitmapSource bmapSrcDepth = mainWindow.curDepthImage;
            //Bitmap bmapDepth = BitmapFromSource(bmapSrcDepth);


            Image<Bgr, Byte> currentFrame = new Image<Bgr, Byte>(bmap);

            //Image<Bgr, Byte> currentDepthFrame = new Image<Bgr, Byte>(bmapDepth);


            Depth planarImage = mainWindow.curDepthPlanarImage; 
            ArrayList list = new ArrayList();

            if (currentFrame != null)
            {
                // there's only one channel (greyscale), hence the zero index
                //var faces = nextFrame.DetectHaarCascade(haar)[0];


                Image<Gray, byte> grayframe = currentFrame.Convert<Gray, byte>();


                var faces = currentFrame.DetectHaarCascade(
                                haarFaces,
                                1.4,
                                4,
                                Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                                new System.Drawing.Size(currentFrame.Width / 8, currentFrame.Height / 8)
                                )[0];

                //var faces =
                //currentFrame.DetectHaarCascade(
                //haar,
                //1.4,
                //4,
                //Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                //new System.Drawing.Size(currentFrame.Width / 8, currentFrame.Height / 8)
                //)[0];

                PlanarImage depthFrame = mainWindow.curDepthPlanarImage;

                byte[] depthFrame16 = depthFrame.Bits;

                foreach (var face in faces)
                {
                    try
                    {
                        //mainWindow.updateAppStatus("FaceDetection:  colorFrame=" + currentFrame.Width + "," + currentFrame.Height + " planarImage=" + planarImage.Width + "," + planarImage.Height);

                        int centroidXcolor = face.rect.X + (face.rect.Width / 2);
                        int centroidYcolor = face.rect.Y + (face.rect.Height / 2);

                        int centroidXdepth = (centroidXcolor * 320) / 640;
                        int centroidYdepth = (centroidYcolor * 240) / 480;

                        byte[] depth = planarImage.Bits;
                        int width = planarImage.Width;
                        int height = planarImage.Height;
                        byte[] color = new byte[width * height * 4];

                        int index = (centroidYdepth * width + centroidXdepth) * 2;
                        int player = depth[index] & 0x07;
                        int depthValue = (depth[index + 1] << 5) | (depth[index] >> 3);

                        //mainWindow.updateAppStatus("FaceDetection:  index=" + index + ", player=" + player + ", depthValue=" + depthValue);
                        //mainWindow.updateAppStatus("FaceDetection:  centroidXcolor=" + centroidXcolor + ", centroidYcolor=" + centroidYcolor + ", centroidXdepth=" + centroidXdepth + ", centroidYdepth=" + centroidYdepth);

                        FaceFrame faceFrame = new FaceFrame(face.rect, depthValue, player);
                        list.Add(faceFrame);
                    }
                    catch (Exception e) {
                        mainWindow.updateAppStatus("FaceDetection:  caught exception");
                    }

                }

            }
            return list;
        }


        private int GetDistanceWithPlayerIndex(byte firstFrame, byte secondFrame)
        {
            //offset by 3 in first byte to get value after player index 
            int distance = (int)(firstFrame >> 3 | secondFrame << 5);
            return distance;

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
