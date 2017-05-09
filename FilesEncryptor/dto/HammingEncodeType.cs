using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto
{
    public class HammingEncodeType
    {
        public string ShortDescription { get; set; }

        public string LongDescription { get; set; }

        public string Extension { get; set; }

        public uint WordBitsSize { get; set; }

        private HammingEncodeType()
        {

        }

        public HammingEncodeType(string shortDescription, string longDescription, string extension, uint wordBitsSize)
        {
            ShortDescription = shortDescription;
            LongDescription = longDescription;
            Extension = extension;
            WordBitsSize = wordBitsSize;
        }
    }
}
