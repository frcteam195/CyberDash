using CsvHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CyberDash
{
    public class CKLogHandler
    {
        public Queue<List<string>> dataQueue = new Queue<List<string>>();
        private object lockObject = new object();
        private string filename;
        private bool runThread = true;
        private Thread logWriterThread;
        private CsvWriter csv;

        public bool HeadersWritten { get; protected set; }

        public CKLogHandler(string filename)
        {
            this.filename = filename;
            logWriterThread = new Thread(() =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                using (TextWriter writer = new StreamWriter(filename, false, Encoding.UTF8))
                {
                    var csv = new CsvWriter(writer);    //Install-Package CsvHelper
                    while (runThread)
                    {
                        lock (lockObject)
                        {
                            while (dataQueue.Count > 0)
                            {
                                try
                                {
                                    List<string> l = dataQueue.Dequeue();
                                    foreach (string s in l)
                                    {
                                        csv.WriteField(s);
                                    }
                                    csv.NextRecordAsync();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                }
                            }
                        }

                        if (stopwatch.ElapsedMilliseconds >= 5000)
                        {
                            csv.Flush();
                            writer.Flush();
                            stopwatch.Restart();
                        }
                    }
                    csv.Flush();
                }
            });
        }

        public void WriteCSVHeaders(List<string> headerData)
        {
            LogData(headerData);
            HeadersWritten = true;
        }

        public void LogData(List<string> logData)
        {
            lock (lockObject)
            {
                dataQueue.Enqueue(logData);
            }
        }

        public void StartLogging()
        {
            logWriterThread.Start();
        }

        public void StopLogging()
        {
            runThread = false;
            if (logWriterThread != null)
                logWriterThread.Join(2000);
        }
    }
}
