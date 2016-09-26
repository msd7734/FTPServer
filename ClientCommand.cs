using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FTPServer
{
    public class ClientCommand
    {
        public String Operation { get; private set; }

        public String[] Args { get; private set; }

        public String Text { get; private set; }

        public ClientCommand(String cmd)
        {
            String[] tokens = cmd.Split(' ');
            Operation = tokens[0];
            Args = tokens.ToList().GetRange(1, tokens.Length - 1).ToArray();
            if (cmd.Substring(cmd.Length - 2, 2) != "\r\n")
            {
                cmd = cmd + "\r\n";
            }
            Text = cmd;
        }

        public bool IsEmpty()
        {
            return String.IsNullOrWhiteSpace(Operation);
        }
    }
}
