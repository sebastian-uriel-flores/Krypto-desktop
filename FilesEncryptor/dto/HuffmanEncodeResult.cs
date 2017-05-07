using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto
{
    public class HuffmanEncodeResult
    {
        public BitCode Encoded { get; private set; }

        public string EncodedProbabilitiesTable { get; private set; }

        public ReadOnlyDictionary<char, BitCode> ProbabilitiesTable { get; private set; }

        public HuffmanEncodeResult(BitCode encoded, ReadOnlyDictionary<char, BitCode> probabilitiesTable)
        {
            Encoded = encoded;
            ProbabilitiesTable = probabilitiesTable;
        }
    }
}
