using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace FTPServer
{
    public class DataConnection
    {
        private TcpClient client;

        public DataConnection(IPAddress ip, int port)
        {
            client = new TcpClient();
            client.Connect(ip, port);
        }

        ~DataConnection()
        {
            try
            {
                client.Close();
            }
            catch (NullReferenceException nre)
            {
                // already closed, no need to do anything
            }
            
        }

        public void SendData(String msg)
        {
            byte[] b = Encoding.ASCII.GetBytes(msg);
            SendData(b, b.Length);
        }

        public void SendData(byte[] b, int bytesToWrite)
        {
            NetworkStream stream = client.GetStream();
            stream.Write(b, 0, bytesToWrite);
            Console.WriteLine("=> Sent {0} bytes to client.", b.Length);
        }

        /// <summary>
        /// Close the underlying TCPClient
        /// </summary>
        public void Close()
        {
            client.Close();
        }

        public bool IsConnected()
        {
            try
            {
                return client.Connected;
            }
            catch (NullReferenceException nre)
            {
                return false;
            }
        }
    }
}
