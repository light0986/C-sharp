using System;

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
            int Pos = DataPos();
            if (Pos != 0)
            {
                string str = char.ConvertFromUtf32(FileArray[Pos]);
                str += char.ConvertFromUtf32(FileArray[Pos + 1]);
                str += char.ConvertFromUtf32(FileArray[Pos + 2]);
                str += char.ConvertFromUtf32(FileArray[Pos + 3]);

                return str;
            }
            return "";
        }

        public int GetSubchunk2Size()
        {
            int Pos = DataPos();
            if (Pos != 0)
            {
                string str = FileArray[Pos + 7].ToString("X2");
                str += FileArray[Pos + 6].ToString("X2");
                str += FileArray[Pos + 5].ToString("X2");
                str += FileArray[Pos + 4].ToString("X2");

                try
                {
                    return Convert.ToInt32(str, 16);
                }
                catch { }
            }
            return 0;
        }

        private int DataPos()
        {
            for (int i = 0; i < FileArray.Length; i++)
            {
                //64 61 74 61
                if (FileArray[i] == 0x64 && FileArray[i + 1] == 0x61 && FileArray[i + 2] == 0x74 && FileArray[i + 3] == 0x61)
                {
                    return i;
                }
            }
            return 0;
        }

        public byte[] GetData()
        {
            int L = GetSubchunk2Size();
            if (L != 0)
            {
                byte[] Data = new byte[L];
                for (int i = 1; i <= L; i++)
                {
                    Data[L - i] = FileArray[FileArray.Length - i];
                }
                return Data;
            }
            return new byte[0];
        }

        public double[] GetSmallData()
        {
            int cut = 4;
            byte[] Data = GetData();
            double[] sData = new double[Data.Length / cut];

            double sample_rate = GetSampleRate();
            double delta = 2.0 * Math.PI * (sample_rate / 1000) / sample_rate;

            for (int i = 0; i < sData.Length; i++)
            {
                string data1 = Data[(i * cut) + 1].ToString("X2");
                data1 += Data[(i * cut) + 0].ToString("X2");
                double one = (double)Convert.ToInt32(data1, 16) / 65536;

                string data2 = Data[(i * cut) + 3].ToString("X2");
                data2 += Data[(i * cut) + 2].ToString("X2");
                double two = (double)Convert.ToInt32(data2, 16) / 65536;

                double MaxValue = one > two ? one : two;
                sData[i] = 50 - (Math.Abs(0.5 - MaxValue) * 100);
                //sData[i] = MaxValue;
            }

            double L = sample_rate / 100.0;
            double[] outData = new double[(int)(sData.Length / L) + 1];

            for(int i = 0; i < outData.Length; i++)
            {
                outData[i] = 0;

                for (int j = 0; j < L; j++)
                {
                    int pos = (i * (int)L) + j;
                    if (outData[i] < sData[pos]) { outData[i] = sData[pos]; }
                    if (pos == sData.Length - 1) { break; }
                }
            }

            return outData;
        }
    }
}
