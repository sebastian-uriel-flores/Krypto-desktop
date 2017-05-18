using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto.Huffman
{
    public class HuffmanCodeLengthParser : ICodeLengthParser
    {
        public object Parse(string codeLength)
        {
            return uint.Parse(codeLength);
        }
    }
}
