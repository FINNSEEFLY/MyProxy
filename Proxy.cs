using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace MyProxy
{
    public partial class Proxy : ServiceBase
    {
        private const string HOST = "127.0.0.1";
        readonly private char[]  Separators = new char[] { '\r', '\n' };
        private const int PROXY_PORT = 8080;
        private const int RECIVE_BUFFER_SIZE = 15360;
        private const int DEFAULT_SERVER_PORT = 80;
        private const int FORBIDDEN = 403;
        private bool isWorking;
        private List<string> BlackList;
        private TcpListener requestListener;
        public Proxy()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            BlackList = LoadBlackList();
            requestListener = new TcpListener(IPAddress.Parse(HOST), PROXY_PORT);
            requestListener.Start();
            isWorking = true;
            ListenRequests();
            AddInLog("MyProxy launched");
        }

        protected override void OnStop()
        {
            isWorking = false;
            AddInLog("MyProxy is stopped");
        }

        private void ListenRequests()
        {
            while (isWorking)
            {
                if (requestListener.Pending())
                {
                    if (isWorking)
                    {
                        var socket = requestListener.AcceptSocket();
                        Task.Factory.StartNew(() => ReceiveData(socket));
                    }
                }
            }
            requestListener.Stop();
        }

        private void ReceiveData(Socket socket)
        {
            NetworkStream browserStream = new NetworkStream(socket);
            var buffer = new byte[RECIVE_BUFFER_SIZE];
            while (browserStream.CanRead)
            {
                try
                {
                    browserStream.Read(buffer, 0, buffer.Length);
                }
                catch (Exception e)
                {
                    AddInErrLog(e.Message);
                    return;
                }
                ProcessRequest(buffer, browserStream);
                socket.Dispose();
            }
        }

        private void ProcessRequest(byte[] buffer, NetworkStream browserstream)
        {
            buffer = FixGET(buffer);
            string[] httpRequest = ParseHTTP(buffer);
            string hostRecord = httpRequest.FirstOrDefault((substr) => substr.Contains("Host"));
            hostRecord = hostRecord.Substring(hostRecord.IndexOf(':') + 2);
            try
            {
                var tcpClient = GetTCPClient(hostRecord);
                var serverStream = tcpClient.GetStream();
                if (IsForbidden(hostRecord))
                {
                    ThrowForbidden(browserstream);
                    AddInLog(hostRecord + ' ' + FORBIDDEN);
                }
                else
                {
                    serverStream.Write(buffer, 0, buffer.Length);
                    
                    var responseBytes = new byte[32];
                    serverStream.Read(responseBytes, 0, responseBytes.Length);
                    browserstream.Write(responseBytes, 0, responseBytes.Length);

                    var responseCode = GetResponseCode(responseBytes);
                    AddInLog(hostRecord + ' ' + responseCode);
                    serverStream.CopyTo(browserstream);
                }
                serverStream.Dispose();
                tcpClient.Dispose();
            }
            catch (Exception e)
            {
                AddInErrLog(e.Message);
            }
        }

        private string GetResponseCode(byte[] response)
        {
            string[] head = Encoding.UTF8.GetString(response).Split(Separators);
            return head[0].Substring(head[0].IndexOf(" ") + 1);
        }

        private void ThrowForbidden(NetworkStream browserstream)
        {
            byte[] data = Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Type: text/html\r\nContent-Length: 52\r\n\r\nThis website is blocked. Please accept our apologies");
            browserstream.Write(data, 0, data.Length);
            return;
        }

        private bool IsForbidden(string address)
        {
            if (BlackList.Contains(address.ToLower()))
            {
                return true;
            }
            else
                return false;
        }
      
        private TcpClient GetTCPClient(string hostrecord)
        {
            string[] domainAndPort = hostrecord.Trim().Split(new char[] { ':' });
            if (domainAndPort.Length == 2)
            {
                return new TcpClient(domainAndPort[0], int.Parse(domainAndPort[1]));
            }
            else
            {
                return new TcpClient(domainAndPort[0], DEFAULT_SERVER_PORT);
            }
        }

        private string[] ParseHTTP(byte[] buffer)
        {
            return Encoding.ASCII.GetString(buffer).Trim().Split(Separators);
        }

        private byte[] FixGET(byte[] buffer)
        {
            var strBuffer = Encoding.ASCII.GetString(buffer);
            var regexp = new Regex("http:\\/\\/[\\wа-яё\\:\\.]+");
            buffer = Encoding.ASCII.GetBytes(strBuffer.Replace(regexp.Match(strBuffer).Value, ""));
            return buffer;
        }

        private void AddInLog(string str)
        {
            if (!File.Exists("Logs.txt"))
            {
                File.Create("Logs.txt").Dispose();
            }
            try
            {
                var writer = new StreamWriter("Logs.txt");
                string dateAndTime = "[" + DateTime.Now.ToString("dd.MM.yyyy|HH:mm:ss") + "] ";
                writer.WriteLine(dateAndTime + str);
                writer.Dispose();
            }
            catch (Exception e)
            {
                AddInErrLog(e.Message);
            };
        }

        private void AddInErrLog(string str)
        {
            if (!File.Exists("ProxyLog.txt"))
            {
                File.Create("ProxyLog.txt").Dispose();
            }
            try
            {
                var writer = new StreamWriter("ProxyLog.txt");
                string dateAndTime = "[" + DateTime.Now.ToString("dd.MM.yyyy|HH:mm:ss") + "] ";
                writer.WriteLine(dateAndTime + str);
                writer.Dispose();
            }
            catch { };
        }

        private List<string> LoadBlackList()
        {
            List<string> blacklist = new List<string>();
            if (File.Exists("blacklist.txt"))
            {
                try
                {
                    var reader = new StreamReader("blacklist.txt");
                    while (!reader.EndOfStream)
                    {
                        blacklist.Add(reader.ReadLine());
                    }
                    reader.Dispose();
                }
                catch (Exception e)
                {
                    AddInErrLog(e.Message);
                };
            }
            else
            {
                File.Create("blacklist.txt").Dispose();
            }
            return blacklist;
        }
    }
}
