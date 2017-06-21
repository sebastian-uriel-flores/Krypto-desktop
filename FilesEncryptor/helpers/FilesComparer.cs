using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace FilesEncryptor.helpers.file_management
{
    public class FilesComparer
    {
        private List<StorageFile> _selectedFiles;
        private List<FileHelper> _filesHelpers;

        public FilesComparer(List<StorageFile> files = null)
        {
            _selectedFiles = files;
            _filesHelpers = new List<FileHelper>();

            if (files != null)
            {
                foreach (StorageFile file in files)
                {
                    _filesHelpers.Add(new FileHelper(file));
                }
            }
        }

        public async Task<bool> PickFiles(List<string> filesExtensions)
        {
            bool result = false;

            var picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            foreach (string extension in filesExtensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            var files = await picker.PickMultipleFilesAsync();

            if (files?.Count > 0)
            {
                //Cierro todos los archivos abiertos por los Files Helpers
                await Finish();
                _selectedFiles = new List<StorageFile>(files);
                //Elimino todos los Files Helpers, dado que no se cuantos archivos se han abierto esta vez 
                //y podria ser una cantidad distinta a la de la lista _filesHelpers
                _filesHelpers.Clear();

                foreach(StorageFile file in files)
                {
                    _filesHelpers.Add(new FileHelper(file));
                }
                result = true;
            }

            return result;
        }

        public async Task<bool> OpenFiles()
        {
            bool openResult = false;

            foreach(FileHelper fileHelper in _filesHelpers)
            {
                openResult = await fileHelper.OpenFile(FileAccessMode.Read);

                //Si uno de los archivos no pudo abrirse, 
                //cierro todos los que fueron abiertos y cancelo la apertura
                if(!openResult)
                {
                    await Finish();
                    break;
                }
            }

            return openResult;
        }

        public bool CompareFiles()
        {
            bool compareResult = false;
            List<BitCode> filesBytes = new List<BitCode>();
            
            foreach(FileHelper fileHelper in _filesHelpers)
            {
                byte[] arr = fileHelper.ReadBytes(fileHelper.FileSize);
                filesBytes.Add(new BitCode(arr.ToList(), arr.Count()*8));
            }

            for(int i = 1; i < _filesHelpers.Count; i++)
            {
                BitCode file1Bytes = filesBytes[i - 1];
                BitCode file2Bytes = filesBytes[i];

                var res = file1Bytes.CompareTo(file2Bytes);
                compareResult = res.Item1 && res.Item2.Count == 0;

                //Si hay 1 archivo diferente, cancelo la comparacion
                if(!compareResult)
                {
                    DebugUtils.WriteLine(string.Format("Files {0} {1} are different:", i, i-1));
                    foreach (uint diff in res.Item2)
                    {
                        DebugUtils.WriteLine(string.Format("Difference at bit {0}", diff));
                    }
                    break;
                }
            }

            return compareResult;
        }

        public async Task Finish()
        {
            foreach(FileHelper fileHelper in _filesHelpers)
            {
                await fileHelper.Finish();
            }
        }
    }
}
