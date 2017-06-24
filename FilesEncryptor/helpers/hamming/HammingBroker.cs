using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.hamming
{
    public class HammingBroker : HammingDecoder
    {
        private Random _moduleRandom, _bitPositionRandom;

        protected HammingBroker(HammingEncodeType encodeType, BitCode hammingCode, uint redundanceBitsCount) : base(encodeType, hammingCode, redundanceBitsCount)
        {
            _moduleRandom = new Random();
            _bitPositionRandom = new Random();
        }
        public HammingBroker(FileHelper fileHelper, HammingEncodeType encodeType) : base(fileHelper, encodeType)
        {
            _moduleRandom = new Random();
            _bitPositionRandom = new Random();
        }
        public HammingBroker(HammingEncodeResult encodeResult) : base(encodeResult)
        {
            _moduleRandom = new Random();
            _bitPositionRandom = new Random();
        }

        public Task<HammingEncodeResult> Broke()
        {
            return Task.Run(() =>
            {
                BitCode brokenCode = _fullCode.Copy();
                int wordsWithError = 0;

                //Por cada palabra, determino aleatoriamente si insertar o no errores
                for(uint index = 0; (int)index < _fullCode.CodeLength; index+= _encodeType.WordBitsSize)
                {
                    if(InsertErrorInModule())
                    {
                        uint replacePos = SelectBitPositionRandom(index, index + _encodeType.WordBitsSize - 1);
                        DebugUtils.ConsoleWL(string.Format("Insert error in word {0} bit {1}", index / _encodeType.WordBitsSize, replacePos), "[PROGRESS]");
                        brokenCode = brokenCode.ReplaceAt(replacePos, brokenCode.ElementAt(replacePos).Negate());
                        wordsWithError++;
                    }
                }

                DebugUtils.ConsoleWL(string.Format("Inserting errors finished with {0} words with error", wordsWithError));

                return new HammingEncodeResult(brokenCode, _encodeType, new HammingCodeLength()
                {
                    FullCodeLength = (uint)brokenCode.CodeLength,
                    RedundanceCodeLength = _redundanceBitsCount
                });
            });
        }
        
        private bool InsertErrorInModule() => _moduleRandom.Next(-15, 1) >= 0;

        private uint SelectBitPositionRandom(uint min, uint max) => (uint)_bitPositionRandom.Next((int)min, (int)max);
    }
}
