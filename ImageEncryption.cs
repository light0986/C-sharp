﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace 各種功能製作
{
    /// <summary>
    /// 用Mode2的方式，把資料加密進去png檔內
    /// </summary>
    public class ImageEncryption
    {
        private readonly EncryptionTools image = new EncryptionTools();

        /// <summary>
        /// 圖片加密工具，用以驗證圖片是否被修改過。
        /// 輸入字串，選擇一張圖片，會將字串加密進入圖片內。
        /// </summary>
        public Bitmap DoEncrypt(string text, Bitmap input = null, string DirPath = "")
        {
            #region 選擇一個圖片檔，並獲取圖片的原始byte[] -> bgra
            Console.WriteLine("原始字串: " + text);
            string savePath = "", tempName = "";
            Bitmap bmp = null;

            //假如輸入的Bitmap是空的，要自己指定一張圖
            if (input == null)
            {
                OpenFileDialog openFileDialog1 = new OpenFileDialog
                {
                    Filter = "Image Files (*.png)|*.PNG"
                };
                if (DialogResult.OK == openFileDialog1.ShowDialog())
                {
                    bmp = new Bitmap(openFileDialog1.FileName);
                    string path = Path.GetDirectoryName(openFileDialog1.FileName);
                    tempName = openFileDialog1.FileName.Replace(path, "").Replace("\\", "");
                }
            }
            //如果輸入的Bitmap有東西，以輸入的Bitmap進行改動
            else
            {
                bmp = new Bitmap(input);
                tempName = text + ".png";
            }

            byte[] bmpArr = image.BitmapToByteArray(bmp);
            Console.WriteLine("圖片寬: " + bmp.Width + ", 圖片高: " + bmp.Height);
            Console.WriteLine("圖片資料總長度: " + string.Join("", bmpArr).Length); //BGRA
            #endregion

            #region 存檔位子
            if (DirPath == null || String.IsNullOrWhiteSpace(DirPath))
            {
                Console.WriteLine("存檔");
                FolderBrowserDialog saveFileDialog = new FolderBrowserDialog
                {
                    Description = "Choose Save Path"
                };
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    savePath = saveFileDialog.SelectedPath;
                }
            }
            else
            {
                savePath = DirPath;
            }

            if (File.Exists(Path.Combine(savePath, tempName)))
            {
                tempName = "_" + tempName;
            }
            savePath = Path.Combine(savePath, tempName);
            #endregion

            #region 判斷byte[]轉換是否成功
            if (bmpArr == null || bmpArr.Length == 0) { Console.WriteLine("圖片轉換錯誤"); return null; }
            #endregion

            #region 確認alpha值不為0的pixel * 3數量
            int AlphaCount = image.AlphaZeroCount(bmp);
            int total = bmp.Width * bmp.Height * 4;
            int cuc = (total - AlphaCount) * 3;
            Console.WriteLine("Alpha非0的pixel數量" + cuc);
            #endregion

            #region 把一個字串轉換成binary[]
                int[] binaryArray = image.StringToBinary(text);
                int bl = string.Join("", binaryArray).Length;
                Console.WriteLine("字串轉二進位長度: " + bl); //GUID固定512
            #endregion

            #region 判斷可不可以藏字
            if (cuc - bl < 0) { Console.WriteLine("不可以藏"); return null; }
            #endregion

            #region 開始藏字，並且多餘部分全部為0，代表空白鍵
            Console.WriteLine("可以藏");
            //創造一個新的bgra
            byte[] newB = new byte[bmpArr.Length];
            int textIndex = 0;

            //每4個位元一組
            for (int i = 0; i < total; i+=4)
            {
                //如果bmp該a值為0，寫入相同資料
                if (bmpArr[i + 3] == 0)
                {
                    newB[i + 0] = bmpArr[i + 0];
                    newB[i + 1] = bmpArr[i + 1];
                    newB[i + 2] = bmpArr[i + 2];
                    newB[i + 3] = bmpArr[i + 3];
                }
                //不為0時，寫入要加密的數字
                else
                {
                    #region B藏字
                    //計算輸入值 與 藏字
                    if (textIndex < binaryArray.Length)
                    {
                        byte b = bmpArr[i + 0];
                        int value = image.Mode2_Encrypt(b, binaryArray[textIndex]);
                        newB[i + 0] = (byte)value;
                        textIndex++;
                    }
                    //藏0
                    else
                    {
                        byte b = bmpArr[i + 0];
                        int value = image.Mode2_Encrypt(b, 0);
                        newB[i + 0] = (byte)value;
                    }
                    #endregion

                    #region G藏字
                    //計算輸入值 與 藏字
                    if (textIndex < binaryArray.Length)
                    {
                        byte b = bmpArr[i + 1];
                        int value = image.Mode2_Encrypt(b, binaryArray[textIndex]);
                        newB[i + 1] = (byte)value;
                        textIndex++;
                    }
                    //藏0
                    else
                    {
                        byte b = bmpArr[i + 1];
                        int value = image.Mode2_Encrypt(b, 0);
                        newB[i + 1] = (byte)value;
                    }
                    #endregion

                    #region R藏字
                    //計算輸入值 與 藏字
                    if (textIndex < binaryArray.Length)
                    {
                        byte b = bmpArr[i + 2];
                        int value = image.Mode2_Encrypt(b, binaryArray[textIndex]);
                        newB[i + 2] = (byte)value;
                        textIndex++;
                    }
                    //藏0
                    else
                    {
                        byte b = bmpArr[i + 2];
                        int value = image.Mode2_Encrypt(b, 0);
                        newB[i + 2] = (byte)value;
                    }
                    #endregion

                    newB[i + 3] = bmpArr[i + 3]; //Alpha值必須複寫
                }
            }
            #endregion

            #region 將newB[]轉成newBitmap，並且存檔
            Bitmap newBitmap = image.ByteToBitmap(newB, bmp.Width, bmp.Height);
            newBitmap.Save(savePath, ImageFormat.Png);
            Console.WriteLine("藏字成功");
            Console.WriteLine("存檔位置: " + savePath);
            #endregion

            #region 讀取剛剛的png做驗證
            bmp = new Bitmap(savePath);
            bmpArr = image.BitmapToByteArray(bmp);
            Console.WriteLine("工作完成，請讀取剛剛的png做驗證。");
            Console.WriteLine("圖片寬: " + bmp.Width + ", 圖片高: " + bmp.Height);
            Console.WriteLine("圖片資料總長度: " + string.Join("", bmpArr).Length); //BGRA
            #endregion

            #region 確認alpha值不為0的pixel * 3數量
            AlphaCount = image.AlphaZeroCount(bmp);
            total = bmp.Width * bmp.Height * 4;
            int _cuc = (total - AlphaCount) * 3;
            Console.WriteLine("驗證的圖片Alpha非0的pixel數量" + _cuc);
            #endregion

            #region 判段是否存取失偵
            if (_cuc != cuc)
            {
                File.Delete(savePath);
                Console.WriteLine("圖片資料不同");
                return null;
            }
            #endregion

            #region 驗證解讀結果
            //可能是答案的數量
            int[] CheckArr = new int[_cuc + 16 - (_cuc % 16)];
            int index = 0;

            for (int i = 0; i < total; i+=4)
            {
                //Alpha值忽略
                if (bmpArr[i + 3] != 0)
                {
                    #region B藏字
                    //計算輸入值 與 藏字
                    int value = image.Mode2_Decrypt(bmpArr[i + 0]);
                    CheckArr[index] = (byte)value;
                    index++;
                    #endregion

                    #region G藏字
                    //計算輸入值 與 藏字
                    value = image.Mode2_Decrypt(bmpArr[i + 1]);
                    CheckArr[index] = (byte)value;
                    index++;
                    #endregion

                    #region R藏字
                    //計算輸入值 與 藏字
                    value = image.Mode2_Decrypt(bmpArr[i + 2]);
                    CheckArr[index] = (byte)value;
                    index++;
                    #endregion
                }
            }
            string ans = image.BinaryToString(CheckArr).Replace("\\0", "");
            Console.WriteLine("解讀答案為:" + ans);
            #endregion

            #region 比對與答案是否一至?
            if (ans != text)
            {
                Console.WriteLine("藏字失敗");
                File.Delete(savePath);
                return null;
            }

            Console.WriteLine("藏字成功");
            return newBitmap;
            #endregion
        }

        public string DoDecrypt(Bitmap input = null)
        {
            #region 選擇圖片，轉換成byte[]
            Bitmap bmp = null;
            if (input == null)
            {
                Console.WriteLine("請選擇一個圖片檔");
                OpenFileDialog openFileDialog2 = new OpenFileDialog
                {
                    Filter = "Image Files (*.png)|*.PNG"
                };
                if (DialogResult.OK == openFileDialog2.ShowDialog())
                {
                    bmp = new Bitmap(openFileDialog2.FileName);
                }
            }
            else
            {
                bmp = new Bitmap(input);
            }
            byte[] bmpArr = image.BitmapToByteArray(bmp);
            #endregion

            #region 創造一個初始化array
            int[] CheckArr = new int[bmpArr.Length + 16 - (bmpArr.Length % 16)];
            for (int i = 0; i < CheckArr.Length; i++)
            {
                CheckArr[i] = 0;
            }
            int index = 0;
            #endregion

            #region 將答案放進初始化array中
            for (int i = 0; i < bmpArr.Length; i += 4)
            {
                //Alpha值忽略
                if (bmpArr[i + 3] != 0)
                {
                    #region B藏字
                    //計算輸入值 與 藏字
                    int value = image.Mode2_Decrypt(bmpArr[i + 0]);
                    CheckArr[index] = (byte)value;
                    index++;
                    #endregion

                    #region G藏字
                    //計算輸入值 與 藏字
                    value = image.Mode2_Decrypt(bmpArr[i + 1]);
                    CheckArr[index] = (byte)value;
                    index++;
                    #endregion

                    #region R藏字
                    //計算輸入值 與 藏字
                    value = image.Mode2_Decrypt(bmpArr[i + 2]);
                    CheckArr[index] = (byte)value;
                    index++;
                    #endregion
                }
            }
            #endregion

            #region 解析答案並回傳
            string ans = image.BinaryToString(CheckArr).Replace("\\0", "");
            Console.WriteLine("解讀答案為:" + ans);
            Console.ReadKey();
            return ans;
            #endregion
        }
    }

    /// <summary>
    /// ImageEncryption 會用到的工具
    /// </summary>
    public class EncryptionTools
    {
        ///<summary> 字串轉二進位int[]，支援中文 </summary>
        public int[] StringToBinary(string text)
        {
            List<int> binaryList = new List<int>();
            foreach (char c in text)
            {
                string binaryString = Convert.ToString(c, 2).PadLeft(16, '0');
                foreach (char bit in binaryString)
                {
                    binaryList.Add(bit - '0');
                }
            }
            return binaryList.ToArray();
        }

        ///<summary> 把int[]轉回字串，支援中文 </summary>
        public string BinaryToString(int[] binaryArray)
        {
            StringBuilder textBuilder = new StringBuilder();
            for (int i = 0; i < binaryArray.Length; i += 16)
            {
                StringBuilder binaryBuilder = new StringBuilder();
                for (int j = 0; j < 16; j++)
                {
                    binaryBuilder.Append(binaryArray[i + j]);
                }
                string binaryString = binaryBuilder.ToString();
                int decimalValue = Convert.ToInt32(binaryString, 2);
                if ((char)decimalValue != '\0')
                {
                    textBuilder.Append((char)decimalValue);
                }
            }
            return textBuilder.ToString();
        }

        ///<summary> Bitmap轉byte[] </summary>
        public byte[] BitmapToByteArray(Bitmap bitmap)
        {
            try
            {
                BitmapData bmpdata = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int numbytes = bmpdata.Stride * bitmap.Height;
                byte[] bytedata = new byte[numbytes];
                IntPtr ptr = bmpdata.Scan0;
                Marshal.Copy(ptr, bytedata, 0, numbytes);
                bitmap.UnlockBits(bmpdata);
                return bytedata;
            }
            catch
            {
                return new byte[0];
            }
        }

        /// <summary> 將bgra轉回去Bitmap </summary>
        public Bitmap ByteToBitmap(byte[] data, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);
            Marshal.Copy(data, 0, bmpData.Scan0, data.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        /// <summary> 計算Bitmap有多少個Alpha = 0 </summary>
        public int AlphaZeroCount(Bitmap bitmap)
        {
            int count = 0;
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    Color c = bitmap.GetPixel(i, j);
                    if (c.A == 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary> 輸入原始數字，與要隱藏的0或1 </summary>
        public int Mode2_Encrypt(int input, int hidden)
        {
            if (input >= 0 && input <= 255)
            {
                int output = input;
                int m = input % 2;
                if (m != hidden)
                {
                    //是255時，需要變化時-1
                    if (output == 255) { output--; }
                    //是0時，需要變化時+1
                    else { output++; }
                }
                return output;
            }

            return -1;
        }

        /// <summary>給出0或1，代表隱藏字</summary>
        public int Mode2_Decrypt(int input)
        {
            return input % 2;
        }
    }
}
