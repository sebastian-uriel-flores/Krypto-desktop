using FilesEncryptor.dto;
using FilesEncryptor.helpers.processes;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.huffman
{
    public class HuffmanDecoder : BaseHuffmanCodifier
    {
        #region VARIABLES

        private BitCode _encoded;
        private byte[] _fileBOM;
        private string _fileBOMString;
        private List<uint> _encodedPartsLengths;
        private Dictionary<int, uint> _tasksProgress;

        private BinaryTree<string> _codesTree;

        #endregion

        #region PROPERTIES

        public byte[] FileBOM => _fileBOM;

        public string FileBOMString => _fileBOMString;

        public BinaryTree<string> CodesTree => _codesTree;

        #endregion 

        #region BUILDERS

        private HuffmanDecoder() : base()
        {
            _codesTree = BinaryTree<string>.EMPTY;
        }

        public static HuffmanDecoder FromEncoded(BitCode encoded, Dictionary<char,BitCode> probabilitiesTable, byte[] bom = null) 
            => new HuffmanDecoder()
            {
                _encoded = encoded,
                _charsCodes = probabilitiesTable,
                _fileBOM = bom,
                _fileBOMString = bom?.Length > 0 ? FileHelper.GetEncoding(bom).GetString(bom) : null
            };

        public static Task<HuffmanDecoder> FromFile(FileHelper fileReader)
        {
            return Task.Run(() =>
            {
                HuffmanDecoder decoder = new HuffmanDecoder();
                int counter = 0;

                try
                {
                    /**- El formato del encabezado de un archivo Huffman es:
                     *   header = [encodingStringLen]:[encodingCodePage][bomBytesLen]:[bomBytes]
                     * - El formato de la tabla de probabilidades es
                     *   probTable = ([char][charBitsLen]:[charCode])+
                     * - El formato del codigo es:
                     *   code = [codeBitsLen][,codeBitsLen]+:[code]
                     *   
                     * - El formato completo es:
                     *   [header][probTable].[code] 
                     */

                    #region HUFFMAN_HEADER

                    //Obtengo el BOM del archivo original
                    DebugUtils.ConsoleWL("Searching for original file BOM");
                    uint bomLen = uint.Parse(fileReader.ReadStringUntil(":"));

                    if (bomLen > 0)
                    {
                        //Obtengo el BOM del texto original
                        byte[] textBom = fileReader.ReadBytes(bomLen);

                        decoder._fileBOM = textBom;
                        decoder._fileBOMString = FileHelper.GetEncoding(textBom).GetString(textBom);

                        DebugUtils.ConsoleWL(string.Format("Original file encoding is {0}", fileReader.FileEncoding.EncodingName));
                    }
                    else
                    {
                        DebugUtils.ConsoleWL("No original file BOM was provided", "[WARN]");
                    }

                    //Obtengo el Encoding usado para leer el archivo original
                    DebugUtils.ConsoleWL("Reading original file Encoding");

                    uint encodingStrLen = uint.Parse(fileReader.ReadStringUntil(":"));
                    int fileEncodingCodePage = int.Parse(fileReader.ReadString(encodingStrLen));

                    Encoding fileEncoding = Encoding.GetEncoding(fileEncodingCodePage);

                    //Seteo el Encoding en el fileReader, para poder leer los caracteres de la tabla de probabilidades
                    fileReader.SetFileEncoding(fileEncoding);

                    #endregion

                    #region HUFFMAN_PROBABILITIES_TABLE

                    DebugUtils.ConsoleWL("Reading Probabilities Table");


                    string endOfTable = fileReader.ReadString(1);

                    while (endOfTable != ".")
                    {
                        //Leo la longitud en bytes de la clave y la longitud en bits del codigo del primer caracter de la tabla
                        uint keyLen = uint.Parse(endOfTable + fileReader.ReadStringUntil(","));
                        uint codeLen = uint.Parse(fileReader.ReadStringUntil(":"));
                        uint codeBytesLen = BitCode.BitsLengthToBytesLength(codeLen);

                        //Leo el caracter clave y el codigo
                        byte[] keyBytes = fileReader.ReadBytes(keyLen);
                        char key = fileEncoding.GetString(keyBytes).First();
                        byte[] codeBytes = fileReader.ReadBytes(codeBytesLen);

                        //Agrego el par a la tabla
                        decoder._charsCodes.Add(key, new BitCode(codeBytes.ToList(), (int)codeLen));
                        decoder._codesTree.Add(new BitCode(codeBytes.ToList(), (int)codeLen), key.ToString());

                        endOfTable = fileReader.ReadString(1);
                        counter++;
                    }

                    ////Leo los 2 primeros caracteres del texto correspondiente a la tabla de probabilidades
                    //string endOfTableReader = fileReader.ReadString(2);

                    ////Cuando el primer caracter sea un . entonces habre leido toda la tabla de probabilidades
                    //while (endOfTableReader != "..")
                    //{
                    //    //Obtengo el caracter del siguiente codigo de la tabla
                    //    char currentChar = endOfTableReader.First();

                    //    //Obtengo la longitud en bits del codigo indicado
                    //    uint.TryParse(endOfTableReader.Last().ToString(), out uint keyLen);
                    //    uint currentCodeLength = uint.Parse(keyLen + fileReader.ReadStringUntil(":"));

                    //    //Convierto la longitud del codigo de bits a bytes y luego, leo el codigo
                    //    byte[] currentCodeBytes = fileReader.ReadBytes(BitCode.BitsLengthToBytesLength(currentCodeLength));

                    //    //Agrego el codigo leido a la tabla de probabilidades del decodificador
                    //    decoder._charsCodes.Add(currentChar, new BitCode(currentCodeBytes.ToList(), (int)currentCodeLength));

                    //    //Leo los 2 ultimos caracteres para verificar si llegue o no al final de la tabla de probabilidades
                    //    endOfTableReader = fileReader.ReadString(2);

                    //    counter++;
                    //}

                    #endregion

                    #region HUFFMAN_CODE

                    DebugUtils.ConsoleWL("Reading Encoded bytes");

                    //Obtengo el fragmento del header que indica la longitud del archivo codificado
                    string rawCodeLen = fileReader.ReadStringUntil(":");

                    //Creo una lista con las longitudes de cada fragmento del archivo codificado
                    //y calculo la longitud total del mismo
                    string[] codePartsLenghts = rawCodeLen.Split(',');

                    decoder._encodedPartsLengths = new List<uint>();
                    uint encodedTextLength = 0;

                    foreach (string codePartLenStr in codePartsLenghts)
                    {
                        uint codePartLen = uint.Parse(codePartLenStr);
                        decoder._encodedPartsLengths.Add(codePartLen);
                        encodedTextLength += codePartLen;
                    }

                    //Calculo la longitud en bytes de todo el archivo codificado
                    uint bytesLength = BitCode.BitsLengthToBytesLength(encodedTextLength);

                    DebugUtils.ConsoleWL(string.Format("Encoded file length is {0} bits ({1} bytes)",
                        encodedTextLength,
                        bytesLength));

                    //Leo el texto codificado
                    byte[] encodedTextBytes = fileReader.ReadBytes(bytesLength);
                    DebugUtils.ConsoleWL(string.Format("Encoded text bytes read: {0}", encodedTextBytes.Length));

                    #endregion

                    //Convierto a BitCode el texto codificado
                    decoder._encoded = new BitCode(new List<byte>(encodedTextBytes), (int)encodedTextLength);
                }
                catch (Exception ex)
                {
                    decoder = null;
                    DebugUtils.ConsoleF(string.Format("Exception loading huffman encoded file. Counter = {0}", counter), ex.Message);
                }

                return decoder;
            });
        }

        #endregion

        #region INSTANCE_METHODS

        public async Task<string> DecodeWithBruteForce()
        {
            //Si poseo un BOM, lo incluyo al principio del texto decodificado.
            string result = _fileBOMString ?? "";

            await Task.Factory.StartNew(() =>
            {
                try
                {
                    BitCode remainingEncodedText = _encoded.Copy();

                    List<byte> currentCodeBytes = new List<byte>();
                    int currentCodeLength = 0;
                    int currentByteIndex = 0; //Arranco analizando el primer byte del codigo completo
                    bool analyzingTrashBits = false;

                    //Esta variable la uso para ir contando la cantidad del código completo que ha sido decodificada,
                    //con el fin de mostrar estadísticas por consola
                    int lastCodeLength = remainingEncodedText.CodeLength;

                    //Determino cada cuantas palabras se mostrará el progresso por consola
                    int wordsDebugStep = (int)Math.Min(0.03 * remainingEncodedText.CodeLength, 1000);

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

                        if (lastCodeLength - remainingEncodedText.CodeLength >= wordsDebugStep)
                        {
                            lastCodeLength = remainingEncodedText.CodeLength;
                            DebugUtils.ConsoleWL(string.Format("Decoded {0} bits of {1}", _encoded.CodeLength - lastCodeLength, _encoded.CodeLength), "[PROGRESS]");
                        }
                    }
                    while (currentByteIndex + currentCodeBytes.Count < _encoded.Code.Count && !analyzingTrashBits);
                }
                catch (Exception ex)
                {
                    result = null;
                    DebugUtils.ConsoleF("Exception decoding huffman file", ex.Message);
                }
            });

            return result;
        }

        public async Task<string> DecodeWithTreeSlow()
        {
            //Si poseo un BOM, lo incluyo al principio del texto decodificado.
            string result = _fileBOMString ?? "";

            await Task.Factory.StartNew(() =>
            {
                try
                {
                    BitCode remainingEncodedText = _encoded.Copy();

                    List<byte> currentCodeBytes = new List<byte>();
                    int currentCodeLength = 0;
                    int currentByteIndex = 0; //Arranco analizando el primer byte del codigo completo
                    bool analyzingTrashBits = false;

                    //Esta variable la uso para ir contando la cantidad del código completo que ha sido decodificada,
                    //con el fin de mostrar estadísticas por consola
                    int lastCodeLength = remainingEncodedText.CodeLength;

                    //Determino cada cuantas palabras se mostrará el progresso por consola
                    int wordsDebugStep = (int)Math.Min(0.03 * remainingEncodedText.CodeLength, 1000);

                    do
                    {
                        byte currentByte = remainingEncodedText.Code[currentByteIndex];

                        //Hago desplazamientos a derecha, yendo desde 7 desplazamientos a 0
                        for (int i = 7; i >= 0; i--)
                        {
                            //Me quedo con los primeros ´8 - i´ bits de la izquierda
                            byte possibleCode = (byte)((currentByte >> i) << i);
                            int diff = 8 - i;

                            currentCodeBytes.Add(possibleCode);
                            currentCodeLength += diff;

                            //Si no estoy agregando bits basura que exceden la longitud del texto codificado
                            if (remainingEncodedText.CodeLength - currentCodeLength >= 0)
                            {
                                BitCode currentCode = new BitCode(currentCodeBytes, currentCodeLength);

                                string value = _codesTree.Get(currentCode);

                                //Si el codigo formado al realizar los 'i' desplazamientos es un codigo valido
                                if (value != default(string))
                                {
                                    //Lo decodifico y agrego al string decodificado
                                    result += value;

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
                                    currentCodeBytes.Remove(possibleCode);
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

                        if (lastCodeLength - remainingEncodedText.CodeLength >= wordsDebugStep)
                        {
                            lastCodeLength = remainingEncodedText.CodeLength;
                            DebugUtils.ConsoleWL(string.Format("Decoded {0} bits of {1}", _encoded.CodeLength - lastCodeLength, _encoded.CodeLength), "[PROGRESS]");
                        }
                    }
                    while (currentByteIndex + currentCodeBytes.Count < _encoded.Code.Count && !analyzingTrashBits);
                }
                catch (Exception ex)
                {
                    result = null;
                    DebugUtils.ConsoleF("Exception decoding huffman file", ex.Message);
                }
            });

            return result;
        }

        public async Task<string> DecodeWithTreeFast()
        {
            KryptoProcess currentProcess = KryptoProcess.GetCurrent();

            //Si poseo un BOM, lo incluyo al principio del texto decodificado.
            string result = _fileBOMString ?? "";

            await Task.Factory.StartNew(() =>
            {
                try
                {   
                    BitCode remainingEncodedText = _encoded.Copy();

                    uint baseIndex = 0;

                    //Esta variable la uso para ir contando la cantidad del código completo que ha sido decodificada,
                    //con el fin de mostrar estadísticas por consola
                    uint lastCodeLength = (uint)remainingEncodedText.CodeLength;

                    //Determino cada cuantas palabras se mostrará el progresso por consola
                    int wordsDebugStep = (int)Math.Min(0.03 * remainingEncodedText.CodeLength, 1000);

                    do
                    {
                        foreach (uint codeLength in _codesTree.TerminalCodesLengths)
                        {
                            BitCode range = BitCode.EMPTY;
                            try
                            {
                                range = remainingEncodedText.GetRange(baseIndex, codeLength);
                            }
                            catch(Exception ex)
                            {

                            }
                            string value = _codesTree.Get(range);

                            if (value != default(string))
                            {
                                result += value;
                                baseIndex += codeLength;
                                break;
                            }
                        }

                        if (lastCodeLength - ((uint)remainingEncodedText.CodeLength - baseIndex) >= wordsDebugStep)
                        {
                            lastCodeLength = (uint)remainingEncodedText.CodeLength - baseIndex;
                            uint decodedCount = (uint)_encoded.CodeLength - lastCodeLength;

                            currentProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                            {
                                Message = $"Decoded {decodedCount} bits of {_encoded.CodeLength}",
                                ProgressAdvance = decodedCount * 100 / (double)_encoded.CodeLength,
                                Tag = "[PROGRESS]"
                            });
                            //DebugUtils.ConsoleWL(string.Format("Decoded {0} bits of {1}", _encoded.CodeLength - lastCodeLength, _encoded.CodeLength), "[PROGRESS]");
                        }
                    }
                    while (baseIndex < remainingEncodedText.CodeLength);
                }
                catch (Exception ex)
                {
                    result = null;
                    DebugUtils.ConsoleF("Exception decoding huffman file", ex.Message);
                }
            });

            return result;
        }

        /// <summary>
        /// Decodifica el arbol utilizando varios Threads en simultaneo.
        /// Luego, espera a que finalicen todos utilizando el metodo Task.WhenAll.
        /// Mas informacion en el link https://msdn.microsoft.com/en-us/library/hh194874(v=vs.110).aspx
        /// </summary>
        /// <returns></returns>
        public async Task<string> DecodeWithTreeMultithreaded(BaseKryptoProcess currentProcess = null)
        {
            //TODO Mejorar esto
            //if (currentProcess == null)
            //{
            //    currentProcess = KryptoProcess.GetCurrent();
            //}

            currentProcess?.UpdateStatus($"Decoding with Huffman using {_encodedPartsLengths.Count} threads");

            //Si poseo un BOM, lo incluyo al principio del texto decodificado.
            string decoded = _fileBOMString ?? "";

            //Creo tantas Task como partes tenga el documento            
            Task<string>[] tasks = new Task<string>[_encodedPartsLengths.Count];

            //Creo un diccionario en el cual almacenare el progreso de cada tarea, 
            //para poder imprimir por consola las estadisticas
            _tasksProgress = new Dictionary<int, uint>();

            uint baseIndex = 0;
            for (int i = 0; i < _encodedPartsLengths.Count; i++)
            {
                tasks[i] = DecodeWithTreeTask(baseIndex, _encodedPartsLengths[i]);
            baseIndex += _encodedPartsLengths[i];
                _tasksProgress.Add(tasks[i].Id, 0);
            }

            Timer timer = new Timer(
                (optionalParam) =>
                {
                    uint totalProgress = 0;

                    foreach (uint progress in _tasksProgress.Values.ToList())
                    {
                        totalProgress += progress;
                    }

                    currentProcess.AddEvent(new BaseKryptoProcess.KryptoEvent()
                    {
                        Message = $"Decoded {totalProgress} bits of {_encoded.CodeLength}",
                        ProgressAdvance = totalProgress * 100 / (double)_encoded.CodeLength,
                        Tag = "[PROGRESS]"
                    });

                    DebugUtils.ConsoleWL(string.Format("Decoded {0} bits of {1}", totalProgress, _encoded.CodeLength), "[PROGRESS]");
                },
                null, 0, 4000);


            //Las partes decodificadas se encontraran en el mismo orden en que se iniciaron las Task,
            //por lo que simplemente hay que concatenarlas.            
            string[] decodedParts = await Task.WhenAll(tasks);
            timer.Dispose();

            currentProcess?.AddEvent(new BaseKryptoProcess.KryptoEvent()
            {
                Message = $"Huffman decoding process finished",
                ProgressAdvance = 100,
                Tag = "[RESULT]"
            });

            currentProcess?.UpdateStatus("Joining decoded parts into one array of bytes", true);

            decoded += string.Join("", decodedParts);

            currentProcess?.AddEvent(new BaseKryptoProcess.KryptoEvent()
            {
                Message = $"Decoded file joining process finished in a {decoded.Length} symbols file",
                ProgressAdvance = 100,
                Tag = "[RESULT]"
            });

            return decoded;
        }

        private Task<string> DecodeWithTreeTask(uint baseIndex, uint count)
        {
            return Task.Factory.StartNew(() =>
            {
                string result = "";

                if (Task.CurrentId != null)
                {
                    int currentTaskId = (int)Task.CurrentId;
                    try
                    {
                        BitCode remainingEncodedText = _encoded.GetRange(baseIndex, count);
                        baseIndex = 0;

                        //Esta variable la uso para ir contando la cantidad del código completo que ha sido decodificada,
                        //con el fin de mostrar estadísticas por consola
                        uint lastCodeLength = (uint)remainingEncodedText.CodeLength;

                        //Determino cada cuantas palabras se mostrará el progresso por consola
                        int wordsDebugStep = (int)Math.Min(0.03 * remainingEncodedText.CodeLength, 1000);

                        do
                        {
                            foreach (uint codeLength in _codesTree.TerminalCodesLengths)
                            {
                                BitCode range = remainingEncodedText.GetRange(baseIndex, codeLength);

                                string value = _codesTree.Get(range);

                                if (value != default(string))
                                {
                                    result += value;
                                    baseIndex += codeLength;
                                    _tasksProgress[currentTaskId] = baseIndex;// (uint)remainingEncodedText.CodeLength - baseIndex;
                                    break;
                                }
                            }
                        }
                        while (baseIndex < remainingEncodedText.CodeLength);
                    }
                    catch (Exception ex)
                    {
                        result = null;
                        DebugUtils.ConsoleF("Exception decoding huffman file", ex.Message);
                    }
                }
                else
                {
                    result = null;
                    DebugUtils.ConsoleF("Exception decoding huffman file", "Task ID is null");
                }
                return result;
            });
        }

        #endregion
    }
}
