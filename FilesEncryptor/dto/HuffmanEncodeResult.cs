using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto
{
    public class HuffmanEncodeResult
    {
        public EncodedString Encoded { get; private set; }

        public string EncodedProbabilitiesTable { get; private set; }

        public HuffmanEncodeResult(EncodedString encoded, string encodedProbabilitiesTable)
        {
            Encoded = encoded;
            EncodedProbabilitiesTable = encodedProbabilitiesTable;
        }
    }
}
