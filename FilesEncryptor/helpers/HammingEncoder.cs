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
    public static class HammingEncoder
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
                List<BitCode> bits = currentBitCode.Explode(1);
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

            return parContMatrix;
        }

        public static async Task<HammingEncodeResult> Encode(List<byte> rawBytes, HammingEncodeType encodeType)
        {
            HammingEncodeResult result = null;

            await Task.Factory.StartNew(() =>
            {
                if (encodeType?.WordBitsSize > 0)
                {
                    Debug.WriteLine("Extracting input words", "[INFO]");

                    //Obtengo todos los bloques de informacion o palabras
                    List<BitCode> dataBlocks = new BitCode(rawBytes, rawBytes.Count * 8).Explode(encodeType.WordBitsSize);

                    //Imprimo todas las palabras de entrada
                    BitCodePresenter.From(dataBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Input Words");

                    //Creo la matriz generadora
                    Debug.WriteLine("Creating generator matrix", "[INFO]");

                    List<BitCode> genMatrix = CreateGeneratorMatrix(encodeType);

                    //Determino el tamaño de los bloques de salida
                    //sumando la cantidad de bits de la palabra de entrada y la cantidad de columnas
                    //de la matriz generadora
                    uint outWordSize = encodeType.WordBitsSize + (uint)genMatrix.Count;

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

                    //Imprimo todas las palabras de salida
                    BitCodePresenter.From(outputBlocks).Print(BitCodePresenter.LinesDisposition.Row, "Output Words");

                    result = new HammingEncodeResult(BitOps.Join(outputBlocks), encodeType);                                        
                }                
            });

            return result;
        }

        public static async Task Decode(StorageFile file)
        {
            //Determino el tipo de codificacion que posee
            HammingEncodeType encodeType = EncodeTypes.FirstOrDefault(encType => encType.Extension == file.FileType);

            if(encodeType == default(HammingEncodeType))
            {
                //No posee una extension valida
            }
            else
            {
                //Extraigo la información del archivo codificado
                string originalFileExtension = "";
                string originalFileDisplayType = "";
                string originalFileName = "";
                BitCode code = BitCode.EMPTY;

                //Abro el archivo para lectura
                using (var stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    using (var inputStream = stream.GetInputStreamAt(0))
                    {
                        using (var dataReader = new DataReader(inputStream))
                        {
                            var size = stream.Size;
                            //Cargo en el buffer todos los bytes del archivo
                            uint numBytesLoaded = await dataReader.LoadAsync((uint)size);

                            string temp = "";

                            //Obtengo el largo del tipo de archivo
                            string fileExtLength = "";

                            temp = dataReader.ReadString(1);

                            while (temp != ":")
                            {
                                fileExtLength += temp;
                                temp = dataReader.ReadString(1);
                            }

                            //Obtengo el tipo de archivo
                            originalFileExtension = dataReader.ReadString(uint.Parse(fileExtLength));

                            //Obtengo el largo de la descripcion del tipo de archivo
                            string fileDisplayTypeLength = "";

                            temp = dataReader.ReadString(1);

                            while (temp != ":")
                            {
                                fileDisplayTypeLength += temp;
                                temp = dataReader.ReadString(1);
                            }

                            //Obtengo la descripcion del tipo de archivo
                            originalFileDisplayType = dataReader.ReadString(uint.Parse(fileDisplayTypeLength));

                            //Obtengo el largo del código
                            string rawCodeLength = "";

                            temp = dataReader.ReadString(1);

                            while (temp != ":")
                            {
                                rawCodeLength += temp;
                                temp = dataReader.ReadString(1);
                            }

                            //Obtengo los bytes del código
                            byte[] rawCode = new byte[CommonUtils.BitsLengthToBytesLength(uint.Parse(rawCodeLength))];
                            dataReader.ReadBytes(rawCode);
                            code = new BitCode(rawCode.ToList(), int.Parse(rawCodeLength));
                        }
                    }
                }

                //WTF do here?
            }
        }
    }
}
