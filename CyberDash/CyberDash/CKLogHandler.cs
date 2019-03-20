using CsvHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
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

        private static readonly string SMTP_USER = "robotdiagnostics@gmail.com";
        private static readonly string SMTP_PASS = "Team195!";
        private static readonly string SMTP_SRV = "smtp.gmail.com";

        private static readonly string LOG_REC = "eltodd@gmail.com";

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
                                    csv.NextRecord();
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

                if (MainWindow.EMAIL_LOG_ENABLED)
                    SendMail(LOG_REC, "Robot Log " + this.filename, "New Data Available " + this.filename, this.filename);
            });
        }

        public void WriteCSVHeaders(List<string> headerData)
        {
            lock (lockObject)
            {
                LogData(headerData);
                HeadersWritten = true;
            }
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

        private static void SendMail(string recipient, string subject, string body, string attachmentFilePath)
        {
            try
            {
                SmtpClient smtpClient = new SmtpClient();
                NetworkCredential basicCredential = new NetworkCredential(SMTP_USER, SMTP_PASS);
                MailMessage message = new MailMessage();
                MailAddress fromAddress = new MailAddress(SMTP_USER);

                // setup up the host, increase the timeout to 5 minutes
                smtpClient.Host = SMTP_SRV;
                smtpClient.EnableSsl = true;
                //TLS Only
                smtpClient.Port = 587;
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = basicCredential;
                smtpClient.Timeout = (60 * 5 * 1000);

                message.From = fromAddress;
                message.Subject = subject;
                message.IsBodyHtml = false;
                message.Body = body;
                message.To.Add(recipient);

                if (!String.IsNullOrWhiteSpace(attachmentFilePath))
                {
                    Attachment attachment = new Attachment(attachmentFilePath, MediaTypeNames.Application.Octet);
                    ContentDisposition disposition = attachment.ContentDisposition;
                    disposition.CreationDate = File.GetCreationTime(attachmentFilePath);
                    disposition.ModificationDate = File.GetLastWriteTime(attachmentFilePath);
                    disposition.ReadDate = File.GetLastAccessTime(attachmentFilePath);
                    disposition.FileName = Path.GetFileName(attachmentFilePath);
                    disposition.Size = new FileInfo(attachmentFilePath).Length;
                    disposition.DispositionType = DispositionTypeNames.Attachment;
                    message.Attachments.Add(attachment);
                }

                smtpClient.Send(message);
            } catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
