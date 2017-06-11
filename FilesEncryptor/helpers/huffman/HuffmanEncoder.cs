using FilesEncryptor.dto;
using FilesEncryptor.dto.huffman;
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
                    //Si es el caracter de BOM, lo ignoro                   
                    if (c == BOM)
                        continue;
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
                
                //Determino cada cuantas palabras se mostrará el progresso por consola
                int wordsDebugStep = (int)Math.Max(0.1 * _baseText.Length, 1000);

                foreach (char c in _baseText)
                {
                    //Obtengo el codigo Huffman para el caracter
                    fullCode.Append(GetCode(c));
                    counter++;

                    if(counter % wordsDebugStep == 0)
                    {
                        DebugUtils.WriteLine(string.Format("Encoded {0} chars of {1}", counter, _baseText.Length), "[PROGRESS]");
                    }
                }

                encoded = new HuffmanEncodeResult(fullCode, CharsCodes);
            }
            catch (Exception ex)
            {
                DebugUtils.Fail(string.Format("Exception encoding file with huffman, counter = {0}", counter), ex.Message);
            }

            return encoded;
        }

        public static bool WriteToFile(FileHelper fileHelper, HuffmanEncodeResult encodeResult, Encoding baseFileEncoding, byte[] baseFileBOM = null)
        {
            bool writeResult = false;

            //Escribo el BOM del archivo original
            if (baseFileBOM != null)
            {
                DebugUtils.WriteLine("Dumping original file BOM to file");
                writeResult = fileHelper.WriteString(string.Format("{0}:", baseFileBOM.Length));
                writeResult = fileHelper.WriteBytes(baseFileBOM);
            }
            else
            {                
                DebugUtils.WriteLine("No original file BOM was provided", "[WARN]");
                writeResult = fileHelper.WriteString(string.Format("{0}:", 0));
            }

            //Escribo la codificacion usada para leer el archivo original
            DebugUtils.WriteLine("Dumping original file encoding to file");
            writeResult = fileHelper.WriteString(string.Format("{0}:{1}", baseFileEncoding.CodePage.ToString().Length, baseFileEncoding.CodePage));

            //Escribo la tabla de probabilidades
            DebugUtils.WriteLine("Dumping probabilities table to file");

            foreach (var element in encodeResult.ProbabilitiesTable)
            {
                //Escribo la clave como bytes, dado que hay caracteres que ocupan mas de 1 byte
                //Luego escribo todos los bytes del codigo asociado a la clave
                byte[] keyBytes = baseFileEncoding.GetBytes(element.Key.ToString());
                writeResult = fileHelper.WriteString(string.Format("{0},{1}:", keyBytes.Length, element.Value.CodeLength));
                writeResult = fileHelper.WriteBytes(keyBytes);
                writeResult = fileHelper.WriteBytes(element.Value.Code.ToArray());                
            }

            //Escribo el texto comprimido
            DebugUtils.WriteLine("Dumping compressed bytes to file");
            writeResult = fileHelper.WriteString(string.Format(".{0}:", encodeResult.Encoded.CodeLength));
            writeResult = fileHelper.WriteBytes(encodeResult.Encoded.Code.ToArray());

            return writeResult;
        } 
    }
}
