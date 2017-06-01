using FilesEncryptor.dto;
using FilesEncryptor.dto.Huffman;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.huffman
{
    public class HuffmanEncoder : BaseHuffmanCodifier
    {
        private string _baseText;
        private Dictionary<char, float> _charsProbabilities;

        public ReadOnlyDictionary<char, BitCode> CharsCodes => new ReadOnlyDictionary<char, BitCode>(_charsCodes);

        private HuffmanEncoder() : base()
        {
            _charsProbabilities = new Dictionary<char, float>();
        }

        public static HuffmanEncoder From(string text)
        {
            return new HuffmanEncoder() { _baseText = text };
        }

        public void Scan()
        {
            _charsProbabilities.Clear();
            _charsCodes.Clear();

            //Primero, obtengo las cantidades de cada caracter del texto
            DebugUtils.WriteLine("Scanning chars aparitions");

            foreach (char c in _baseText)
            {
                if (_charsProbabilities.ContainsKey(c))
                {
                    _charsProbabilities[c]++;
                }
                else
                {
                    _charsProbabilities.Add(c, 1);
                }
            }

            //Ahora que tengo las cantidades, calculo las probabilidades
            DebugUtils.WriteLine("Calculating probabilities");
            foreach (char key in _charsProbabilities.Keys.ToList())
            {
                _charsProbabilities[key] /= _baseText.Length;
            }

            //A continuacion, ordeno las probabilidades de mayor a menor
            DebugUtils.WriteLine("Creating Probabilities table");
            var probabilitiesList = _charsProbabilities.ToList();
            probabilitiesList.Sort((a, b) => a.Value < b.Value
                ? 1
                : a.Value > b.Value
                    ? -1
                    : 0);

            _charsCodes = ApplyHuffman(probabilitiesList);
        }

        public HuffmanEncodeResult Encode()
        {            
            HuffmanEncodeResult encoded = null;
            int counter = 0;

            try
            {
                BitCode fullCode = BitCode.EMPTY;

                foreach (char c in _baseText)
                {
                    //Obtengo el codigo Huffman para el caracter
                    fullCode.Append(GetCode(c));
                    counter++;
                }

                encoded = new HuffmanEncodeResult(fullCode, CharsCodes);
            }
            catch (Exception ex)
            {
                DebugUtils.Fail(string.Format("Exception encoding file with huffman, counter = {0}", counter), ex.Message);
            }

            return encoded;
        }

        public static bool WriteToFile(FileHelper fileHelper, HuffmanEncodeResult encodeResult)
        {
            bool writeResult = false;

            DebugUtils.WriteLine("Dumping probabilities table to file");

            foreach (var element in encodeResult.ProbabilitiesTable)
            {
                writeResult = fileHelper.WriteString(string.Format("{0}{1}:", element.Key, element.Value.CodeLength));
                writeResult = fileHelper.WriteBytes(element.Value.Code.ToArray());
            }

            //Escribo el texto comprimido
            DebugUtils.WriteLine("Dumping compressed bytes to file");
            writeResult = fileHelper.WriteString(string.Format("..{0}:", encodeResult.Encoded.CodeLength));
            writeResult = fileHelper.WriteBytes(encodeResult.Encoded.Code.ToArray());

            return writeResult;
        } 

        public static string Decode(ProbabilitiesScanner scanner, BitCode encodedText)
        {
            string result = "";
            BitCode remainingEncodedText = encodedText.Copy();

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
                        if (scanner.ContainsChar(currentCode))
                        {
                            //Lo decodifico y agrego al string decodificado
                            result += scanner.GetChar(currentCode);

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
            while (currentByteIndex + currentCodeBytes.Count < encodedText.Code.Count && !analyzingTrashBits);

            return result;
        }
    }
}
