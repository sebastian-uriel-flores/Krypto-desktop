using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto.hamming
{
    public class HammingCodeLength
    {
        public uint FullCodeLength { get; set; }

        public uint RedundanceCodeLength { get; set; }
    }
}