/*
 * By: Matthew Dennis (msd7734)
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FTPServer
{
    /// <summary>
    /// Parse out the parts of a true FTP command.
    /// </summary>
    public class ClientCommand
    {
        /// <summary>
        /// The keyword that determines the server operation.
        /// </summary>
        public String Operation { get; private set; }

        /// <summary>
        /// Arguments given that affect the operation.
        /// </summary>
        public String[] Args { get; private set; }

        /// <summary>
        /// The entire original text as received.
        /// </summary>
        public String Text { get; private set; }

        /// <summary>
        /// Construct a ClientCommand with the given string.
        /// </summary>
        /// <param name="cmd">A command in the following format: [OP] [ARG0] ... [ARGN]</param>
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

        /// <summary>
        /// Whether this command is empty due to a NOOP or blank sent command.
        /// </summary>
        /// <returns>True if empty, false otherwise.</returns>
        public bool IsEmpty()
        {
            return String.IsNullOrWhiteSpace(Operation);
        }
    }
}
