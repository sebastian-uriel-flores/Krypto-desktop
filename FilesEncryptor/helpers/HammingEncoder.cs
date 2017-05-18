using FilesEncryptor.dto;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;

namespace FilesEncryptor.helpers
{
    public class HammingEncoder : BaseCodifier
    {
        private static List<HammingEncodeType> _encodeTypes => new List<HammingEncodeType>
        {
            new HammingEncodeType("16 bits .HA0", "Archivo codificado en Hamming de 16 bits", ".ha0", 16),
            new HammingEncodeType("64 bits .HA1", "Archivo codificado en Hamming de 64 bits", ".ha1", 64),
            new HammingEncodeType("256 bits .HA2", "Archivo codificado en Hamming de 256 bits", ".ha2", 256),
            new HammingEncodeType("1024 bits .HA3", "Archivo codificado en Hamming de 1024 bits", ".ha3", 1024),
            new HammingEncodeType("4096 bits .HA4", "Archivo codificado en Hamming de 4096 bits", ".ha4", 4096)
        };

        public static ReadOnlyCollection<HammingEncodeType> EncodeTypes => _encodeTypes.AsReadOnly();

        private HammingEncodeType _encodeType;
        private BitCode _fullCode;
        private uint _redundanceCodeLength;

        public static uint CalculateControlBits(HammingEncodeType encodeType)
        {
            uint cantControlBits = 1;

            while (Math.Pow(2, cantControlBits) < cantControlBits + encodeType.WordBitsSize + 1)
            {
                cantControlBits++;
            }

            return cantControlBits;
        }

        public static List<BitCode> CreateGeneratorMatrix(HammingEncodeType encodeType)
        {
            uint cantControlBits = CalculateControlBits(encodeType);

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
                List<BitCode> bits = currentBitCode.Explode(1, false).Item1;
                bits[(int)Math.Pow(2, i) - 1] = BitCode.ZERO;
                genMatrix.Add(BitOps.Join(bits));
            }

            //Imprimo la matriz generadora
            BitCodePresenter.From(genMatrix).Print(BitCodePresenter.LinesDisposition.Column, "Generator Matrix");

            return genMatrix;
        }

        public static List<BitCode> CreateParityControlMatrix(HammingEncodeType encodeType)
        {
            uint controlBitsCount = CalculateControlBits(encodeType);

            //Determino el tamaño de los bloques de salida
            //sumando la cantidad de bits de la palabra de entrada y la cantidad de columnas
            //de la matriz generadora
            uint outWordSize = encodeType.WordBitsSize + controlBitsCount;

            //La matriz de control de paridad tendrá tantas columnas como bits de control sean necesarios
            List<BitCode> parContMatrix = new List<BitCode>((int)controlBitsCount);

            //Completo la matriz de control de paridad
            for (uint columnIndex = 0; columnIndex < controlBitsCount; columnIndex++)
            {
                //Cada columna poseera un total de bits igual al tamaño de palabra de salida
                BitCode currentColumn = BitCode.EMPTY;

                //Recorro del final hacia el principio bit a bit 
                //y reemplazo con 1 en las posiciones correspondientes
                uint bitsStep = (uint)Math.Pow(2, columnIndex);
                bool insertOnes = true;

                for(uint i = 0; i < outWordSize; i++) 
                {
                    //Hago esto para pushear en lugar de appendear
                    BitCode tempCode = insertOnes ? BitCode.ONE : BitCode.ZERO;

                    tempCode.Append(currentColumn);
                    currentColumn = tempCode;

                    //Reviso el paso para ver si debo cambiar de insertar ceros a unos o de unos a ceros,
                    //o no debo cambiar
                    if ((i + 1) % bitsStep == 0)
                    {
                        insertOnes = !insertOnes;
                    }
                }

                //Junto los bits ya modificados en un nuevo bitcode y lo agrego a la lista de columnas
                parContMatrix.Add(currentColumn);
            }

            BitCodePresenter.From(parContMatrix).Print(BitCodePresenter.LinesDisposition.Column, "Parity control matrix");

            return parContMatrix;
        }

