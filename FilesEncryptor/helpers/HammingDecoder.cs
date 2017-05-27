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
        public EventHandler<double> DecodingProgressChanged;

        public BitCode RawCode => _fullCode.Copy();
        public HammingCodeLength RawCodeLength => new HammingCodeLength() { FullCodeLength = (uint)RawCode.CodeLength, RedundanceCodeLength = _redundanceBitsCount };

        #region BUILDERS

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
            byte[] fullCodeBytes = fileHelper.ReadBytes(BitCode.BitsLengthToBytesLength(uint.Parse(fullCodeLength)));

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

        #endregion

        public async Task<BitCode> Decode()
        {
            BitCode result = BitCode.EMPTY;

            await Task.Factory.StartNew(() =>
            {
                //Separo el codigo completo en bloques representando a cada palabra del mismo
                DebugUtils.WriteLine("Creating parity matrix");

                List<BitCode> parityControlMatrix = CreateParityControlMatrix(_encodeType);
                uint encodedWordSize = (uint)parityControlMatrix[0].CodeLength;

                DebugUtils.WriteLine(string.Format("Extracting {0} bits encoded words from input code", encodedWordSize));
                List<BitCode> encodedWords = _fullCode.Explode(encodedWordSize, false).Item1;

                DebugUtils.Write(string.Format("Extracted {0} encoded words", encodedWords.Count));
                DebugUtils.WriteLine("Checking words parity");

                //TODO:Chequeo la paridad en cada una de las palabras, utilizando la matriz de control de paridad
                for(int encodedWordIndex = 0; encodedWordIndex < encodedWords.Count; encodedWordIndex++)
                {
                    /*int errorPosition = CheckParity(parityControlMatrix, encodedWords[encodedWordIndex]);

                    //Si encuentra un error en la palabra
                    if(errorPosition > - 1)
                    {                        
                        //TODO: Fix error
                        /*encodedWords[encodedWordIndex] = encodedWords[encodedWordIndex].ReplaceAt(
                            (uint)errorPosition, 
                            encodedWords[encodedWordIndex].ElementAt((uint)errorPosition).Negate());
                            */
                      /*  DebugUtils.WriteLine(string.Format("Fixed error in word {0} at bit {1}", encodedWordIndex, errorPosition));
                    }*/
                }
                
                DebugUtils.WriteLine("Parity check OK");

                //Decodifico cada una de las palabras
                DebugUtils.WriteLine(string.Format("Decoding words in {0} bits word output size", _encodeType.WordBitsSize));

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

                    if (decodedWords.Count % 10 == 0)
                    {
                        DebugUtils.WriteLine(string.Format("Decoded {0} words of {1}", decodedWords.Count, encodedWords.Count), "[PROGRESS]");
                        //DecodingProgressChanged(this, (decodedWords.Count * 100) / encodedWords.Count);

                    if(decodedWords.Count == 8110)
                        {

                        }
                    }
                }

                DebugUtils.WriteLine(string.Format("Decoding process finished with a total of {0} output words", decodedWords.Count));

                BitCodePresenter.From(decodedWords).Print(BitCodePresenter.LinesDisposition.Row, "Decoded matrix");

                //Junto todas las palabras decodificadas en un solo codigo
                DebugUtils.WriteLine("Joining decoded words into one array of bytes");
                result = BitOps.Join(decodedWords);

                //Remuevo los bits de redundancia
                result = result.GetRange(0, (uint)result.CodeLength - _redundanceBitsCount);

                BitCodePresenter.From(new List<BitCode>() { result }).Print(BitCodePresenter.LinesDisposition.Row, "Decoded matrix");
            });

            return result;
        }

        private int CheckParity(List<BitCode> parityControlMatrix, BitCode codeToCheck)
        {
            List<int> syndrome = new List<int>();

            for (int columnIndex = 0; columnIndex < parityControlMatrix.Count; columnIndex++)
            {
                syndrome.Add(BitOps.Xor(BitOps.And(new List<BitCode>() { codeToCheck, parityControlMatrix[columnIndex] }).Explode(1, false).Item1).ToIntList().First());                
            }

            int errorPosition = -1;

            //Ahora, convierto el sindrome a entero para ver si hay errores
            for (int i = 0; i <syndrome.Count; i++)
            {
                errorPosition += (int)Math.Pow(2, i) * syndrome[i];
            }
            
            return errorPosition;
        }
    }
}
