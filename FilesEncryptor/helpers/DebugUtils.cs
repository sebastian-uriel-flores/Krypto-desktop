using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers
{
    public static class DebugUtils
    {
        public static void Write(object message, string category = "[INFO]")
        {
            Debug.Write(string.Format("({0}) - {1}", DateTime.Now, message), category);
        }
        public static void WriteLine(object message, string category = "[INFO]")
        {
            Debug.WriteLine(string.Format("({0}) - {1}", DateTime.Now, message), category);
        }
        public static void Fail(object shortMessage, string detailedMessage = "")
        {
            Debug.Fail(string.Format("({0}) - {1}", DateTime.Now, shortMessage), detailedMessage);
        }
    }
}
