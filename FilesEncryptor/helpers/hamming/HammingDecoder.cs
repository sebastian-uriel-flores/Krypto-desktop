using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
using FilesEncryptor.helpers.processes;
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

        public Task<BitCode> Decode(BaseKryptoProcess currentProcess = null)
        {
            return Task.Run(() =>
            {
                BitCode result = BitCode.EMPTY;
                //currentProcess = KryptoProcess.GetCurrent();

                try
                {
                    //Separo el codigo completo en bloques representando a cada palabra del mismo
                    currentProcess?.UpdateStatus("Creating parity matrix", true);

                    List<BitCode> parityControlMatrix = CreateParityControlMatrix(_encodeType);
                    uint encodedWordSize = (uint)parityControlMatrix[0].CodeLength;

                    currentProcess?.UpdateStatus(string.Format("Extracting input words of {0} bits", encodedWordSize), true);

                    //Obtengo todos los bloques de informacion o palabras
                    Tuple<List<BitCode>, int> exploded = _fullCode.Explode(encodedWordSize, false, false, currentProcess);
                    List<BitCode> encodedWords = exploded.Item1;

                    currentProcess?.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Input words extracting process finished with a total of {exploded.Item1.Count} input words",
                        ProgressAdvance = 100,
                        Tag = "[RESULT]"
                    });

                    //Decodifico cada una de las palabras
                    List<BitCode> decodedWords = new List<BitCode>(encodedWords.Count);
                    List<uint> controlBitsIndexes = GetControlBitsIndexes(_encodeType);

                    currentProcess?.UpdateStatus($"Decoding words with {controlBitsIndexes.Count} control bits, in {_encodeType.WordBitsSize} bits word output size");

                    //Determino cada cuantas palabras se mostrará el progreso por consola
                    int wordsDebugStep = (int)Math.Min(0.1 * encodedWords.Count, 1000);

                    foreach (BitCode encoded in encodedWords)
                    {
                        BitCode decoded = encoded.Copy();

                        //Chequeo si algún bit de la palabra actual es erróneo
                        int errorPosition = CheckParity(parityControlMatrix, decoded);

                        //Si hay un error
                        if (errorPosition >= 0)
                        {
                            //Fixeo el error en el bit correspondiente
                            BitCode erroneousBit = decoded.ElementAt((uint)errorPosition);
                            decoded = decoded.ReplaceAt((uint)errorPosition, erroneousBit.Negate());
                        }

                        uint currentExp = 0;
                        foreach (uint index in controlBitsIndexes)
                        {
                            decoded = decoded.ReplaceAt(index - currentExp, BitCode.EMPTY);
                            currentExp++;
                        }

                        decodedWords.Add(decoded);

                        if (decodedWords.Count % wordsDebugStep == 0)
                        {
                            currentProcess?.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = $"Decoded {decodedWords.Count} words of {encodedWords.Count}",
                                ProgressAdvance = decodedWords.Count * 100 / (double)encodedWords.Count,
                                Tag = "[PROGRESS]"
                            });
                        }
                    }

                    currentProcess?.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Decoding process finished with a total of {decodedWords.Count} output words",
                        ProgressAdvance = 100,
                        Tag = "[RESULT]"
                    });

                    BitCodePresenter.From(decodedWords).Print(BitCodePresenter.LinesDisposition.Row, "Decoded matrix");

                    //Junto todas las palabras decodificadas en un solo codigo
                    currentProcess?.UpdateStatus("Joining encoded words into one array of bytes", true);
                    result = BitOps.Join(decodedWords, currentProcess);

                    currentProcess?.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Joining process finished in a decoded file of {result.CodeLength} bits size",
                        ProgressAdvance = 100,
                        Tag = "[RESULT]"
                    });

                    //Remuevo los bits de redundancia
                    result = result.GetRange(0, (uint)result.CodeLength - _redundanceBitsCount);

                    currentProcess?.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"{_redundanceBitsCount} Redundance bits removed, generating a decoded file of {result.CodeLength} bits size",
                        ProgressAdvance = 100,
                        Tag = "[RESULT]"
                    });

                    BitCodePresenter.From(new List<BitCode>() { result }).Print(BitCodePresenter.LinesDisposition.Row, "Decoded matrix");
                }
                catch (Exception ex)
                {
                    result = null;
                    currentProcess?.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Decoding process failed because of {ex.Message}",
                        ProgressAdvance = 100,
                        Tag = "[RESULT]"
                    });
                }
                return result;
            });
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
