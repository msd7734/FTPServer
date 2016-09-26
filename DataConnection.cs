using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace FTPServer
{
    /// <summary>
    /// A wrapper class for handling a data connection to a client.
    /// This is the type of connection that would be made when the client
    ///     is in Active Mode.
    /// </summary>
    public class DataConnection
    {
        private TcpClient client;

        /// <summary>
        /// Connect to an active client.
        /// </summary>
        /// <param name="ip">The client's IP.</param>
        /// <param name="port">The port to connect to.</param>
        public DataConnection(IPAddress ip, int port)
        {
            client = new TcpClient();
            client.Connect(ip, port);
        }

        /// <summary>
        /// Destructor to ensure the connection closes.
        /// </summary>
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

        /// <summary>
        /// Send data to the active client over the data connection.
        /// </summary>
        /// <param name="msg">A string that will be converted to ASCII before being sent.</param>
        public void SendData(String msg)
        {
            byte[] b = Encoding.ASCII.GetBytes(msg);
            SendData(b, b.Length);
        }

        /// <summary>
        /// Send data to the active client over the data connection.
        /// </summary>
        /// <param name="b">A buffer of bytes to write to the connection.</param>
        /// <param name="bytesToWrite">The number of bytes in the buffer to write.</param>
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

        /// <summary>
        /// Whether the client is still connected on this DataConnection.
        /// </summary>
        /// <returns>True if the client is connected, false otherwise.</returns>
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
