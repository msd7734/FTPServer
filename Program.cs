using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FTPServer
{
    /// <summary>
    /// The entry point for operating the FTPServer.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Initializing server.");
            FTPServer server = new FTPServer();
            Console.WriteLine("Starting server.");
            server.Start();
            Console.WriteLine("Awaiting connections on {0}", server.LocalIP.ToString());
            try
            {
                server.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("!! The server closed unexpectedly due to an unhandled error.");
                Console.Error.WriteLine(e.Message);
            }
            
        }
    }
}
