using FilesEncryptor.dto;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers
{
    public class HuffmanEncoder
    {
        private ProbabilitiesScanner _probScanner;
        
        public async Task<HuffmanEncodeResult> Encode(string text)
        {
            _probScanner = await ProbabilitiesScanner.FromText(text);// new ProbabilitiesScanner(text);
            //await _probScanner.ScanProbabilities();
            
            EncodedString fullCode = null;
            if(text != null)
            {
                int counter = 0;
                foreach(char c in text)
                {
                    counter++;
                    try
                    {
                        //Obtengo el codigo Huffman para el caracter
                        EncodedString code = _probScanner.GetCode(c);

                        if (fullCode == null)
                        {
                            fullCode = code;
                        }
                        else
                        {
                            fullCode.Append(code);                         
                        }
                    }
                    catch(Exception ex)
                    {

                    }
                }
            }

            return new HuffmanEncodeResult(fullCode, _probScanner.EncodedProbabilitiesTable);
        }        

        public string Decode(ProbabilitiesScanner scanner, EncodedString encodedText)
        {
            string result = "";
            EncodedString remainingEncodedText = encodedText.Copy();
            _probScanner = scanner;

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
                        if (_probScanner.ContainsChar(currentCode))
                        {
                            //Lo decodifico y agrego al string decodificado
                            result += _probScanner.GetChar(currentCode);

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
