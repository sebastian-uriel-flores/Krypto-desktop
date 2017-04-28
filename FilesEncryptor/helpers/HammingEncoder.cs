using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media;

namespace FilesEncryptor.helpers
{
    public static class HammingEncoder
    {
        public static async Task Encode(EncodedString encodedStr, uint inWordSize)
        {
            if (inWordSize > 0)
            {
                //Obtengo todos los bloques de informacion o palabras
                List<EncodedString> dataBlocks = await encodedStr.GetCodeBlocks(inWordSize);

                //Determino el tamaño de los bloques de salida
                uint outWordSize = 0;
                double cantControlBits = 1;

                while (Math.Pow(2, cantControlBits) < cantControlBits + inWordSize + 1)
                {
                    cantControlBits++;
                }

                outWordSize = inWordSize + (uint)cantControlBits;

                //Creo la matriz Generadora, reducida, sin los bits de informacion
                List<List<EncodedString>> genMatrix = new List<List<EncodedString>>();

                //La cantidad de columnas será la cantidad de bits de control
                for (int i = 0; i < cantControlBits; i++)
                {
                    List<EncodedString> currentList = new List<EncodedString>();

                    for (int j = 0; i < dataBlocks.Count; j++)
                    {
                        if (j == Math.Pow(2, i) - 1)
                        {
                            currentList.Add(EncodedString.ZERO);
                        }
                        else
                        {
                            currentList.Add(EncodedString.ONE);
                        }
                    }

                    //La cantidad de filas esta dada por la cantidad de bits de informacion (dataBlocks.Count)
                    genMatrix.Add(currentList);
                }

                //Creo la lista con los bloques de salida
                List<EncodedString> outputBlocks = new List<EncodedString>((int)outWordSize);

                int currentExp = 0;
                int currentDataBit = 0;

                for(int i = 0; i < outWordSize; i++)
                {
                    //Si es un bit de control, calculo su valor, basandome en la matriz generadora
                    if(i + 1 == Math.Pow(2,currentExp))
                    {
                        //Dejar de saltear y empezar a calcular los valores
                        currentExp++;
                    }
                    //Si es un bit de informacion, lo relleno con el siguiente bit de informacion de la palabra
                    else
                    {                        
                        outputBlocks[i] = dataBlocks[currentDataBit];
                        currentDataBit++;
                    }                    
                }                
            }
        }
    }
}