        public static async Task<HammingEncodeResult> Encode(List<byte> rawBytes, HammingEncodeType encodeType)
        {
            HammingEncodeResult result = null;

            await Task.Factory.StartNew(() =>
            {
                if (encodeType?.WordBitsSize > 0)
                {
                    DebugUtils.WriteLine("Extracting input words");

                    //Obtengo todos los bloques de informacion o palabras
                    Tuple<List<BitCode>, int> exploded = new BitCode(rawBytes, rawBytes.Count * 8).Explode(encodeType.WordBitsSize);
                    List<BitCode> dataBlocks = exploded.Item1;
                    
                    //Imprimo todas las palabras de entrada
                    BitCodePresenter.From(dataBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Input Words");

                    //Creo la matriz generadora
                    DebugUtils.WriteLine("Creating generator matrix");

                    List<BitCode> genMatrix = CreateGeneratorMatrix(encodeType);

                    //Determino el tamaño de los bloques de salida
                    //sumando la cantidad de bits de la palabra de entrada y la cantidad de columnas
                    //de la matriz generadora
                    uint outWordSize = encodeType.WordBitsSize + (uint)genMatrix.Count;

                    DebugUtils.WriteLine("Codifying words");

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
                                currentOutputWord.Append(BitOps.Xor(BitOps.And(new List<BitCode>() { currentWord, genMatrix[currentExp] }).Explode(1, false).Item1));
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

                    //Imprimo todas las palabras de salida
                    BitCodePresenter.From(outputBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Output Words");

                    result = new HammingEncodeResult(BitOps.Join(outputBlocks), encodeType, exploded.Item2);
                }                
            });

            return result;
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

                BitCodePresenter.From(encodedWords).Print(BitCodePresenter.LinesDisposition.Row, "Encoded matrix");

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
                result = result.GetRange(0, (uint)result.CodeLength - _redundanceCodeLength);
            });
            
            return result;
        }

        private static List<uint> GetControlBitsIndexes(HammingEncodeType encodeType)
        {            
            uint ctrlBits = CalculateControlBits(encodeType);
            List<uint> result = new List<uint>((int)ctrlBits);

            for (uint i = 0; i < ctrlBits; i++)
            {
                result.Add((uint)Math.Pow(2, i) - 1);
            }

            return result;
        }

        private static List<uint> GetDataBitsIndexes(uint wordSize, List<uint> controlBits)
        {
            List<uint> dataBits = new List<uint>();
            for(uint i = 0; i < wordSize; i++)
            {
                dataBits.Add(i);
            }

            dataBits = dataBits.Except(controlBits).ToList();

            return dataBits;
        }




        private HammingEncoder()
        {

        }
        
        public HammingEncoder(HammingEncodeType encodeType)
        {
            _encodeType = encodeType;
        }

        public HammingEncoder (HammingEncodeResult encodeResult)
        {
            _encodeType = encodeResult.EncodeType;
            _fullCode = encodeResult.Encoded;
            _redundanceCodeLength = (uint)encodeResult.RedundanceBitsCount;
        }

        public override async Task<bool> ReadFileContent(FilesHelper filesHelper)
        {
            bool result = false;

            //Obtengo la cantidad de bits del codigo completo, incluyendo la redundancia
            string fullCodeLength = await filesHelper.ReadStringUntil(",");

            //Obtengo la cantidad de bits de redundancia ubicados al final del código
            string redundanceCodeLength = await filesHelper.ReadStringUntil(":");

            //Obtengo los bytes del codigo, incluyendo la redundancia
            byte[] fullCodeBytes = await filesHelper.ReadBytes(CommonUtils.BitsLengthToBytesLength(uint.Parse(fullCodeLength)));

            _fullCode = new BitCode(fullCodeBytes.ToList(), int.Parse(fullCodeLength));
            _redundanceCodeLength = uint.Parse(redundanceCodeLength);

            result = true;

            return result;
        }
    }

    
}
