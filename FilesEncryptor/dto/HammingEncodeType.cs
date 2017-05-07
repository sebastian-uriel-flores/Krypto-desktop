using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto
{
    public class HammingEncodeType
    {
        public string Name { get; set; }

        public string Extension { get; set; }

        public uint WordBitsSize { get; set; }

        private HammingEncodeType()
        {

        }

        public HammingEncodeType(string name, string extension, uint wordBitsSize)
        {
            Name = name;
            Extension = extension;
            WordBitsSize = wordBitsSize;
        }
    }
}
