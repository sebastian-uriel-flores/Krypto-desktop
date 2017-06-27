using FilesEncryptor.dto;
using FilesEncryptor.dto.huffman;
using FilesEncryptor.helpers.processes;
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

        public Task Scan()
        {
            return Task.Run(() =>
            {
                _charsProbabilities.Clear();
                _charsCodes.Clear();

                //Primero, obtengo las cantidades de cada caracter del texto
                DebugUtils.ConsoleWL("Scanning chars aparitions");
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
                DebugUtils.ConsoleWL("Calculating probabilities");
                foreach (char key in _charsProbabilities.Keys.ToList())
                {
                    _charsProbabilities[key] /= _baseText.Length;
                }

                //A continuacion, ordeno las probabilidades de mayor a menor
                DebugUtils.ConsoleWL("Creating Probabilities table");
                var probabilitiesList = _charsProbabilities.ToList();
                probabilitiesList.Sort((a, b) => a.Value < b.Value
                    ? 1
                    : a.Value > b.Value
                        ? -1
                        : 0);

                _charsCodes = ApplyHuffman(probabilitiesList);
            });
        }

        public Task<HuffmanEncodeResult> Encode(BaseKryptoProcess currentProcess = null)
        {
            return Task.Run(() =>
            {
                HuffmanEncodeResult encoded = null;
                int encodedCharsCount = 0;

                currentProcess?.UpdateStatus("Encoding file with Huffman");

                #region CALCULATE_CODE_PARTS_COUNT

                //Determino en cuantas partes de dividira el archivo codificado para que este pueda ser decodificado con multithreading
                List<uint> codePartsLengths = new List<uint>();

                int codeLengthMark = 50000;
                int codeLengthMarkRate = 5;
                int marks = 1;

                while (_baseText.Length >= codeLengthMark)
                {
                    marks++;
                    codeLengthMark *= codeLengthMarkRate;
                }

                marks = Math.Min(marks, 3);

                int mark = _baseText.Length / marks;
                #endregion

                try
                {
                    BitCode fullCode = BitCode.EMPTY;

                    //Determino cada cuantas palabras se mostrará el progresso por consola
                    int wordsDebugStep = (int)Math.Min(0.1 * _baseText.Length, 1000);

                    foreach (char c in _baseText)
                    {
                        //Obtengo el codigo Huffman para el caracter
                        fullCode.Append(GetCode(c));
                        encodedCharsCount++;

                        #region MARK_CODE_PART_END

                        //Si llegué a una marca en el codigo, significa que hasta aqui se realiza una división del código
                        //Si es la última marca, entonces la salteo, ya que podría agregarme una marca unos bits antes
                        //e ignorar los bits siguientes al decodificar
                        if (encodedCharsCount % mark == 0 && codePartsLengths.Count < marks - 1)
                        {
                            codePartsLengths.Add((uint)fullCode.CodeLength);
                        }

                        #endregion

                        #region PRINT_ENCODING_STATS

                        if (encodedCharsCount % wordsDebugStep == 0)
                        {
                            if (currentProcess != null)
                            {
                                currentProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                                {
                                    Message = $"Encoded {encodedCharsCount} symbols of {_baseText.Length}",
                                    ProgressAdvance = encodedCharsCount * 100 / (double)_baseText.Length,
                                    Tag = "[PROGRESS]"
                                });
                            }
                            else
                            {
                                DebugUtils.ConsoleWL(string.Format("Encoded {0} chars of {1}", encodedCharsCount, _baseText.Length), "[PROGRESS]");
                            }
                        }

                        #endregion
                    }

                    currentProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Huffman encoding process finished with a {fullCode.CodeLength} bits encoded file",
                        ProgressAdvance = 100,
                        Tag="[RESULT]"
                    });

                    #region CALCULATE_CODE_PARTS_SIZE

                    //Agrego la última marca en el código
                    codePartsLengths.Add((uint)fullCode.CodeLength);

                    //Calculo la longitud de cada marca, desde el índice en el que empieza la misma
                    //y no desde el principio del código completo
                    for (int i = codePartsLengths.Count - 1; i > 0; i--)
                    {
                        codePartsLengths[i] -= codePartsLengths[i - 1];
                    }

                    #endregion

                    encoded = new HuffmanEncodeResult(fullCode, CharsCodes, codePartsLengths);
                }
                catch (Exception ex)
                {
                    DebugUtils.ConsoleF(string.Format("Exception encoding file with huffman, counter = {0}", encodedCharsCount), ex.Message);
                }

                return encoded;
            });
        }

        public static bool WriteToFile(FileHelper fileHelper, HuffmanEncodeResult encodeResult, Encoding baseFileEncoding, byte[] baseFileBOM = null)
        {
            bool writeResult = false;

            //Escribo el BOM del archivo original
            if (baseFileBOM != null)
            {
                DebugUtils.ConsoleWL("Dumping original file BOM to file");
                writeResult = fileHelper.WriteString(string.Format("{0}:", baseFileBOM.Length));
                writeResult = fileHelper.WriteBytes(baseFileBOM);
            }
            else
            {                
                DebugUtils.ConsoleWL("No original file BOM was provided", "[WARN]");
                writeResult = fileHelper.WriteString(string.Format("{0}:", 0));
            }

            //Escribo la codificacion usada para leer el archivo original
            DebugUtils.ConsoleWL("Dumping original file encoding to file");
            writeResult = fileHelper.WriteString(string.Format("{0}:{1}", baseFileEncoding.CodePage.ToString().Length, baseFileEncoding.CodePage));

            //Escribo la tabla de probabilidades
            DebugUtils.ConsoleWL("Dumping probabilities table to file");

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
            DebugUtils.ConsoleWL("Dumping compressed bytes to file");

            //Escribo la longitud de la primera parte del archivo codificado
            writeResult = fileHelper.WriteString(string.Format(".{0}", encodeResult.CodePartsLengths[0]));

            //Ahora, escribo la longitud del resto de las N partes del archivo codificado
            //para que al decodificarlo se utilicen N threads
            for(int i = 1; i < encodeResult.CodePartsLengths.Count; i++)
            {
                writeResult = fileHelper.WriteString(string.Format(",{0}", encodeResult.CodePartsLengths[i]));
            }

            writeResult = fileHelper.WriteString(":");            
            writeResult = fileHelper.WriteBytes(encodeResult.Encoded.Code.ToArray());

            return writeResult;
        } 
    }
}
