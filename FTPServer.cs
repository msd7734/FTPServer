﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace FTPServer
{
    public class FTPServer
    {
        public const int PORT = 2121;

        #region Message Constants

        public static readonly int DIR_INCOMING = 150;
        public static readonly string MSG_DIR_INCOMING = "Here comes the directory listing.";

        public static readonly int SUCCESS = 200;
        public static readonly string MSG_NOT_SUPPORTED = "Command not supported.";
                       
        public static readonly int NEW_USER = 220;
        public static readonly string MSG_WELCOME = "Welcome to my FTP server. Please don't break anything.";

        public static readonly int GOODBYE = 221;
        public static readonly string MSG_GOODBYE = "Goodbye.";

        public static readonly int DIR_SEND = 226;
        public static readonly string MSG_DIR_SEND_OK = "Directory send OK.";

        public static readonly int PASSIVE_MODE = 227;
        public static readonly string MSG_PASSIVE_MODE = "Entering Passive Mode {0}.";              

        public static readonly int LOGIN_SUCCESS = 230;
        public static readonly string MSG_LOGIN_SUCCESS = "Login successful.";

        public static readonly int DIR_CHANGE = 250;
        public static readonly string MSG_DIR_CHANGE_SUCCESS = "Directory successfully changed.";

        public static readonly int PASSWORD = 331;
        public static readonly string MSG_PASSWORD = "Please supply the password.";
                      
        public static readonly int UNAVAILABLE = 421;
                      
        public static readonly int BAD_USER = 530;
        public static readonly string MSG_ANONYMOUS_ONLY = "This FTP server is anonymous only.";
        public static readonly string MSG_MUST_LOGIN = "Please login with USER and PASS.";
        public static readonly string MSG_CANT_CHANGE = "Can't change from guest user.";
                      
        public static readonly int PERMISSION_DENIED = 550;
        public static readonly string MSG_ACTION_NOT_TAKEN = "The requested action could not be completed.";

        #endregion

        #region Server Commands

        public static readonly string[] COMMANDS =
        {
            "USER",
            "PASS",
            "RETR",
            "LIST",
            "PORT",
            "PASV",
            "QUIT",
            "CWD"
        };

        public const int CMD_USER = 0;
        public const int CMD_PASS = 1;
        public const int CMD_RETR = 2;
        public const int CMD_LIST = 3;
        public const int CMD_PORT = 4;
        public const int CMD_PASV = 5;
        public const int CMD_QUIT = 6;
        public const int CMD_CWD = 7;

        #endregion

        private TcpListener cmdListener;
        private TcpClient cmdClient;
        private byte[] cmdBuffer;
        private TcpListener dataListener;
        private TcpClient dataClient;
        private DataConnection dataCon;

        private EventWaitHandle waitHandle = new EventWaitHandle(false, 
            EventResetMode.AutoReset, 
            "13200c5f8287cff9edfda4d5020091fe");

        public IPAddress LocalIP { get; private set; }

        public String Username { get; private set; }

        public bool UserLoggedIn { get; private set; }

        public String CurrentDirectory { get; private set; }

        public FTPServer()
        {
            cmdListener = new TcpListener(IPAddress.Any, PORT);
            cmdClient = null;
            cmdBuffer = new byte[1024];
            dataCon = null;
            dataListener = null;
            dataClient = null;

            IPHostEntry host = Dns.GetHostByName(Dns.GetHostName());
            LocalIP = host.AddressList[0];
            Username = String.Empty;
            UserLoggedIn = false;
            CurrentDirectory = Directory.GetCurrentDirectory();
        }

        ~FTPServer()
        {
            cmdListener.Stop();
        }

        public void Start()
        {
            cmdListener.Start();
        }

        public bool HasClient()
        {
            return cmdClient != null;
        }

        public void Run()
        {
            // connect loop
            while (true)
            {
                var acceptedClient = AwaitClient();
                cmdClient = acceptedClient.Result;
                Console.WriteLine("Accepted client: {0}", cmdClient.Client.LocalEndPoint.ToString());

                SendMessage(NEW_USER, MSG_WELCOME);

                // command loop
                while (HasClient())
                {
                    try
                    {
                        ClientCommand cmd = new ClientCommand(AwaitCommand().Result);
                        HandleCommand(cmd);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Client was dropped:\n>> {0}", e.Message);
                        CloseClient();
                    }
                }
            }
        }

        private void HandleCommand(ClientCommand cmd)
        {
            Console.Write("<= {0}", cmd.Text);



            int cid = -1;
            for (int i = 0; i < COMMANDS.Length && cid == -1; ++i)
            {
                if (COMMANDS[i].Equals(cmd.Operation, StringComparison.CurrentCultureIgnoreCase))
                {
                    cid = i;
                }
            }

            if (cid != CMD_USER && !UserLoggedIn)
            {
                SendMessage(BAD_USER, MSG_MUST_LOGIN);
                return;
            }

            switch (cid)
            {
                case CMD_USER:
                    User(cmd);
                    break;
                case CMD_PASS:
                    Password(cmd);
                    break;
                case CMD_RETR:
                    break;
                case CMD_LIST:
                    ListDir();
                    break;
                case CMD_PASV:
                    Passive();
                    break;
                case CMD_PORT:
                    break;
                case CMD_QUIT:
                    SendMessage(GOODBYE, MSG_GOODBYE);
                    cmdClient = null;
                    break;
                case CMD_CWD:
                    Cd(cmd);
                    break;
                default:
                    SendMessage(SUCCESS, MSG_NOT_SUPPORTED);
                    break;
            }
        }

        private void User(ClientCommand cmd)
        {
            if (UserLoggedIn)
            {
                SendMessage(BAD_USER, MSG_CANT_CHANGE);
                return;
            }

            if (cmd.Args.Length < 1 ||
                        !cmd.Args[0].Equals("anonymous", StringComparison.CurrentCultureIgnoreCase))
            {
                SendMessage(BAD_USER, MSG_ANONYMOUS_ONLY);
            }
            else
            {
                Username = cmd.Args[0];
                SendMessage(PASSWORD, MSG_PASSWORD);
                Password( new ClientCommand(AwaitCommand().Result) );
            }
        }

        private void Password(ClientCommand cmd)
        {
            if (UserLoggedIn)
            {
                SendMessage(BAD_USER, MSG_CANT_CHANGE);
                return;
            }

            //accept anything as a password
            Console.Write(cmd.Text);
            SendMessage(LOGIN_SUCCESS, MSG_LOGIN_SUCCESS);
            UserLoggedIn = true;
        }

        private void ListDir()
        {
            // CurrentDirectory
            String files = String.Join("\n", Directory.GetFileSystemEntries(CurrentDirectory))+"\r\n";
            SendMessage(DIR_INCOMING, MSG_DIR_INCOMING);
            if (IsPassive())
            {
                try
                {
                    NetworkStream stream = dataClient.GetStream();
                    byte[] b = Encoding.ASCII.GetBytes(files);
                    stream.Write(b, 0, b.Length);
                    SendMessage(DIR_SEND, MSG_DIR_SEND_OK);
                }
                catch (Exception e)
                {
                    SendMessage(PERMISSION_DENIED, MSG_ACTION_NOT_TAKEN);
                }
                finally
                {
                    dataClient.Close();
                    dataListener.Stop();
                    dataClient = null;
                    dataListener = null;
                }
            }
            else
            {
                // implicitly active
            }
        }

        private void Cd(ClientCommand cmd)
        {
            String cdStr;
            if (cmd.Args.Length < 1)
                cdStr = String.Empty;
            else
                cdStr = cmd.Args[0];

            Uri currentDir = new Uri(CurrentDirectory);
            Uri newDir = new Uri(currentDir, cdStr);

            CurrentDirectory = newDir.LocalPath;

            SendMessage(DIR_CHANGE, MSG_DIR_CHANGE_SUCCESS);
        }

        private void Passive()
        {
            dataListener = new TcpListener(IPAddress.Any, 0);
            dataListener.Start(1);

            IPHostEntry host = Dns.GetHostByName(Dns.GetHostName());
            IPAddress localIp = host.AddressList[0];

            String commaSepIP = String.Join(",", localIp.ToString().Split('.'));
            int port = Int32.Parse(dataListener.LocalEndpoint.ToString().Split(':')[1]);

            int octet1 = port / 256;
            int octet2 = port % 256;
            String ipParam = String.Format("{0},{1},{2}", commaSepIP, octet1, octet2);

            SendMessage(PASSIVE_MODE, String.Format(MSG_PASSIVE_MODE, ipParam));
            dataClient = dataListener.AcceptTcpClient();
        }

        private void SendMessage(int code, String msg)
        {
            NetworkStream stream = cmdClient.GetStream();
            String fullMsg = String.Format("{0} {1}\r\n", code, msg);
            byte[] b = Encoding.ASCII.GetBytes(fullMsg);
            stream.Write(b, 0, b.Length);
            Console.Write("=>  {0}", fullMsg);
        }

        private void CloseClient()
        {
            cmdClient = null;
        }

        private bool IsPassive()
        {
            return (dataClient != null);
        }

        /*
        private bool IsPassive()
        {
            if (dataListener == null)
                return true;
            else if (dataListener != null && dataCon != null)
                throw new Exception("Mismatched passive/active states.");
            else
                return false;
        }
        */

        async Task<TcpClient> AwaitClient()
        {
            return await cmdListener.AcceptTcpClientAsync();
        }

        async Task<String> AwaitCommand()
        {
            StreamReader reader = new StreamReader(cmdClient.GetStream(), Encoding.ASCII);
            return await reader.ReadLineAsync();
        }
    }
}