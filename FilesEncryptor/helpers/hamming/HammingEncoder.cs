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

        public async Task<HammingEncodeResult> Encode(HammingEncodeType encodeType)
        {
            HammingEncodeResult result = null;

            try
            {
                await Task.Factory.StartNew(() =>
                {
                    if (encodeType.WordBitsSize > 0)
                    {
                        DebugUtils.WriteLine(string.Format("Extracting input words of {0} bits", encodeType.WordBitsSize));

                        //Obtengo todos los bloques de informacion o palabras
                        Tuple<List<BitCode>, int> exploded = _baseCode.Explode2(encodeType.WordBitsSize, true, true);
                        List<BitCode> dataBlocks = exploded.Item1;

                        DebugUtils.WriteLine(string.Format("Extracted {0} words with {1} redundance bits", dataBlocks.Count, exploded.Item2));

                        //Imprimo todas las palabras de entrada
                        BitCodePresenter.From(dataBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Input Words");

                        //Creo la matriz generadora
                        DebugUtils.WriteLine("Creating generator matrix");

                        List<BitCode> genMatrix = CreateGeneratorMatrix(encodeType);

                        //Determino el tamaño de los bloques de salida
                        //sumando la cantidad de bits de la palabra de entrada y la cantidad de columnas
                        //de la matriz generadora
                        uint outWordSize = encodeType.WordBitsSize + (uint)genMatrix.Count;

                        DebugUtils.WriteLine(string.Format("Encoding words in {0} bits output size", outWordSize));

                        //Creo la lista con los bloques de salida
                        List<BitCode> outputBlocks = new List<BitCode>((int)outWordSize * dataBlocks.Count);
                        List<uint> controlBitsIndexes = GetControlBitsIndexes(encodeType);

                        //Determino cada cuantas palabras se mostrará el progresso por consola
                        int wordsDebugStep = (int)Math.Min(0.1 * dataBlocks.Count, 1000);

                        foreach (BitCode currentWord in dataBlocks)
                        {
                            int currentExp = 0;
                            int currentDataBit = 0;
                            BitCode currentOutputWord = currentWord.Copy();

                            List<Tuple<int, int>> dataBlocksIndexes = new List<Tuple<int, int>>();
                            List<Tuple<int, int>> controlBlocksIndexes = new List<Tuple<int, int>>();
                           
                            foreach(uint index in controlBitsIndexes)
                            {
                                var code = BitOps.Xor(BitOps.And(new List<BitCode>() { currentWord, genMatrix[currentExp] }).Explode2(1, false).Item1);
                                currentOutputWord = currentOutputWord.Insert2(index, code);
                                currentExp++;
                            }
                            
                            /*for (int i = 0; i < outWordSize; i++)
                            {
                                //Si es un bit de control, calculo su valor, basandome en la matriz generadora
                                if (i + 1 == Math.Pow(2, currentExp))
                                {
                                    controlBlocksIndexes.Add(new Tuple<int, int>(i, currentExp));
                                    //Al realizar un and, estoy haciendo la multiplicacion bit a bit
                                    //Luego, al Código formado por esa multiplicacion, lo divido en subcodigos de 1 bit
                                    //Y realizo la suma entre todos los bits, haciendo un xor entre todos
                                    currentOutputWord.Append(BitOps.Xor(BitOps.And(new List<BitCode>() { currentWord, genMatrix[currentExp] }).Explode(1, false).Item1));
                                    currentExp++;
                                }
                                //Si es un bit de informacion, lo relleno con el siguiente bit de informacion de la palabra
                                else
                                {
                                    dataBlocksIndexes.Add(new Tuple<int, int>(i, currentDataBit));
                                    currentOutputWord.Append(currentWord.ElementAt((uint)currentDataBit));
                                    currentDataBit++;
                                }
                            }*/

                            //Agrego la palabra recién creada a la lista de palabras de salida
                            outputBlocks.Add(currentOutputWord);

                            if (outputBlocks.Count % wordsDebugStep  == 0)
                            {
                                DebugUtils.WriteLine(string.Format("Encoded {0} words of {1}", outputBlocks.Count, dataBlocks.Count), "[PROGRESS]");
                            }
                        }

                        DebugUtils.WriteLine(string.Format("Encoding process finished with a total of {0} output words", outputBlocks.Count));

                        //Imprimo todas las palabras de salida
                        BitCodePresenter.From(outputBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Output Words");

                        DebugUtils.WriteLine("Joining encoded words into one array of bytes");
                        BitCode resultCode = BitOps.Join(outputBlocks);

                        BitCodePresenter.From(new List<BitCode>() { resultCode }).Print(BitCodePresenter.LinesDisposition.Row, "Output words");

                        result = new HammingEncodeResult(resultCode,
                            encodeType,
                            new HammingCodeLength()
                            {
                                FullCodeLength = (uint)resultCode.CodeLength,
                                RedundanceCodeLength = (uint)exploded.Item2
                            });
                    }
                });
            }
            catch (Exception ex)
            {
                result = null;
            }

            return result;
        }

        private async Task<bool> Verify(HammingEncodeResult encodeResult)
        {
            var decoded = await HammingDecoder.FromEncoded(encodeResult).Decode();                        
            return decoded != null && (BitOps.And(BitOps.Xor(new List<BitCode>() { _baseCode, decoded }).Explode(1, false).Item1).Code[0] == 0);
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
