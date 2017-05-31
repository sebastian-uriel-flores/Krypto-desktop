﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto.Huffman
{
    public class HuffmanEncodeResult : IEncodeResult
    {
        public BitCode Encoded { get; set; }

        public string EncodedProbabilitiesTable { get; set; }

        public ReadOnlyDictionary<char, BitCode> ProbabilitiesTable { get; set; }

        public HuffmanEncodeResult(BitCode encoded, ReadOnlyDictionary<char, BitCode> probabilitiesTable)
        {
            Encoded = encoded;
            ProbabilitiesTable = probabilitiesTable;
        }
    }
}