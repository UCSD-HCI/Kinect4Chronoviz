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

        public LoggerQueue() {
            startingTime = DateTime.Now;
            queue = new List<queueItem>();


            t = new Thread(new ThreadStart(startQueue));
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            while (!t.IsAlive);
        }


        public void addToQueue(string fileName_arg, string strToAdd_arg) {

            queueItem newItem = new queueItem();
            newItem.time = DateTime.Now;
            newItem.fileName = fileName_arg;
            newItem.toWrite = strToAdd_arg;
            queue.Add(newItem);
        }

        public void addNewFile(string fullFilename, string header)
        {
            if (!File.Exists(fullFilename))
            {
                lock (lockLogger)
                {
                    writer = File.CreateText(fullFilename);
                    writer.WriteLine(header);
                    writer.Close();
                }
            }
        }


        void startQueue() {

            while (true) {

                if (queue.Count > 0)
                {
                    queueItem nextItem = queue[0];
                    if ((nextItem.fileName != null) && (nextItem.time.Subtract(startingTime).TotalMilliseconds > 0))
                    {
                        lock (lockLogger)
                        {
                            writer = File.AppendText(nextItem.fileName);
                            writer.WriteLine(nextItem.time.Subtract(startingTime) + "," + nextItem.toWrite);                            
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



    }
}
