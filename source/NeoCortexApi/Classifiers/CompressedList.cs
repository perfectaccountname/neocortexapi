using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using NeoCortexApi.Entities;

namespace NeoCortexApi.Classifiers
{
    /// <summary>
    /// Distributes huge dictionary across mutliple dictionaries. Used mainly for testing purposes.
    /// Special case of this dictionary is with number of nodes = 1. In this case dictionary is redused 
    /// to a single dictionary, which corresponds original none-distributed implementation of SP and TM.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class CompressedList : IEnumerable
    {
        private string compressedString = "";
        private int divider = 10;
        private string fmt = "00000";


        public CompressedList()
        {
        }

        public int[] this[int index] 
        { 
            get
            {
                List<int> res = new List<int>();
                // decompression
                string shortCompressedString = compressedString.Split(",")[index];
                BigInteger bigInt = 0;
                foreach(char remainder in shortCompressedString.ToArray())
                {
                    bigInt = bigInt*divider + int.Parse(remainder.ToString());
                }
                int numPadding = (fmt.Length - (bigInt.ToString().Length % fmt.Length));
                string loopLength = bigInt.ToString().PadLeft(numPadding + bigInt.ToString().Length, '0');          
                for (int i = 0; i < loopLength.Length; i += fmt.Length)
                {
                    res.Add(int.Parse(loopLength.Substring(i, fmt.Length)));
                }

                return res.ToArray();
            }
            set
            {
                // update list
                List<string> temp = compressedString.Split(",").ToList();

                var combinedString = String.Join("", value);
                var bigInt = BigInteger.Parse(combinedString);
                string tempCompressedString = "";

                //
                // Compression
                while (bigInt != 0)
                {
                    var remainder = bigInt % divider;
                    tempCompressedString += remainder.ToString(fmt);
                    bigInt = (bigInt - remainder) / divider;
                }
                temp[index] = tempCompressedString;
                compressedString = String.Join(",", temp);
            }
        }

        public int Count => compressedString.Split(",").Length;

        public void Add(int[] item)
        {
            List<string> itemList = new List<string>();
            foreach (var item2 in item)
            {
                itemList.Add(item2.ToString().PadLeft(fmt.Length, '0'));
            }
            var combinedString = String.Join("", itemList);
            var bigInt = BigInteger.Parse(combinedString);

            //
            // Compression
            while (bigInt != 0)
            {
                var remainder = bigInt % divider;
                compressedString = remainder.ToString() + compressedString;
                bigInt = (bigInt - remainder) / divider;
            }
            compressedString = "," + compressedString;
        }

        public void Clear()
        {
            compressedString = null;
        }

        public int[] Last()
        {
            return this[this.Count - 1];
        }

        public IEnumerator<int[]> GetEnumerator()
        {
            for (var i = 0; i < this.Count; i++)
            {
                yield return this[i];
            }
        }

        public void RemoveAt(int index)
        {
            List<string> temp = compressedString.Split(",").ToList();
            temp.RemoveAt(index);
            compressedString = String.Join(",", temp);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
