using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.huffman
{
    public class HuffmanTreeNode
    {
        public float Probability { get; set; }

        public List<HuffmanTreeNode> ParentsPositions { get; set; }

        public BitCode Code { get; set; }

        public HuffmanTreeNode(float prob, List<HuffmanTreeNode> parentsPositions)
        {
            Probability = prob;
            ParentsPositions = parentsPositions;
        }
    }
}
