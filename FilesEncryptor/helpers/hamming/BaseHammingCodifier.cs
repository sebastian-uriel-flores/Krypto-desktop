using FilesEncryptor.dto;
using FilesEncryptor.dto.hamming;
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

namespace FilesEncryptor.helpers.hamming
{
    public class BaseHammingCodifier
    {
        #region ENCODE_TYPES
        private static List<HammingEncodeType> _encodeTypes => new List<HammingEncodeType>
        {
            new HammingEncodeType("16 bits .HA0", "Archivo codificado en Hamming de 16 bits", ".ha0", 16),
            new HammingEncodeType("64 bits .HA1", "Archivo codificado en Hamming de 64 bits", ".ha1", 64),
            new HammingEncodeType("256 bits .HA2", "Archivo codificado en Hamming de 256 bits", ".ha2", 256),
            new HammingEncodeType("1024 bits .HA3", "Archivo codificado en Hamming de 1024 bits", ".ha3", 1024),
            new HammingEncodeType("4096 bits .HA4", "Archivo codificado en Hamming de 4096 bits", ".ha4", 4096)
        };

        public static ReadOnlyCollection<HammingEncodeType> EncodeTypes => _encodeTypes.AsReadOnly();

        #endregion

        public uint CalculateControlBits(HammingEncodeType encodeType)
        {
            uint cantControlBits = 1;

            while (Math.Pow(2, cantControlBits) < cantControlBits + encodeType.WordBitsSize + 1)
            {
                cantControlBits++;
            }

            return cantControlBits;
        }

        public List<BitCode> CreateGeneratorMatrix(HammingEncodeType encodeType)
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

        public List<BitCode> CreateParityControlMatrix(HammingEncodeType encodeType)
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
                BitCode currentColumn = BitCode.Zeros(outWordSize);

                uint bitsStep = (uint)Math.Pow(2, columnIndex);

                for (uint rowIndex = bitsStep - 1; rowIndex < outWordSize; rowIndex += 2 * bitsStep)
                {
                    for(uint i=rowIndex; i < rowIndex+bitsStep; i++)
                    {
                        currentColumn = currentColumn.ReplaceAt(i, BitCode.ONE);
                    }
                }

                //Junto los bits ya modificados en un nuevo bitcode y lo agrego a la lista de columnas
                parContMatrix.Add(currentColumn);
            }

            BitCodePresenter.From(parContMatrix).Print(BitCodePresenter.LinesDisposition.Column, "Parity control matrix", 4, true);

            return parContMatrix;
        }

        public List<uint> GetControlBitsIndexes(HammingEncodeType encodeType)
        {            
            uint ctrlBits = CalculateControlBits(encodeType);
            List<uint> result = new List<uint>((int)ctrlBits);

            for (uint i = 0; i < ctrlBits; i++)
            {
                result.Add((uint)Math.Pow(2, i) - 1);
            }

            return result;
        }

        public List<uint> GetDataBitsIndexes(uint wordSize, List<uint> controlBits)
        {
            List<uint> dataBits = new List<uint>();
            for(uint i = 0; i < wordSize; i++)
            {
                dataBits.Add(i);
            }

            dataBits = dataBits.Except(controlBits).ToList();

            return dataBits;
        }
    }
}
