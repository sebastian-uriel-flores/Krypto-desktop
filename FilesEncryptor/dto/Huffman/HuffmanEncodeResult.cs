using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto.huffman
{
    public class HuffmanEncodeResult
    {
        public BitCode Encoded { get; set; }

        public ReadOnlyDictionary<char, BitCode> ProbabilitiesTable { get; set; }

        public List<uint> CodePartsLengths { get; set; }

        public HuffmanEncodeResult(BitCode encoded, ReadOnlyDictionary<char, BitCode> probabilitiesTable, List<uint> codePartsLengths)
        {
            Encoded = encoded;
            ProbabilitiesTable = probabilitiesTable;
            CodePartsLengths = codePartsLengths;
        }
    }
}
