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

        public DataConnection(String hostName, int port)
        {
            client = new TcpClient();
            client.Connect(hostName, port);
        }

        ~DataConnection()
        {
            client.Close();
        }

        public void SendData(String msg)
        {
            SendData( Encoding.ASCII.GetBytes(msg) );
        }

        public void SendData(byte[] b)
        {
            NetworkStream stream = client.GetStream();
            stream.Write(b, 0, b.Length);
            Console.WriteLine("=> Sent {0} bytes to client.", b.Length);
        }
    }
}
