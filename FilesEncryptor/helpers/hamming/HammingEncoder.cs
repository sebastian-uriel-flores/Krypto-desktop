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
    public class HammingEncoder : BaseHammingCodifier
    {
        private BitCode _baseCode;

        private HammingEncoder()
        {

        }

        public static HammingEncoder From(BitCode baseCode)
        {
            return new HammingEncoder()
            {
                _baseCode = baseCode
            };
        }

        public HammingEncodeResult Encode(HammingEncodeType encodeType)
        {
            HammingEncodeResult result = null;
            KryptoProcess currentProcess = KryptoProcess.GetCurrent();

            try
            {
                if (encodeType.WordBitsSize > 0)
                {
                    //DebugUtils.ConsoleWL(string.Format("Extracting input words of {0} bits", encodeType.WordBitsSize));
                    currentProcess.UpdateStatus(string.Format("Extracting input words of {0} bits", encodeType.WordBitsSize));

                    //Obtengo todos los bloques de informacion o palabras
                    Tuple<List<BitCode>, int> exploded = _baseCode.Explode(encodeType.WordBitsSize, true, false, currentProcess);
                    List<BitCode> dataBlocks = exploded.Item1;

                    currentProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Input words extracting process finished with a total of {exploded.Item1.Count} input words with {exploded.Item2} redundance bits",
                        ProgressAdvance = 100,
                        Tag = "[RESULT]"
                    });

                    //Imprimo todas las palabras de entrada
                    BitCodePresenter.From(dataBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Input Words");

                    //Determino el tamaño de los bloques de salida
                    //sumando la cantidad de bits de la palabra de entrada y la cantidad de bits de control
                    uint outWordSize = encodeType.WordBitsSize + CalculateControlBits(encodeType);

                    currentProcess.UpdateStatus($"Encoding {exploded.Item1.Count} words in {outWordSize} bits output size with {exploded.Item2} redundance bits", true);

                    //Creo la lista con los bloques de salida
                    List<BitCode> outputBlocks = new List<BitCode>((int)outWordSize * dataBlocks.Count);
                    List<uint> controlBitsIndexes = GetControlBitsIndexes(encodeType);

                    //Determino cada cuantas palabras se mostrará el progresso por consola
                    int wordsDebugStep = (int)Math.Min(0.1 * dataBlocks.Count, 1000);

                    foreach (BitCode currentWord in dataBlocks)
                    {
                        //Codifico la palabra actual y la agrego a la lista de palabras de salida
                        outputBlocks.Add(EncodeWord(currentWord, controlBitsIndexes));

                        if (outputBlocks.Count % wordsDebugStep == 0)
                        {
                            currentProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = $"Encoded {outputBlocks.Count} words of {dataBlocks.Count}",
                                ProgressAdvance = outputBlocks.Count * 100 / (double)dataBlocks.Count,
                                Tag = "[PROGRESS]"
                            });

                            //DebugUtils.ConsoleWL(string.Format("Encoded {0} words of {1}", outputBlocks.Count, dataBlocks.Count), "[PROGRESS]");
                        }
                    }

                    currentProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Encoding process finished with a total of {outputBlocks.Count} output words",
                        ProgressAdvance = 100,
                        Tag = "[RESULT]"
                    });

                    //DebugUtils.ConsoleWL(string.Format("Encoding process finished with a total of {0} output words", outputBlocks.Count));

                    //Imprimo todas las palabras de salida
                    BitCodePresenter.From(outputBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Output Words");

                    currentProcess.UpdateStatus("Joining encoded words into one array of bytes", true);
                    //DebugUtils.ConsoleWL("Joining encoded words into one array of bytes");

                    BitCode resultCode = BitOps.Join(outputBlocks, currentProcess);

                    currentProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Joining process finished in an encoded file of {resultCode.CodeLength} bits size",
                        ProgressAdvance = 100,
                        Tag = "[RESULT]"
                    });

                    BitCodePresenter.From(new List<BitCode>() { resultCode }).Print(BitCodePresenter.LinesDisposition.Row, "Output words");

                    result = new HammingEncodeResult(resultCode,
                        encodeType,
                        new HammingCodeLength()
                        {
                            FullCodeLength = (uint)resultCode.CodeLength,
                            RedundanceCodeLength = (uint)exploded.Item2
                        });
                }
            }
            catch (Exception ex)
            {
                result = null;
                currentProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                {
                    Message = $"Encoding process failed because of {ex.Message}",
                    ProgressAdvance = 100,
                    Tag = "[RESULT]"
                });
            }

            return result;
        }

        protected BitCode EncodeWord(BitCode inputWord, HammingEncodeType encodeType)
        {
            BitCode outputWord = inputWord.Copy();
            List<uint> controlBitsIndexes = GetControlBitsIndexes(encodeType);

            foreach (uint index in controlBitsIndexes)
            {
                outputWord = outputWord.Insert(index, BitCode.ZERO);
            }

            //Calculo los valores que iran en cada bit de control
            foreach (uint index in controlBitsIndexes)
            {
                uint bitsStep = index + 1;
                List<BitCode> protectedBits = new List<BitCode>();
                bool take = true;
                uint count = 0;

                for (uint i = index; i < outputWord.CodeLength; i++)
                {
                    if (take)
                    {
                        protectedBits.Add(outputWord.ElementAt(i));
                    }
                    count++;

                    if (count == bitsStep)
                    {
                        count = 0;
                        take = !take;
                    }
                }


                BitCode parityBit = BitOps.Xor(protectedBits);
                outputWord = outputWord.ReplaceAt(index, parityBit);
            }

            return outputWord;
        }

        protected BitCode EncodeWord(BitCode inputWord, List<uint> controlBitsIndexes)
        {
            BitCode outputWord = inputWord.Copy();
            
            foreach (uint index in controlBitsIndexes)
            {
                outputWord = outputWord.Insert(index, BitCode.ZERO);
            }

            //Calculo los valores que iran en cada bit de control
            foreach (uint index in controlBitsIndexes)
            {
                uint bitsStep = index + 1;
                List<BitCode> protectedBits = new List<BitCode>();
                bool take = true;
                uint count = 0;

                for (uint i = index; i < outputWord.CodeLength; i++)
                {
                    if (take)
                    {
                        protectedBits.Add(outputWord.ElementAt(i));
                    }
                    count++;

                    if (count == bitsStep)
                    {
                        count = 0;
                        take = !take;
                    }
                }


                BitCode parityBit = BitOps.Xor(protectedBits);
                outputWord = outputWord.ReplaceAt(index, parityBit);
            }

            return outputWord;
        }
        
        public static bool WriteEncodedToFile(HammingEncodeResult encodeResult, FileHelper fileHelper)
        {
            bool result = false;

            if (encodeResult != null)
            {
                string codeLength = string.Format("{0},{1}:", encodeResult.Length.FullCodeLength, encodeResult.Length.RedundanceCodeLength);
                result = fileHelper.WriteString(codeLength);
                result = fileHelper.WriteBytes(encodeResult.Encoded.Code.ToArray());
            }

            return result;
        }
    }
}
