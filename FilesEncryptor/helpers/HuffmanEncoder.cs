using FilesEncryptor.dto;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers
{
    public static class HuffmanEncoder
    {
        public async static Task<HuffmanEncodeResult> Encode(ProbabilitiesScanner scanner, string text)
        {
            EncodedString fullCode = null;
                        
            if(text != null)
            {
                await Task.Factory.StartNew(() =>
                {
                    int counter = 0;
                    foreach (char c in text)
                    {
                        counter++;
                        try
                        {
                            //Obtengo el codigo Huffman para el caracter
                            EncodedString code = scanner.GetCode(c);

                            if (fullCode == null)
                            {
                                fullCode = code;
                            }
                            else
                            {
                                fullCode.Append(code);
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                });
            }

            return new HuffmanEncodeResult(fullCode, scanner.CodesTable);
        }        

        public static string Decode(ProbabilitiesScanner scanner, EncodedString encodedText)
        {
            string result = "";
            EncodedString remainingEncodedText = encodedText.Copy();

            List<byte> currentCodeBytes = new List<byte>();
            int currentCodeLength = 0;
            int currentByteIndex = 0; //Arranco analizando el primer byte del codigo completo
            bool analyzingTrashBits = false;
            
            do
            {
                byte currentByte = remainingEncodedText.Code[currentByteIndex];

                //Hago desplazamientos a derecha, yendo desde 7 desplazamientos a 0
                for (int i = 7; i >= 0; i--)
                {
                    //Me quedo con los primeros ´8 - i´ bits de la izquierda
                    byte possibleCode = (byte)((currentByte >> i) << i);
                    int diff = 8 - i;

                    currentCodeBytes.Add(currentByte);
                    currentCodeLength += diff;

                    //Si no estoy agregando bits basura que exceden la longitud del texto codificado
                    if (remainingEncodedText.CodeLength - currentCodeLength >= 0)
                    {
                        EncodedString currentCode = new EncodedString(currentCodeBytes, currentCodeLength);

                        //Si el codigo formado al realizar los 'i' desplazamientos es un codigo valido
                        if (scanner.ContainsChar(currentCode))
                        {
                            //Lo decodifico y agrego al string decodificado
                            result += scanner.GetChar(currentCode);

                            //Ahora, desplazo el codigo original hacia la izquierda, tantos bits como sea necesario,
                            //para eliminar el codigo que acabo de agregar y continuar con el siguiente
                            remainingEncodedText.ReplaceCode(
                                CommonUtils.LeftShifting(remainingEncodedText.Code, currentCodeLength),
                                remainingEncodedText.CodeLength - currentCodeLength);

                            currentCodeBytes = new List<byte>();
                            currentCodeLength = 0;
                            currentByteIndex = 0;
                            break;
                        }
                        //Si los primeros '8 - i' bits del codigo, con i > 0, no representan a ningun caracter, 
                        //remuevo el ultimo codigo agregado a la lista y paso a la siguiente iteracion,
                        //para decrementar i
                        else if (i > 0)
                        {
                            currentCodeBytes.Remove(currentByte);
                            currentCodeLength -= diff;
                        }
                        //Si el byte completo junto con los bytes ya agregados no representa a ningun codigo, entonces paso al siguiente byte.
                        //La idea es realizar el mismo procedimiento, pero esta vez evaluando en todos los bytes ya agregados,
                        //sumando de a 1 bit del byte nuevo.
                        else
                        {
                            currentByteIndex++;
                        }
                    }
                    else
                    {
                        analyzingTrashBits = true;
                        break;
                    }
                }
            }
            while (currentByteIndex + currentCodeBytes.Count < encodedText.Code.Count && !analyzingTrashBits);

            return result;
        }
    }
}
