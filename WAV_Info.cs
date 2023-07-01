using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioAnimation
{
    public class WAV_Info
    {
        private byte[] FileArray { get; set; }

        public WAV_Info(byte[] fileArray)
        {
            FileArray = fileArray;
        }

        public string GetChunkID()
        {
            string str = char.ConvertFromUtf32(FileArray[0]);
            str += char.ConvertFromUtf32(FileArray[1]);
            str += char.ConvertFromUtf32(FileArray[2]);
            str += char.ConvertFromUtf32(FileArray[3]);

            return str;
        }

        public int GetChunkSize()
        {
            //24 08 00 00 => 824 => 2084
            string str = FileArray[7].ToString("X2");
            str += FileArray[6].ToString("X2");
            str += FileArray[5].ToString("X2");
            str += FileArray[4].ToString("X2");

            try
            {
                return Convert.ToInt32(str, 16);
            }
            catch { }
            return 0;
        }

        public string GetFormat()
        {
            string str = char.ConvertFromUtf32(FileArray[8]);
            str += char.ConvertFromUtf32(FileArray[9]);
            str += char.ConvertFromUtf32(FileArray[10]);
            str += char.ConvertFromUtf32(FileArray[11]);

            return str;
        }

        public string GetSubchunk1ID()
        {
            string str = char.ConvertFromUtf32(FileArray[12]);
            str += char.ConvertFromUtf32(FileArray[13]);
            str += char.ConvertFromUtf32(FileArray[14]);
            str += char.ConvertFromUtf32(FileArray[15]);

            return str;
        }

        public int GetSubchunk1Size()
        {
            string str = FileArray[19].ToString("X2");
            str += FileArray[18].ToString("X2");
            str += FileArray[17].ToString("X2");
            str += FileArray[16].ToString("X2");

            try
            {
                return Convert.ToInt32(str, 16);
            }
            catch { }
            return 0;
        }

        public int GetAudioFormat()
        {
            string str = FileArray[21].ToString("X2") + FileArray[20].ToString("X2");
            try
            {
                return Convert.ToInt32(str, 16);
            }
            catch { }
            return 0;
        }

        public int GetNumChannels()
        {
            string str = FileArray[23].ToString("X2") + FileArray[22].ToString("X2");
            try
            {
                return Convert.ToInt32(str, 16);
            }
            catch { }
            return 0;
        }

        public int GetSampleRate()
        {
            string str = FileArray[27].ToString("X2");
            str += FileArray[26].ToString("X2");
            str += FileArray[25].ToString("X2");
            str += FileArray[24].ToString("X2");

            try
            {
                return Convert.ToInt32(str, 16);
            }
            catch { }
            return 0;
        }

        public int GetByteRate()
        {
            string str = FileArray[31].ToString("X2");
            str += FileArray[30].ToString("X2");
            str += FileArray[29].ToString("X2");
            str += FileArray[28].ToString("X2");

            try
            {
                return Convert.ToInt32(str, 16);
            }
            catch { }
            return 0;
        }

        public int GetBlockAlign()
        {
            string str = FileArray[33].ToString("X2") + FileArray[32].ToString("X2");
            try
            {
                return Convert.ToInt32(str, 16);
            }
            catch { }
            return 0;
        }

        public int GetBitsPerSample()
        {
            string str = FileArray[35].ToString("X2") + FileArray[34].ToString("X2");
            try
            {
                return Convert.ToInt32(str, 16);
            }
            catch { }
            return 0;
        }

        public string GetSubchunk2ID()
        {
            string str = char.ConvertFromUtf32(FileArray[36]);
            str += char.ConvertFromUtf32(FileArray[37]);
            str += char.ConvertFromUtf32(FileArray[38]);
            str += char.ConvertFromUtf32(FileArray[39]);

            return str;
        }

        public int GetSubchunk2Size()
        {
            string str = FileArray[43].ToString("X2");
            str += FileArray[42].ToString("X2");
            str += FileArray[41].ToString("X2");
            str += FileArray[40].ToString("X2");

            try
            {
                return Convert.ToInt32(str, 16);
            }
            catch { }
            return 0;
        }

        public byte[] GetData()
        {
            byte[] Data = new byte[FileArray.Length - 44];
            for (int i = 0; i < FileArray.Length; i++)
            {

                if (i >= 44)
                {
                    Data[i - 44] = FileArray[i];
                }
            }
            return Data;
        }
    }
}
