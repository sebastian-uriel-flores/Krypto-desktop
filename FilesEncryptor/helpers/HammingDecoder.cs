using FilesEncryptor.dto;
using FilesEncryptor.dto.Hamming;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers
{
    public class HammingDecoder : BaseHammingCodifier
    {
        private uint _redundanceBitsCount;
        private BitCode _fullCode;
        private HammingEncodeType _encodeType;

        private HammingDecoder()
        {

        }

        public static HammingDecoder FromFile(FileHelper fileHelper, HammingEncodeType encodeType)
        {
            //Obtengo la cantidad de bits del codigo completo, incluyendo la redundancia
            string fullCodeLength = fileHelper.ReadStringUntil(",");

            //Obtengo la cantidad de bits de redundancia ubicados al final del código
            string redundanceCodeLength = fileHelper.ReadStringUntil(":");

            //Obtengo los bytes del codigo, incluyendo la redundancia
            byte[] fullCodeBytes = fileHelper.ReadBytes(CommonUtils.BitsLengthToBytesLength(uint.Parse(fullCodeLength)));

            return new HammingDecoder()
            {
                _encodeType = encodeType,
                _fullCode = new BitCode(fullCodeBytes.ToList(), int.Parse(fullCodeLength)),
                _redundanceBitsCount = uint.Parse(redundanceCodeLength)
            };
        }

        public static HammingDecoder FromEncoded(HammingEncodeResult encodeResult)
        {
            return new HammingDecoder()
            {
                _fullCode = encodeResult.Encoded,
                _redundanceBitsCount = encodeResult.Length.RedundanceCodeLength,
                _encodeType = encodeResult.EncodeType
            };
        }
        
        public async Task<BitCode> Decode()
        {
            BitCode result = BitCode.EMPTY;

            await Task.Factory.StartNew(() =>
            {
                DebugUtils.WriteLine("Checking words parity");

                //Separo el codigo completo en bloques representando a cada palabra del mismo
                List<BitCode> parityControlMatrix = CreateParityControlMatrix(_encodeType);
                uint encodedWordSize = (uint)parityControlMatrix[0].CodeLength;

                List<BitCode> encodedWords = _fullCode.Explode(encodedWordSize, false).Item1;

                //BitCodePresenter.From(encodedWords).Print(BitCodePresenter.LinesDisposition.Row, "Encoded matrix");

                //TODO:Chequeo la paridad en cada una de las palabras, utilizando la matriz de control de paridad

                //Decodifico cada una de las palabras
                DebugUtils.WriteLine("Decodifying words");

                List<BitCode> decodedWords = new List<BitCode>(encodedWords.Count);
                List<uint> controlBitsIndexes = GetControlBitsIndexes(_encodeType);

                foreach (BitCode encoded in encodedWords)
                {
                    BitCode decoded = BitCode.EMPTY;

                    foreach (uint index in GetDataBitsIndexes((uint)encoded.CodeLength, controlBitsIndexes))
                    {
                        decoded.Append(encoded.ElementAt(index));
                    }

                    decodedWords.Add(decoded);
                }

                BitCodePresenter.From(decodedWords).Print(BitCodePresenter.LinesDisposition.Row, "Decoded matrix");

                //Junto todas las palabras decodificadas en un solo codigo
                result = BitOps.Join(decodedWords);

                //Remuevo los bits de redundancia
                result = result.GetRange(0, (uint)result.CodeLength - _redundanceBitsCount);

                BitCodePresenter.From(new List<BitCode>() { result }).Print(BitCodePresenter.LinesDisposition.Row, "Decoded matrix");
            });

            return result;
        }
    }
}
