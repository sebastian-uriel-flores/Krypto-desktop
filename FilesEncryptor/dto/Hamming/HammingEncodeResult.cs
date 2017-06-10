using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto.hamming
{
    public class HammingEncodeResult
    {
        public BitCode Encoded { get; set; }

        public HammingEncodeType EncodeType { get; set; }

        public HammingCodeLength Length { get; set; }


        private HammingEncodeResult()
        {

        }

        public HammingEncodeResult(BitCode encoded, HammingEncodeType encodeType, HammingCodeLength length)
        {
            Encoded = encoded;
            EncodeType = encodeType;
            Length = length;
        }
    }
}
