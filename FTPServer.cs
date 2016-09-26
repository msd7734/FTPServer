using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace FTPServer
{
    public enum DataMode
    {
        ASCII,
        Binary
    }

    public class FTPServer
    {
        public const int PORT = 2121;

        #region Message Constants

        public static readonly int INCOMING = 150;
        public static readonly string MSG_DIR_INCOMING = "Here comes the directory listing.";
        public static readonly string MSG_FILE_INCOMING = "Opening {0} mode data connection for {1} ({2} bytes).";

        public static readonly int SUCCESS = 200;
        public static readonly string MSG_NOT_SUPPORTED = "Command not supported.";
        public static readonly string MSG_MODE_SWITCH = "Switching to {0} mode.";
        public static readonly string MSG_PORT_SUCCESS = "Port command successful.";
                       
        public static readonly int NEW_USER = 220;
        public static readonly string MSG_WELCOME = "Welcome to my FTP server. Please don't break anything.";

        public static readonly int GOODBYE = 221;
        public static readonly string MSG_GOODBYE = "Goodbye.";

        public static readonly int GOOD_SEND = 226;
        public static readonly string MSG_DIR_SEND_OK = "Directory send OK.";
        public static readonly string MSG_FILE_SEND_OK = "Transfer complete.";

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
        public static readonly string MSG_FAILED_TO_OPEN = "Failed to open file.";
        public static readonly string MSG_FAILED_TO_CHANGE = "Failed to change directory.";

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
            "CWD",
            "TYPE"
        };

        public const int CMD_USER = 0;
        public const int CMD_PASS = 1;
        public const int CMD_RETR = 2;
        public const int CMD_LIST = 3;
        public const int CMD_PORT = 4;
        public const int CMD_PASV = 5;
        public const int CMD_QUIT = 6;
        public const int CMD_CWD = 7;
        public const int CMD_TYPE = 8;

        #endregion

        private TcpListener cmdListener;
        private TcpClient cmdClient;
        private byte[] cmdBuffer;
        private TcpListener dataListener;
        private TcpClient dataClient;
        private DataConnection dataCon;

        public IPAddress LocalIP { get; private set; }

        public String Username { get; private set; }

        public bool UserLoggedIn { get; private set; }

        public String CurrentDirectory { get; private set; }

        public DataMode DatMode { get; private set; }

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
                    Retrieve(cmd);
                    break;
                case CMD_LIST:
                    ListDir();
                    break;
                case CMD_PASV:
                    Passive();
                    break;
                case CMD_PORT:
                    Port(cmd);
                    break;
                case CMD_QUIT:
                    SendMessage(GOODBYE, MSG_GOODBYE);
                    cmdClient = null;
                    break;
                case CMD_CWD:
                    Cd(cmd);
                    break;
                case CMD_TYPE:
                    ChangeType(cmd);
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
            String files = String.Empty;

            try
            {
                files = String.Join("\n", Directory.GetFileSystemEntries(CurrentDirectory)) + "\r\n";
            }
            catch (Exception e)
            {
                SendMessage(PERMISSION_DENIED, MSG_ACTION_NOT_TAKEN);
            }

            SendMessage(INCOMING, MSG_DIR_INCOMING);

            if (IsPassive())
            {
                try
                {
                    NetworkStream stream = dataClient.GetStream();
                    byte[] b = Encoding.ASCII.GetBytes(files);
                    stream.Write(b, 0, b.Length);
                    SendMessage(GOOD_SEND, MSG_DIR_SEND_OK);
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
                try
                {
                    dataCon.SendData(files);
                    SendMessage(GOOD_SEND, MSG_DIR_SEND_OK);
                }
                catch (Exception e)
                {
                    SendMessage(PERMISSION_DENIED, MSG_ACTION_NOT_TAKEN);
                }
                finally
                {
                    dataCon.Close();
                    dataCon = null;
                }
            }
        }

        private void Cd(ClientCommand cmd)
        {
            String cdStr;
            if (cmd.Args.Length < 1)
                cdStr = String.Empty;
            else
                cdStr = cmd.Args[0];

            try
            {
                String newDir = Path.GetFullPath(Path.Combine(CurrentDirectory, cdStr));
                if (!Directory.Exists(newDir))
                {
                    SendMessage(PERMISSION_DENIED, MSG_FAILED_TO_CHANGE);
                    return;
                }

                CurrentDirectory = newDir;
                SendMessage(DIR_CHANGE, MSG_DIR_CHANGE_SUCCESS);
            }
            catch (Exception e)
            {
                SendMessage(PERMISSION_DENIED, MSG_FAILED_TO_CHANGE);
            }
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

        private void Port(ClientCommand cmd)
        {
            if (cmd.Args.Length < 1)
            {
                SendMessage(PERMISSION_DENIED, MSG_ACTION_NOT_TAKEN);
                return;
            }

            if (dataCon != null && dataCon.IsConnected())
            {
                dataCon.Close();
                dataCon = null;
            }


            String targetRegex = @"(\d{1,3},){5}\d{1,3}";
            Match m = Regex.Match(cmd.Text, targetRegex);

            if (!m.Success)
            {
                Console.Error.Write(
                    "Attempted to connect in active mode but client seems to have given no connection information:\n{0}",
                    cmd.Text
                );
                return;
            }

            String target = m.Value;
            String[] byteStrs = target.Split(',');
            String ipStr = String.Join<String>(".", byteStrs.Take<String>(4));
            System.Net.IPAddress IP = System.Net.IPAddress.Parse(ipStr);

            String[] octetStrs = { byteStrs[byteStrs.Length - 2], byteStrs[byteStrs.Length - 1] };
            int port = (Int32.Parse(octetStrs[0]) * 256) + Int32.Parse(octetStrs[1]);

            dataCon = new DataConnection(IP, port);

            SendMessage(SUCCESS, MSG_PORT_SUCCESS);
        }

        private void ChangeType(ClientCommand cmd)
        {
            if (cmd.Args.Length < 1)
            {
                SendMessage(PERMISSION_DENIED, MSG_ACTION_NOT_TAKEN);
                return;
            }

            String modeStr = cmd.Args[0];
            if (modeStr.Equals("A", StringComparison.CurrentCultureIgnoreCase))
            {
                DatMode = DataMode.ASCII;
            }
            else
            {
                DatMode = DataMode.Binary;
            }

            SendMessage(SUCCESS, String.Format(MSG_MODE_SWITCH, DatMode.ToString()));
        }

        private void Retrieve(ClientCommand cmd)
        {
            if (cmd.Args.Length < 1)
            {
                SendMessage(PERMISSION_DENIED, MSG_FAILED_TO_OPEN);
                return;
            }

            String filePath = Path.Combine(CurrentDirectory, cmd.Args[0]);

            FileInfo finfo = new FileInfo(filePath);
            SendMessage(INCOMING, String.Format(MSG_FILE_INCOMING, DatMode.ToString(), Path.GetFileName(filePath), finfo.Length));

            if (IsPassive())
            {
                try
                {
                    // transmit ASCII
                    if (DatMode == DataMode.ASCII)
                    {
                        using (StreamReader reader = new StreamReader(File.OpenRead(filePath)))
                        {
                            NetworkStream stream = dataClient.GetStream();
                            String line;

                            while ((line = reader.ReadLine()) != null)
                            {
                                // append line endings
                                byte[] b = Encoding.ASCII.GetBytes(line+"\r\n");
                                stream.Write(b, 0, b.Length);
                            }
                        
                        }
                    }
                    // transmit binary
                    else 
                    {
                        using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath)))
                        {
                            NetworkStream stream = dataClient.GetStream();
                            byte[] buf = new byte[0x40000];
                            int bytesRead = 0;
                            do
                            {
                                Array.Clear(buf, 0, buf.Length);
                                bytesRead = reader.Read(buf, 0, buf.Length);
                                stream.Write(buf, 0, bytesRead);
                            }
                            while (bytesRead != 0);
                        }
                    }
                    SendMessage(GOOD_SEND, MSG_FILE_SEND_OK);
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
                try
                {
                    // transmit ASCII
                    if (DatMode == DataMode.ASCII)
                    {
                        using (StreamReader reader = new StreamReader(File.OpenRead(filePath)))
                        {
                            String line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                // append line endings
                                dataCon.SendData(line + "\r\n");
                            }

                        }
                    }
                    // transmit binary
                    else
                    {
                        using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath)))
                        {
                            byte[] buf = new byte[0x40000];
                            int bytesRead = 0;
                            do
                            {
                                Array.Clear(buf, 0, buf.Length);
                                bytesRead = reader.Read(buf, 0, buf.Length);
                                dataCon.SendData(buf, bytesRead);
                            }
                            while (bytesRead != 0);
                        }
                    }

                    SendMessage(GOOD_SEND, MSG_FILE_SEND_OK);
                }
                catch (Exception e)
                {
                    SendMessage(PERMISSION_DENIED, MSG_ACTION_NOT_TAKEN);
                }
                finally
                {
                    dataCon.Close();
                    dataCon = null;
                }

            }
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
