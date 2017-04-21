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
        public EncodedString Encoded { get; private set; }

        public string EncodedProbabilitiesTable { get; private set; }

        public ReadOnlyDictionary<char, EncodedString> ProbabilitiesTable { get; private set; }

        public HuffmanEncodeResult(EncodedString encoded, ReadOnlyDictionary<char, EncodedString> probabilitiesTable)
        {
            Encoded = encoded;
            ProbabilitiesTable = probabilitiesTable;
        }
    }
}
