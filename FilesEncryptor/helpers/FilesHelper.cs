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
    public class FilesHelper
    {
        private List<string> _filesExtensions;
        private StorageFile _selectedFile;
        private FileHeader _selectedFileHeader;
        private IRandomAccessStream _fileStream;
        private IInputStream _fileInputStream;
        private DataReader _fileDataReader;
        private uint _fileSize;

        public uint FileSize => _fileSize;
        public string SelectedFileName => _selectedFile != null ? _selectedFile.Name : "";
        public string SelectedFileExtension => _selectedFile != null ? _selectedFile.FileType : "";
        public string SelectedFileDisplayName => _selectedFile != null ? _selectedFile.DisplayName : "";
        public FileHeader SelectedFileHeader => _selectedFileHeader;

        public FilesHelper(List<string> filesExtensions, StorageFile selectedFile = null)
        {
            _filesExtensions = filesExtensions;
            _selectedFile = selectedFile;
        }

        /// <summary>
        /// Lanza el explorador de archivos
        /// </summary>
        /// <returns></returns>
        public async Task<bool> Pick()
        {
            var picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            foreach (string extension in _filesExtensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            var file = await picker.PickSingleFileAsync();

            if(file != null)
            {
                Finish();
                _selectedFile = file;
            }

            return file != null;
        }

        public async Task<bool> OpenFile(FileAccessMode accesMode)
        {
            bool result = false;
            _fileSize = 0;

            if (_selectedFile == null)
            {
                await Pick();
            }

            //Abrir el archivo y obtener sus propiedades
            if (_selectedFile != null)
            {
                //Abro el archivo para lectura
                _fileStream = await _selectedFile.OpenAsync(accesMode);
                _fileInputStream = _fileStream.GetInputStreamAt(0);
                _fileDataReader = new DataReader(_fileInputStream);
                var size = _fileStream.Size;
                
                //Cargo en el buffer todos los bytes del archivo
                _fileSize = await _fileDataReader.LoadAsync((uint)size);
                
                result = true;
            }

            return result;
        }

        public async Task<FileHeader> ExtractFileHeader()
        {
            FileHeader result = null;
            bool fileOpened = _fileDataReader != null;
            _selectedFileHeader = null;

            if (!fileOpened)
            {
                fileOpened = await OpenFile(FileAccessMode.Read);
            }

            //Abrir el archivo y obtener sus propiedades
            if (fileOpened)
            {
                result = new FileHeader();

                //Obtengo el largo del tipo de archivo
                string fileExtLength = await ReadStringUntil(":");

                //Obtengo el tipo de archivo
                result.FileExtensionLength = uint.Parse(fileExtLength);
                result.FileExtension = await ReadString(result.FileExtensionLength);

                //Obtengo el largo de la descripcion del tipo de archivo
                string fileDisplayTypeLength = await ReadStringUntil(":");

                //Obtengo la descripcion del tipo de archivo
                result.FileDisplayTypeLength = uint.Parse(fileDisplayTypeLength);
                result.FileDisplayType = await ReadString(result.FileDisplayTypeLength);

                //Obtengo el largo del nombre original del archivo
                string fileNameLength = await ReadStringUntil(":");

                //Obtengo la descripcion del tipo de archivo
                result.FileNameLength = uint.Parse(fileNameLength);
                result.FileName = await ReadString(result.FileNameLength);
            }

            _selectedFileHeader = result;

            return result;
        }
        
        public async Task<string> ReadString(uint stringLength)
        {
            string result = null;
            bool isOpened = _fileDataReader != null;

            if(!isOpened)
            {
                isOpened = await OpenFile(FileAccessMode.Read);
            }

            if (isOpened)
            {
                result = _fileDataReader.ReadString(stringLength);
            }

            return result;          
        }

        public async Task<string> ReadStringUntil(string finishMark)
        {
            string result = null;
            bool isOpened = _fileDataReader != null;

            if (!isOpened)
            {
                isOpened = await OpenFile(FileAccessMode.Read);
            }

            if (isOpened)
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

        public async Task<byte[]> ReadBytes(uint bytesCount)
        {
            byte[] result = null;
            bool isOpened = _fileDataReader != null;

            if (!isOpened)
            {
                isOpened = await OpenFile(FileAccessMode.Read);
            }

            if (isOpened)
            {
                result = new byte[bytesCount];
                _fileDataReader.ReadBytes(result);
            }

            return result;
        }

        public void Finish()
        {
            if(_fileDataReader != null)
            {
                _fileDataReader.Dispose();
                _fileInputStream.Dispose();
                //await _fileStream.FlushAsync();
                _fileStream.Dispose();

                _fileDataReader = null;
                _fileInputStream = null;
                _fileStream = null;
            }
        }
    }
}
