using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto
{
    public class HammingEncodeResult : IEncodeResult
    {
        public BitCode Encoded { get; set; }

        public HammingEncodeType EncodeType { get; set; }

        private HammingEncodeResult()
        {

        }

        public HammingEncodeResult(BitCode encoded, HammingEncodeType encodeType)
        {
            Encoded = encoded;
            EncodeType = encodeType;
        }
    }
}
