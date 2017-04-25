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
                double exp = 1;

                while (Math.Pow(2, exp) < inWordSize + 1)
                {
                    exp++;
                }

                outWordSize = inWordSize + (uint)exp;

                //Creo la lista con los bloques de salida
                List<EncodedString> outputBlocks = new List<EncodedString>(dataBlocks.Count);
                
                foreach(EncodedString data in dataBlocks)
                {
                    EncodedString encoded = data.Copy();

                    for(int i=0; i < exp; i++)
                    {
                        encoded.Insert((uint)Math.Pow(2, i) - 1, EncodedString.ONE);
                    }
                    
                    //EncodedString encoded = new EncodedString()
                }
            }
        }
    }
}
