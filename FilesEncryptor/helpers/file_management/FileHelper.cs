using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace FilesEncryptor.helpers
{
    public class FileHelper
    {
        private const string FILE_HEADER_PATTERN = "{0}:{1},{2},{3}";

        #region VARIABLES

        private StorageFile _selectedFile;
        private IRandomAccessStream _fileStream;
        private IInputStream _fileInputStream;
        private IOutputStream _fileOutputStream;
        private DataReader _fileDataReader;
        private DataWriter _fileDataWriter;
        private uint _fileSize;
        private FileAccessMode _fileAccessMode;
        private Encoding _fileEncoding;
        private byte[] _fileBOM;

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Devuelve el tamaño del archivo completo.
        /// </summary>
        public uint FileSize => _fileSize;

        /// <summary>
        /// Devuelve el tamaño del contenido del archivo, es decir, del archivo sin incluir el FileBOM.
        /// </summary>
        public uint FileContentSize => _fileBOM != null? _fileSize - (uint)_fileBOM.Length : _fileSize;
        public Encoding FileEncoding => _fileEncoding;
        public byte[] FileBOM => _fileBOM;
        public string SelectedFileName => _selectedFile != null ? _selectedFile.Name : "";
        public string SelectedFileExtension => _selectedFile != null ? _selectedFile.FileType : "";
        public string SelectedFileDisplayName => _selectedFile != null ? _selectedFile.DisplayName : "";
        public string SelectedFileDisplayType => _selectedFile != null ? _selectedFile.DisplayType : "";
        public string SelectedFilePath => _selectedFile != null ? _selectedFile.Path : "";

        public void SetFileEncoding(Encoding encoding)
        {
            if (encoding == Encoding.Unicode)
            {
                _fileDataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf16LE;
            }
            else if (encoding == Encoding.BigEndianUnicode)
            {
                _fileDataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf16BE;
            }
            else
            {
                _fileDataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            }

            if (_fileDataWriter != null)
            {
                _fileDataWriter.UnicodeEncoding = _fileDataReader.UnicodeEncoding;
            }

            _fileEncoding = encoding;
        }

        #endregion

        public FileHelper(StorageFile selectedFile = null)
        {
            _selectedFile = selectedFile;
        }

        /// <summary>
        /// Lanza el explorador de archivos, para abrir un archivo
        /// </summary>
        /// <returns></returns>
        public async Task<bool> PickToOpen(List<string> filesExtensions)
        {
            var picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            foreach (string extension in filesExtensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                await Finish();
                _selectedFile = file;
            }

            return file != null;
        }

        public async Task<bool> PickToSave(string suggestedFileName, string fileDisplayType, string fileExtension)
        {
            var picker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedFileName
            };

            picker.FileTypeChoices.Add(fileDisplayType, new List<string>() { fileExtension });

            var file = await picker.PickSaveFileAsync();

            if (file != null)
            {
                await Finish();
                _selectedFile = file;
            }

            return file != null;
        }

        public async Task<bool> OpenFile(FileAccessMode accesMode, bool takeEncoding = false)
        {
            bool result = false;

            //Abrir el archivo y obtener sus propiedades
            if (_selectedFile != null)
            {
                _fileAccessMode = accesMode;
                _fileSize = 0;

                if(_fileAccessMode == FileAccessMode.ReadWrite)
                {
                    // Prevent updates to the remote version of the file until
                    // we finish making changes and call CompleteUpdatesAsync.
                    try
                    {
                        CachedFileManager.DeferUpdates(_selectedFile);
                    }
                    catch(Exception ex)
                    {

                    }
                }

                //Abro el archivo en el modo indicado
                _fileStream = await _selectedFile.OpenAsync(accesMode);
                
                var size = _fileStream.Size;

                //Cargo el Flujo de entrada
                _fileInputStream = _fileStream.GetInputStreamAt(0);                
                _fileDataReader = new DataReader(_fileInputStream);

                //Si debo verificar la codificacion del archivo
                if (takeEncoding)
                {
                    //Cargo en el buffer todos los bytes del archivo
                    _fileSize = await _fileDataReader.LoadAsync((uint)size);

                    //Leo los primeros 4 bytes y determino la codificacion del archivo
                    _fileBOM = ReadBytes(4);                     
                    
                    //Dado que ya lei 4 bytes, vuelvo a cargar el Flujo de entrada, desde el primer bit
                    _fileInputStream = _fileStream.GetInputStreamAt(0);
                    _fileDataReader = new DataReader(_fileInputStream);                    
                }

                //Si el archivo fue abierto en modo de escritura, cargo el Flujo de salida
                if(accesMode == FileAccessMode.ReadWrite)
                {
                    _fileOutputStream = _fileStream.GetOutputStreamAt(0);
                    _fileDataWriter = new DataWriter(_fileOutputStream);
                }

                //Si se indico verificar la codificacion del archivo,
                //basandome en la codificacion, configuro el data DataReader y el DataWriter
                //Si no se habia indicado, entonces seteo una configuracion por defecto
                Encoding fileEncoding = GetEncoding(_fileBOM);
                SetFileEncoding(fileEncoding);

                //Cargo en el buffer todos los bytes del archivo
                _fileSize = await _fileDataReader.LoadAsync((uint)size);

                //Si se indico verificar la codificacion del archivo,
                //Dado que siempre leo 4 bytes para el BOM, y segun la codificacion, el BOM podria ser de 2, 3 o 4 bytes,
                //podria tener en el arreglo algunos bytes correspondientes al BOM y otros correspondientes al texto.
                //Elimino esos bytes extra, correspondientes al texto.
                if (_fileBOM != null)
                {
                    int bomBytes = GetBOMBytesLen(fileEncoding);

                    //Si es una codificacion para la cual se inserta un BOM al principio del archivo
                    if (bomBytes > 0)
                    {
                        //_fileSize -= (uint)bomBytes;
                        _fileBOM = _fileBOM.ToList().GetRange(0, bomBytes).ToArray();

                        //Salteo los bytes correspondientes al BOM, dado que no quiero leerlos de nuevo.
                        ReadBytes((uint)bomBytes);
                    }
                    //Si se leyeron 4 bytes pero la codificacion no utiliza un BOM
                    else
                    {
                        _fileBOM = null;
                    }
                }

                result = true;
            }

            return result;
        }

        #region READ_ACTIONS

        public FileHeader ReadFileHeader()
        {
            FileHeader result = null;

            //Abrir el archivo y obtener sus propiedades
            if (_fileDataReader != null)
            {
                result = new FileHeader();

                //Obtengo el largo del header
                string rawFileHeaderLength = ReadStringUntil(":");
                uint fileHeaderLength = uint.Parse(rawFileHeaderLength);

                //Obtengo el header crudo
                string rawHeader = ReadString(fileHeaderLength);
                var headerParts = rawHeader.Split(',');

                result = new FileHeader()
                {
                    FileExtension = headerParts[0], //Obtengo el tipo de archivo
                    FileDisplayType = headerParts[1], //Obtengo la descripcion del tipo de archivo
                    FileName = headerParts[2] //Obtengo el nombre del archivo
                };
            }

            return result;
        }
        
        public string ReadString(uint stringLength)
        {
            string result = null;

            if (_fileDataReader != null)
            {
                if (_fileEncoding != null)
                {
                    int bitsCount = _fileDataReader.UnicodeEncoding == Windows.Storage.Streams.UnicodeEncoding.Utf8 ? 8 : 16;
                    uint bytesCount = BitCode.BitsLengthToBytesLength((uint)bitsCount * stringLength);
                    byte[] bytes = ReadBytes(bytesCount);
                    result = _fileEncoding.GetString(bytes);
                }
                else
                {
                    result = _fileDataReader.ReadString(stringLength);
                }
            }

            return result;          
        }

        public string ReadStringUntil(string finishMark)
        {
            string result = null;

            if (_fileDataReader != null)
            {
                string temp = ReadString(1);// _fileDataReader.ReadString(1);
                result = "";
                while (temp != finishMark)
                {
                    result += temp;
                    temp = ReadString(1);//_fileDataReader.ReadString(1);
                }
            }

            return result;
        }

        public byte[] ReadBytes(uint bytesCount)
        {
            byte[] result = null;

            if (_fileDataReader != null)
            {
                result = new byte[bytesCount];
                _fileDataReader.ReadBytes(result);
            }

            return result;
        }


        /// <summary>
        /// Determines a text file's encoding by analyzing its byte order mark (BOM).
        /// Defaults to ASCII when detection of the text file's endianness fails.
        /// </summary>
        /// <param name="filename">The text file to analyze.</param>
        /// <returns>The detected encoding.</returns>
        public static Encoding GetEncoding(byte[] bom)
        {
            // Read the BOM            
            Encoding result = null;

            // Analyze the BOM
            if (bom != null)
            {
                if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76)
                {
                    result = Encoding.UTF7;
                }
                else if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
                {
                    result = Encoding.UTF8;
                }
                else if (bom[0] == 0xff && bom[1] == 0xfe)
                {
                    result = Encoding.Unicode; //UTF-16LE
                }
                else if (bom[0] == 0xfe && bom[1] == 0xff)
                {
                    result = Encoding.BigEndianUnicode; //UTF-16BE
                }
                else if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff)
                {
                    result = Encoding.UTF32;
                }
            }

            if(result == null)
            {
                result = Encoding.ASCII;
            }

            return result;
        }

        /// <summary>
        /// Determines a text file's encoding by analyzing its byte order mark (BOM).
        /// Defaults to ASCII when detection of the text file's endianness fails.
        /// </summary>
        /// <param name="filename">The text file to analyze.</param>
        /// <returns>The detected encoding.</returns>
        public static int GetBOMBytesLen(Encoding enc)
        {
            int result = 0;

            if(enc == Encoding.UTF32)
            {
                result = 4;
            }
            else if(enc == Encoding.UTF8 || enc == Encoding.UTF7)
            {
                result = 3;
            }
            else if(enc == Encoding.Unicode || enc == Encoding.BigEndianUnicode)
            {
                result = 2;
            }
            
            return result;
        }

        #endregion

        #region WRITE_ACTIONS

        public bool WriteFileHeader(FileHeader fileHeader)
        {
            bool result = false;

            if (_fileAccessMode == FileAccessMode.ReadWrite)
            {
                uint headerLength = (uint)(fileHeader.FileName.Length + fileHeader.FileDisplayType.Length + fileHeader.FileExtension.Length) + 2;

                _fileDataWriter.WriteString(
                    string.Format(
                        FILE_HEADER_PATTERN,
                        headerLength,
                        fileHeader.FileExtension,
                        fileHeader.FileDisplayType,
                        fileHeader.FileName));
                    
                result = true;
            }

            return result;
        }

        public bool WriteString(string str)
        {
            bool result = false;

            if(_fileAccessMode == FileAccessMode.ReadWrite)
            {
                if (_fileEncoding != null)
                {
                    int bitsCount = _fileDataReader.UnicodeEncoding == Windows.Storage.Streams.UnicodeEncoding.Utf8 ? 8 : 16;
                    byte[] bytes = _fileEncoding.GetBytes(str);
                    _fileDataWriter.WriteBytes(bytes);
                }
                else
                {
                    _fileDataWriter.WriteString(str);
                }
                result = true;
            }

            return result;
        }

        public bool WriteBytes(byte[] bytes)
        {
            bool result = false;

            if (_fileAccessMode == FileAccessMode.ReadWrite)
            {
                _fileDataWriter.WriteBytes(bytes);
                result = true;
            }

            return result;
        }

        #endregion

        public async Task Finish()
        {
            if(_selectedFile != null && _fileDataReader != null)
            {
                if(_fileAccessMode == FileAccessMode.ReadWrite)
                {
                    //Escribo en el archivo todo el buffer
                    await _fileDataWriter.StoreAsync();
                    await _fileOutputStream.FlushAsync();
                    _fileDataWriter.Dispose();
                    _fileOutputStream.Dispose();
                }

                _fileDataReader?.Dispose();                
                _fileInputStream?.Dispose();
                //await _fileStream.FlushAsync();
                
                _fileStream?.Dispose();

                _fileDataReader = null;
                _fileDataWriter = null;
                _fileInputStream = null;
                _fileOutputStream = null;
                _fileStream = null;
                //_fileSize = 0;

                if(_fileAccessMode == FileAccessMode.ReadWrite)
                {
                    // Let Windows know that we're finished changing the file so
                    // the other app can update the remote version of the file.
                    // Completing updates may require Windows to ask for user input.
                    Windows.Storage.Provider.FileUpdateStatus status =
                        await CachedFileManager.CompleteUpdatesAsync(_selectedFile);
                }
            }
        }
    }
}
