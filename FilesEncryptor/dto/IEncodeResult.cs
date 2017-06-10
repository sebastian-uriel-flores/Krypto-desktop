using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto
{
    public interface IEncodeResult
    {
        BitCode Encoded { get; set; }
    }
}
