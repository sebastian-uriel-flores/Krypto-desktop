using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.huffman
{
    public class HuffmanDecoder : BaseHuffmanCodifier
    {
        private BitCode _encoded;

        private HuffmanDecoder() : base()
        {

        }

        public static HuffmanDecoder FromEncoded(BitCode encoded, Dictionary<char,BitCode> probabilitiesTable) 
            => new HuffmanDecoder() { _encoded = encoded, _charsCodes = probabilitiesTable };

        public static HuffmanDecoder FromFile(FileHelper fileReader)
        {
            HuffmanDecoder decoder = new HuffmanDecoder();
            int counter = 0;

            try
            {
                DebugUtils.WriteLine("Reading Probabilities Table");

                //Leo los 2 primeros caracteres del texto correspondiente a la tabla de probabilidades
                string endOfTableReader = fileReader.ReadString(2);

                //Cuando el primer caracter sea un . entonces habre leido toda la tabla de probabilidades
                while (endOfTableReader != "..")
                {
                    //Obtengo el caracter del siguiente codigo de la tabla
                    char currentChar = endOfTableReader.First();

                    //Obtengo la longitud en bits del codigo indicado
                    uint currentCodeLength = uint.Parse(endOfTableReader.Last() + fileReader.ReadStringUntil(":"));

                    //Convierto la longitud del codigo de bits a bytes y luego, leo el codigo
                    byte[] currentCodeBytes = fileReader.ReadBytes(BitCode.BitsLengthToBytesLength(currentCodeLength));

                    //Agrego el codigo leido a la tabla de probabilidades del decodificador
                    decoder._charsCodes.Add(currentChar, new BitCode(currentCodeBytes.ToList(), (int)currentCodeLength));

                    //Leo los 2 ultimos caracteres para verificar si llegue o no al final de la tabla de probabilidades
                    endOfTableReader = fileReader.ReadString(2);

                    counter++;
                }

                DebugUtils.WriteLine("Reading Encoded bytes");

                //Obtengo la longitud en bits del texto codificado
                uint encodedTextLength = uint.Parse(fileReader.ReadStringUntil(":"));
                DebugUtils.WriteLine(string.Format("Encoded file length is {0} bits ({1} bytes)", 
                    encodedTextLength, 
                    BitCode.BitsLengthToBytesLength(encodedTextLength)));

                //Leo el texto codificado
                byte[] encodedTextBytes = fileReader.ReadBytes(encodedTextLength);
                DebugUtils.WriteLine(string.Format("Encoded text bytes read: {0}", encodedTextBytes.Length));

                //Convierto a BitCode el texto codificado
                decoder._encoded = new BitCode(new List<byte>(encodedTextBytes), (int)encodedTextLength);
            }
            catch(Exception ex)
            {
                decoder = null;
                DebugUtils.Fail(string.Format("Exception loading huffman encoded file. Counter = {0}", counter), ex.Message);
            }

            return decoder;
        }

        public string Decode()
        {
            string result = "";

            try
            {
                BitCode remainingEncodedText = _encoded.Copy();

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
                            BitCode currentCode = new BitCode(currentCodeBytes, currentCodeLength);

                            //Si el codigo formado al realizar los 'i' desplazamientos es un codigo valido
                            if (ContainsCode(currentCode))
                            {
                                //Lo decodifico y agrego al string decodificado
                                result += GetChar(currentCode);

                                //Ahora, desplazo el codigo original hacia la izquierda, tantos bits como sea necesario,
                                //para eliminar el codigo que acabo de agregar y continuar con el siguiente
                                remainingEncodedText.ReplaceCode(
                                    BitCode.LeftShifting(remainingEncodedText.Code, currentCodeLength),
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
                while (currentByteIndex + currentCodeBytes.Count < _encoded.Code.Count && !analyzingTrashBits);
            }
            catch(Exception ex)
            {
                result = null;
                DebugUtils.Fail("Exception decoding huffman file", ex.Message);
            }

            return result;
        }
    }
}
