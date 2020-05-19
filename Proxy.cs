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
        private const int PROXY_PORT = 8080;
        private const string PATH_TO_FILES = "D:\\MyProxyFiles\\";
        private const string FORBIDDEN_MESSAGE = "HTTP/1.1 403 Forbidden\r\nContent-Type: text/html\r\nContent-Length: 52\r\n\r\nThis website is blocked. Please accept our apologies";
        private const string DATE_AND_TIME_FORMAT = "dd.MM.yyyy|HH:mm:ss";
        private const string FILENAME_PROXY_LOG = "Log.txt";
        private const string FILENAME_PROXY_ERR_LOG = "ErrLog.txt";
        private const string FILENAME_BLACKLIST = "Blacklist.txt";
        readonly private char[] Separators = new char[] { '\r', '\n' };
        private const int RECIVE_BUFFER_SIZE = 15360;
        private const int DEFAULT_SERVER_PORT = 80;
        private const int FORBIDDEN = 403;
        private bool isWorking;
        readonly private List<string> BlackList;
        readonly private TcpListener requestListener;

        public Proxy()
        {
            InitializeComponent();
            BlackList = LoadBlackList();
            requestListener = new TcpListener(IPAddress.Parse(HOST), PROXY_PORT);
        }

        protected override void OnStart(string[] args)
        {
            requestListener.Start();
            isWorking = true;
            AddInLog("MyProxy launched");
            Task.Factory.StartNew(ListenRequests);
        }

        protected override void OnStop()
        {
            isWorking = false;
            AddInLog("MyProxy is stopped");
        }

        protected override void OnShutdown()
        {
            isWorking = false;
            AddInLog("MyProxy is stopped by OS");
            base.OnShutdown();
        }

        protected override void OnPause()
        {
            AddInLog("MyProxy is paused");
            base.OnPause();
        }

        protected override void OnContinue()
        {
            AddInLog("MyProxy has resumed working");
            base.OnContinue();
        }

        // Слушаем входящие запросы браузера
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

        // Получаем получаем данные от браузера
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

        // Обработка полученного запроса
        private void ProcessRequest(byte[] buffer, NetworkStream browserstream)
        {
            buffer = FixGET(buffer);
            string[] httpRequest = ParseHTTP(buffer);
            string fullHostRecord = httpRequest.FirstOrDefault((substr) => substr.Contains("Host"));
            string hostRecord = fullHostRecord.Substring(fullHostRecord.IndexOf(':') + 2);
            try
            {
                if (IsForbidden(hostRecord))
                {
                    ThrowForbidden(browserstream);
                    AddInLog(hostRecord + ' ' + FORBIDDEN);
                }
                else
                {
                    var tcpClient = GetTCPClient(hostRecord);
                    var serverStream = tcpClient.GetStream();
                    serverStream.Write(buffer, 0, buffer.Length);

                    var responseBytes = new byte[32];
                    serverStream.Read(responseBytes, 0, responseBytes.Length);
                    browserstream.Write(responseBytes, 0, responseBytes.Length);

                    var responseCode = GetResponseCode(responseBytes);
                    AddInLog(hostRecord + ' ' + responseCode);
                    serverStream.CopyTo(browserstream);
                    serverStream.Dispose();
                    tcpClient.Dispose();
                }
            }
            catch (Exception e)
            {
                AddInErrLog(e.Message);
            }
        }

        // Извлекает из ответа от сервера код ответа
        private string GetResponseCode(byte[] response)
        {
            string[] head = Encoding.ASCII.GetString(response).Split(Separators);
            return head[0].Substring(head[0].IndexOf(" ") + 1);
        }

        // Отправляет пользователю сообщение о запрете перехода
        private void ThrowForbidden(NetworkStream browserstream)
        {
            byte[] data = Encoding.ASCII.GetBytes(FORBIDDEN_MESSAGE);
            browserstream.Write(data, 0, data.Length);
            return;
        }

        // Проверка, есть ли адрес в черном списке
        private bool IsForbidden(string address)
        {
            if (BlackList.Contains(address.ToLower()))
            {
                return true;
            }
            else
                return false;
        }

        // Получает TCPClient из Host
        private TcpClient GetTCPClient(string hostrecord)
        {
            string[] hostNameAndPort = hostrecord.Trim().Split(new char[] { ':' });
            return new TcpClient(hostNameAndPort[0], (hostNameAndPort.Length == 2) ? int.Parse(hostNameAndPort[1]) : DEFAULT_SERVER_PORT);
        }

        // Разбивает ответ от сервера на строки
        private string[] ParseHTTP(byte[] buffer)
        {
            return Encoding.ASCII.GetString(buffer).Trim().Split(Separators);
        }

        // Преобразует путь в относительный 
        private byte[] FixGET(byte[] buffer)
        {
            var strBuffer = Encoding.ASCII.GetString(buffer);
            var regexp = new Regex("http:\\/\\/[\\wа-яё\\:\\.]+");
            buffer = Encoding.ASCII.GetBytes(strBuffer.Replace(regexp.Match(strBuffer).Value, ""));
            return buffer;
        }

        // Добавление записи в основной лог
        private void AddInLog(string str)
        {
            if (!File.Exists(PATH_TO_FILES + FILENAME_PROXY_LOG))
            {
                File.Create(PATH_TO_FILES + FILENAME_PROXY_LOG).Dispose();
            }
            try
            {
                string dateAndTime = "[" + DateTime.Now.ToString(DATE_AND_TIME_FORMAT) + "] ";
                File.AppendAllText(PATH_TO_FILES + FILENAME_PROXY_LOG, dateAndTime + str + "\r\n");
            }
            catch (Exception e)
            {
                AddInErrLog(e.Message);
            };
        }

        // Добавление записи в лог ошибок
        private void AddInErrLog(string str)
        {
            if (!File.Exists(PATH_TO_FILES + FILENAME_PROXY_ERR_LOG))
            {
                File.Create(PATH_TO_FILES + FILENAME_PROXY_ERR_LOG).Dispose();
            }
            try
            {
                string dateAndTime = "[" + DateTime.Now.ToString(DATE_AND_TIME_FORMAT) + "] ";
                File.AppendAllText(PATH_TO_FILES + FILENAME_PROXY_ERR_LOG, dateAndTime + str + "\r\n");
            }
            catch { };
        }

        // Загрузить черный список
        private List<string> LoadBlackList()
        {
            List<string> blacklist = new List<string>();
            if (File.Exists(PATH_TO_FILES + FILENAME_BLACKLIST))
            {
                try
                {
                    var reader = new StreamReader(PATH_TO_FILES + FILENAME_BLACKLIST);
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
                File.Create(PATH_TO_FILES + FILENAME_BLACKLIST).Dispose();
            }
            return blacklist;
        }
    }
}
