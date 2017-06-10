using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto.Hamming
{
    public class HammingCodeLengthParser : ICodeLengthParser
    {
        public const char SEPARATOR = ',';
        public object Parse(string codeLength)
        {
            HammingCodeLength hammingCodeLength = null;

            var splitted = codeLength.Split(SEPARATOR);
            
            if(splitted?.Length == 2)
            {
                hammingCodeLength = new HammingCodeLength()
                {
                    FullCodeLength = uint.Parse(splitted[0]),
                    RedundanceCodeLength = uint.Parse(splitted[1])
                };
            }

            return hammingCodeLength;
        }
    }
}
