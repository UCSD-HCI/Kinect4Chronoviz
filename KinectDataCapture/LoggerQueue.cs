using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace KinectDataCapture
{
    class LoggerQueue
    {
        StreamWriter writer;
        List<queueItem> queue;
        DateTime startingTime;
        string dir;
        Thread t;
        private object lockLogger = new object();

        struct queueItem {
            public DateTime time;
            public string fileName;
            public string toWrite;
        }

        struct logFileItem {
            public string logFileName;
            public LogFile logFile;
        
        }

        public LoggerQueue(string dir_arg) {
            startingTime = DateTime.Now;
            queue = new List<queueItem>();

            dir = dir_arg;
            Directory.CreateDirectory(dir);

            t = new Thread(new ThreadStart(startQueue));
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            while (!t.IsAlive);
        }


        public void addToQueue(string fileName_arg, string strToAdd_arg) {

            queueItem newItem = new queueItem();
            newItem.time = DateTime.Now;
            newItem.fileName = dir + "\\" + fileName_arg;
            newItem.toWrite = strToAdd_arg;

            if (!File.Exists(newItem.fileName))
            {
                lock (lockLogger)
                {
                    writer = File.CreateText(newItem.fileName);
                    writer.WriteLine(getColumnHeader(newItem.fileName));
                    writer.Close();
                }
            }

            queue.Add(newItem);
        }

        void startQueue() {

            while (true) {

                if (queue.Count > 0)
                {
                    queueItem nextItem = queue[0];
                    if (nextItem.fileName != null)
                    {
                        lock (lockLogger)
                        {
                            writer = File.AppendText(nextItem.fileName);
                            writer.WriteLine(nextItem.time.Subtract(startingTime) + "," + nextItem.toWrite);
                            //writer.Flush();
                            writer.Close();
                        }
                    }
                    queue.Remove(nextItem);
                }
            }
        }

        public void endLogging() {

                while(queue.Count > 0)
                {
                    queueItem nextItem = queue[0];
                    lock (lockLogger)
                    {
                        writer = File.AppendText((string)nextItem.fileName);
                        writer.WriteLine(nextItem.time.Subtract(startingTime) + "," + nextItem.toWrite);
                        //writer.Flush();
                        writer.Close();
                    }
                    queue.Remove(nextItem);
                }
                t.Abort();
        }

        string getColumnHeader(string fileName) {

            string columnHeader = "";
            if (fileName.Contains(MainWindow.appStatusFileName)) {
                columnHeader = "Time,StatusMessage";            
            }
            else if(fileName.Contains(MainWindow.audioAngleFileName)){
                columnHeader = "Time,AudioAngle,AudioConfidence";                        
            }
            else if(fileName.Contains(MainWindow.audioStateFileName)){
                columnHeader = "Time,SpeechState,AudioAngle";            
            }
            else if (fileName.Contains(MainWindow.audioRecordingFilename))
            {
                columnHeader = "Time,Filename";
            }
            else if(fileName.Contains(MainWindow.speechRecognitionFileName)){
                columnHeader = "Time,Speech Recognized";            
            }
            else if(fileName.Contains(MainWindow.depthTrackingFileName)){
                columnHeader = "Time,MinDepth,MaxDepth";
            }
            else if(fileName.Contains(MainWindow.bodyTrackingFileName)){
                columnHeader = "Time,HipCenterX,HipCenterY,HipCenterZ,SpineX,SpineY,SpineZ,ShoulderCenterX,ShoulderCenterY,ShoulderCenterZ,HeadX,HeadY,HeadZ,ShoulderLeftX,ShoulderLeftY,ShoulderLeftZ,ElbowLeftX,ElbowLeftY,ElbowLeftZ,WristLeftX,WristLeftY,WristLeftZ,HandLeftX,HandLeftY,HandLeftZ,ShoulderRightX,ShoulderRightY,ShoulderRightZ,ElbowRightX,ElbowRightY,ElbowRightZ,WristRightX,WristRightY,WristRightZ,HandRightX,HandRightY,HandRightZ,HipLeftX,HipLeftY,HipLeftZ,KneeLeftX,KneeLeftY,KneeLeftZ,AnkleLeftX,AnkleLeftY,AnkleLeftZ,FootLeftX,FootLeftY,FootLeftZ,HipRightX,HipRightY,HipRightZ,KneeRightX,KneeRightY,KneeRightZ,AnkleRightX,AnkleRightY,AnkleRightZ,FootRightX,FootRightY,FootRightZ,";
            }

            else if (fileName.Contains(MainWindow.twoBodyProximityFileName)) {
                columnHeader = "Time,HeadDistance,SpineDistance";
            }
            else if (fileName.Contains(MainWindow.jointVelocityFileName)) {
                columnHeader = "Time,HipCenter,Spine,ShoulderCenter,Head,ShoulderLeft,ElbowLeft,WristLeft,HandLeft,ShoulderRight,ElbowRight,WristRight,HandRight,HipLeft,KneeLeft,AnkleLeft,FootLeft,HipRight,KneeRight,AnkleRight,FootRight,";
            }
            else if (fileName.Contains(MainWindow.faceTrackingDepthFileName)) {
                columnHeader = "Time,CentroidX,CentroidY,Depth,Player";
            }
            else if (fileName.Contains(MainWindow.colorImagesFileName))
            {
                columnHeader = "Time,FileName";
            }
            return columnHeader;

        }

    }
}
