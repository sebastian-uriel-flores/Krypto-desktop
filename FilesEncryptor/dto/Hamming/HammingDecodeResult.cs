using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto.Hamming
{
    public class HammingDecodeResult
    {
        public string FileExtension { get; set; }

        public string FileDescription { get; set; }

        public string FileName { get; set; }

        public BitCode Decoded { get; set; }
    }
}
