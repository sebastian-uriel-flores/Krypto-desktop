using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.hamming
{
    public class HammingDecoder : BaseHammingCodifier
    {
        protected uint _redundanceBitsCount;
        protected BitCode _fullCode;
        protected HammingEncodeType _encodeType;
        public EventHandler<double> DecodingProgressChanged;

        public BitCode RawCode => _fullCode.Copy();
        public HammingCodeLength RawCodeLength => new HammingCodeLength() { FullCodeLength = (uint)RawCode.CodeLength, RedundanceCodeLength = _redundanceBitsCount };

        #region BUILDERS

        public HammingDecoder(HammingEncodeType encodeType, BitCode hammingCode, uint redundanceBitsCount)
        {
            _encodeType = encodeType;
            _fullCode = hammingCode;
            _redundanceBitsCount = redundanceBitsCount;
        }

        public HammingDecoder(FileHelper fileHelper, HammingEncodeType encodeType) 
        {
            //Obtengo la cantidad de bits del codigo completo, incluyendo la redundancia
            string fullCodeLength = fileHelper.ReadStringUntil(",");

            //Obtengo la cantidad de bits de redundancia ubicados al final del código
            string redundanceCodeLength = fileHelper.ReadStringUntil(":");

            //Obtengo los bytes del codigo, incluyendo la redundancia
            byte[] fullCodeBytes = fileHelper.ReadBytes(BitCode.BitsLengthToBytesLength(uint.Parse(fullCodeLength)));

            _encodeType = encodeType;
            _fullCode = new BitCode(fullCodeBytes.ToList(), int.Parse(fullCodeLength));
            _redundanceBitsCount = uint.Parse(redundanceCodeLength);
        }

        public HammingDecoder(HammingEncodeResult encodeResult) 
            : this(encodeResult.EncodeType, encodeResult.Encoded, encodeResult.Length.RedundanceCodeLength) { }

        #endregion

        public async Task<BitCode> Decode()
        {
            BitCode result = BitCode.EMPTY;

            try
            {
                await Task.Factory.StartNew(() =>
                {
                    //Separo el codigo completo en bloques representando a cada palabra del mismo
                    DebugUtils.ConsoleWL("Creating parity matrix");

                    List<BitCode> parityControlMatrix = CreateParityControlMatrix(_encodeType);
                    uint encodedWordSize = (uint)parityControlMatrix[0].CodeLength;

                    DebugUtils.ConsoleWL(string.Format("Extracting {0} bits encoded words from input code", encodedWordSize));
                    List<BitCode> encodedWords = _fullCode.Explode(encodedWordSize, false, true).Item1;

                    DebugUtils.ConsoleWL(string.Format("Extracted {0} encoded words", encodedWords.Count));

                    //Decodifico cada una de las palabras
                    DebugUtils.ConsoleWL(string.Format("Decoding words in {0} bits word output size", _encodeType.WordBitsSize));

                    List<BitCode> decodedWords = new List<BitCode>(encodedWords.Count);
                    List<uint> controlBitsIndexes = GetControlBitsIndexes(_encodeType);

                    //Determino cada cuantas palabras se mostrará el progresso por consola
                    int wordsDebugStep = (int)Math.Min(0.1 * encodedWords.Count, 1000);

                    foreach (BitCode encoded in encodedWords)
                    {
                        BitCode decoded = encoded.Copy();

                        //Chequeo si algún bit de la palabra actual es erróneo
                        int errorPosition = CheckParity(parityControlMatrix, decoded);

                        //Si hay un error
                        if(errorPosition >= 0)
                        {
                            //Fixeo el error en el bit correspondiente
                            BitCode erroneousBit = decoded.ElementAt((uint)errorPosition);
                            decoded = decoded.ReplaceAt((uint)errorPosition, erroneousBit.Negate());
                        }

                        uint currentExp = 0;
                        foreach(uint index in controlBitsIndexes)
                        {
                            decoded = decoded.ReplaceAt(index - currentExp, BitCode.EMPTY);
                            currentExp++;
                        }

                        decodedWords.Add(decoded);

                        if (decodedWords.Count % wordsDebugStep == 0)
                        {
                            DebugUtils.ConsoleWL(string.Format("Decoded {0} words of {1}", decodedWords.Count, encodedWords.Count), "[PROGRESS]");
                        }
                    }

                    DebugUtils.ConsoleWL(string.Format("Decoding process finished with a total of {0} output words", decodedWords.Count));

                    BitCodePresenter.From(decodedWords).Print(BitCodePresenter.LinesDisposition.Row, "Decoded matrix");

                    //Junto todas las palabras decodificadas en un solo codigo
                    DebugUtils.ConsoleWL("Joining decoded words into one array of bytes");
                    result = BitOps.Join(decodedWords);

                    //Remuevo los bits de redundancia
                    result = result.GetRange(0, (uint)result.CodeLength - _redundanceBitsCount);

                    BitCodePresenter.From(new List<BitCode>() { result }).Print(BitCodePresenter.LinesDisposition.Row, "Decoded matrix");
                });
            }
            catch (Exception ex)
            {
                result = null;
            }
            return result;
        }

        private int CheckParity(List<BitCode> parityControlMatrix, BitCode codeToCheck)
        {
            BitCode syndrome = BitCode.EMPTY;

            for (int columnIndex = 0; columnIndex < parityControlMatrix.Count; columnIndex++)
            {
                var and = BitOps.And(new List<BitCode>() { codeToCheck, parityControlMatrix[columnIndex] });
                var exploded = and.Explode(1, false).Item1;
                var xor = BitOps.Xor(exploded);
                //xor.Append(syndrome);
                syndrome.Append(xor);
            }
            
            int errorPosition = 0;
            //Ahora, convierto el sindrome a entero para ver si hay errores
            for (int i = 0; i < syndrome.CodeLength; i++)
            {
                errorPosition += (int)Math.Pow(2, i) * syndrome.ElementAt((uint)i).Code.First();
            }
            errorPosition /= (int)Math.Pow(2, syndrome.CodeLength);
            return errorPosition - 1;
        }
    }
}
