using FilesEncryptor.dto;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media;

namespace FilesEncryptor.helpers
{
    public static class HammingEncoder
    {
        private static List<HammingEncodeType> _encodeTypes => new List<HammingEncodeType>
        {
            new HammingEncodeType("16 bits", ".HA0", 16),
            new HammingEncodeType("64 bits", ".HA1", 64),
            new HammingEncodeType("256 bits", ".HA2", 256),
            new HammingEncodeType("1024 bits", ".HA3", 1024),
            new HammingEncodeType("4096 bits", ".HA4", 4096)
        };

        public static ReadOnlyCollection<HammingEncodeType> EncodeTypes => _encodeTypes.AsReadOnly();

        public static async Task Encode(List<byte> rawBytes, HammingEncodeType encodeType)
        {
            await Task.Factory.StartNew(() =>
            {
                if (encodeType?.WordBitsSize > 0)
                {
                    Debug.WriteLine("Extracting input words", "[INFO]");

                    //Obtengo todos los bloques de informacion o palabras
                    List<BitCode> dataBlocks = new BitCode(rawBytes, rawBytes.Count * 8).Explode(encodeType.WordBitsSize);

                    BitCodePresenter.From(dataBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Input Words");

                    Debug.WriteLine("Creating generator matrix", "[INFO]");

                    //Determino el tamaño de los bloques de salida
                    uint outWordSize = 0;
                    uint cantControlBits = 1;

                    while (Math.Pow(2, cantControlBits) < cantControlBits + encodeType.WordBitsSize + 1)
                    {
                        cantControlBits++;
                    }

                    outWordSize = encodeType.WordBitsSize + cantControlBits;

                    

                    //Creo la matriz Generadora, reducida, sin los bits de información
                    //Será representada por una lista de BitCode, donde cada uno de ellos 
                    //corresponderá a una columna de la matriz
                    //La cantidad de columnas será la cantidad de bits de control
                    List<BitCode> genMatrix = new List<BitCode>((int)cantControlBits);

                    //Por cada columna
                    for (int i = 0; i < cantControlBits; i++)
                    {
                        //Agrego las filas a la columna actual.
                        //La cantidad de filas será igual a la cantidad de bits de la palabra de entrada
                        BitCode currentBitCode = BitCode.Ones(encodeType.WordBitsSize);
                        
                        //Reemplazo con un 0 el valor de la fila correspondiente a la
                        //potencia 'i' de 2
                        List<BitCode> bits = currentBitCode.Explode(1);
                        bits[(int)Math.Pow(2, i) - 1] = BitCode.ZERO;
                        genMatrix.Add(BitOps.Join(bits));
                    }

                    BitCodePresenter.From(genMatrix).Print(BitCodePresenter.LinesDisposition.Column, "Generator Matrix");

                    Debug.WriteLine("Codifying words", "[INFO]");

                    //Creo la lista con los bloques de salida
                    List<BitCode> outputBlocks = new List<BitCode>((int)outWordSize * dataBlocks.Count);

                    foreach (BitCode currentWord in dataBlocks)
                    {
                        int currentExp = 0;
                        int currentDataBit = 0;
                        BitCode currentOutputWord = BitCode.EMPTY;

                        for (int i = 0; i < outWordSize; i++)
                        {
                            //Si es un bit de control, calculo su valor, basandome en la matriz generadora
                            if (i + 1 == Math.Pow(2, currentExp))
                            {
                                //Al realizar un and, estoy haciendo la multiplicacion bit a bit
                                //Luego, al Código formado por esa multiplicacion, lo divido en subcodigos de 1 bit
                                //Y realizo la suma entre todos los bits, haciendo un xor entre todos
                                currentOutputWord.Append(BitOps.Xor(BitOps.And(new List<BitCode>() { currentWord, genMatrix[currentExp] }).Explode(1)));
                                currentExp++;
                            }
                            //Si es un bit de informacion, lo relleno con el siguiente bit de informacion de la palabra
                            else
                            {
                                currentOutputWord.Append(currentWord.ElementAt((uint)currentDataBit));
                                currentDataBit++;
                            }
                        }

                        //Agrego la palabra recién creada a la lista de palabras de salida
                        outputBlocks.Add(currentOutputWord);
                    }

                    BitCodePresenter.From(outputBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Output Words");
                }                
            });
        }
    }
}
