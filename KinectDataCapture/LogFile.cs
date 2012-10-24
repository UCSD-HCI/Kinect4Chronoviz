using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace KinectDataCapture
{
    class LogFile
    {
        private StreamWriter log;
        private string fileName;
        private static DateTime startTime;

        public LogFile(string fileName_arg)
        {
            fileName = fileName_arg;
            if (!File.Exists(fileName))
            {
                log = new StreamWriter(fileName);
            }
            startTime = DateTime.Now;
        }

        public void append(string textToAdd)
        {
            if (File.Exists(fileName))
            {
                log = File.AppendText(fileName);
                log.WriteLine(DateTime.Now - startTime + "," + textToAdd);
                log.Flush();
                log.Close();
            }
        }

    }
}
