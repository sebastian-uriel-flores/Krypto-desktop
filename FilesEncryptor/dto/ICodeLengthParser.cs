using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto
{
    public interface ICodeLengthParser
    {
        object Parse(string codeLength);
    }
}
