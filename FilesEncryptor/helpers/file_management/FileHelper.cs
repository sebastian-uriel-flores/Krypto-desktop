﻿using FilesEncryptor.dto;
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
        private FileHeader _selectedFileHeader;
        private IRandomAccessStream _fileStream;
        private IInputStream _fileInputStream;
        private IOutputStream _fileOutputStream;
        private DataReader _fileDataReader;
        private DataWriter _fileDataWriter;
        private uint _fileSize;
        private FileAccessMode _fileAccessMode;

        #endregion

        #region PROPERTIES

        public uint FileSize => _fileSize;
        public string SelectedFileName => _selectedFile != null ? _selectedFile.Name : "";
        public string SelectedFileExtension => _selectedFile != null ? _selectedFile.FileType : "";
        public string SelectedFileDisplayName => _selectedFile != null ? _selectedFile.DisplayName : "";
        public string SelectedFileDisplayType => _selectedFile != null ? _selectedFile.DisplayType : "";
        public string SelectedFilePath => _selectedFile != null ? _selectedFile.Path : "";

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

        public async Task<bool> OpenFile(FileAccessMode accesMode)
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
                    CachedFileManager.DeferUpdates(_selectedFile);
                }

                //Abro el archivo en el modo indicado
                _fileStream = await _selectedFile.OpenAsync(accesMode);

                var size = _fileStream.Size;

                //Cargo el Flujo de entrada
                _fileInputStream = _fileStream.GetInputStreamAt(0);
                _fileDataReader = new DataReader(_fileInputStream);

                //Si el archivo fue abierto en modo de escritura, cargo el Flujo de salida
                if(accesMode == FileAccessMode.ReadWrite)
                {
                    _fileOutputStream = _fileStream.GetOutputStreamAt(0);
                    _fileDataWriter = new DataWriter(_fileOutputStream);
                }
                
                //Cargo en el buffer todos los bytes del archivo
                _fileSize = await _fileDataReader.LoadAsync((uint)size);
                
                result = true;
            }

            return result;
        }

        #region READ_ACTIONS

        public FileHeader ExtractFileHeader()
        {
            FileHeader result = null;
            _selectedFileHeader = null;

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

            _selectedFileHeader = result;

            return result;
        }
        
        public string ReadString(uint stringLength)
        {
            string result = null;
            
            if (_fileDataReader != null)
            {
                result = _fileDataReader.ReadString(stringLength);
            }

            return result;          
        }

        public string ReadStringUntil(string finishMark)
        {
            string result = null;

            if (_fileDataReader != null)
            {
                string temp = _fileDataReader.ReadString(1);
                result = "";
                while (temp != finishMark)
                {
                    result += temp;
                    temp = _fileDataReader.ReadString(1);
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
                _fileDataWriter.WriteString(str);
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
                _fileSize = 0;

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