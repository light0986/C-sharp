using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorRNN
{
    public class RNN
    {
        //不轉Matrix訓練法:
        //a.中心值為:0,127,127,127
        //b.範圍為0~255
        //c.學習量太大，梯度消失會很明顯

        public void TrainPositive(ref RNNData rNN, Bitmap input, string Answer, double Learningrate)
        {
            if (Learningrate > 1 && Learningrate < 0)
            {
                throw new InvalidOperationException("Learningrate 必須在 0~1 之間");
            }
            int AnserIndex = rNN.Models.FindIndex(x => x.Name == Answer);
            MyBitmap inner = rNN.Models[AnserIndex].Bitmap;

            if (input.Size != inner.Size)
            {
                throw new InvalidOperationException("訓練圖與原圖大小不同");
            }

            for (int h = 0; h < input.Height; h++)
            {
                for (int w = 0; w < input.Width; w++)
                {
                    Color inputColor = input.GetPixel(w, h); //訓練資料
                    double innerR = inner.GetPositivePixel(w, h, MyBitmap.RGBA.R);
                    double innerG = inner.GetPositivePixel(w, h, MyBitmap.RGBA.G);
                    double innerB = inner.GetPositivePixel(w, h, MyBitmap.RGBA.B);

                    int A = inputColor.A;
                    if (A != 0)
                    {
                        double R = (inputColor.R - innerR) * Learningrate;
                        double G = (inputColor.G - innerG) * Learningrate;
                        double B = (inputColor.B - innerB) * Learningrate;

                        innerR += R;
                        innerG += G;
                        innerB += B;

                        inner.SetPixel(w, h, 255, innerR, innerG, innerB);
                    }
                }
            }

            //rNN.models[AnserIndex].bitmap = inner;
        }

        public void TrainNegtive(ref RNNData rNN, int TrainIndex, MyBitmap input, double Learningrate)
        {
            if (Learningrate > 1 && Learningrate < 0)
            {
                throw new InvalidOperationException("Learningrate 必須在 0~1 之間");
            }

            MyBitmap inner = rNN.Models[TrainIndex].Bitmap;

            for (int h = 0; h < input.Height; h++)
            {
                for (int w = 0; w < input.Width; w++)
                {
                    double inputA = input.GetPositivePixel(w, h, MyBitmap.RGBA.A);
                    if (inputA != 0)
                    {
                        double inputR = input.GetNegtivePixel(w, h, MyBitmap.RGBA.R);
                        double inputG = input.GetNegtivePixel(w, h, MyBitmap.RGBA.G);
                        double inputB = input.GetNegtivePixel(w, h, MyBitmap.RGBA.B);

                        double innerR = inner.GetPositivePixel(w, h, MyBitmap.RGBA.R);
                        double innerG = inner.GetPositivePixel(w, h, MyBitmap.RGBA.G);
                        double innerB = inner.GetPositivePixel(w, h, MyBitmap.RGBA.B);

                        double R = (inputR - innerR) * Learningrate;
                        double G = (inputG - innerG) * Learningrate;
                        double B = (inputB - innerB) * Learningrate;

                        innerR += R;
                        innerG += G;
                        innerB += B;

                        inner.SetPixel(w, h, 255, innerR, innerG, innerB);
                    }
                }
            }
        }

        public string Query(RNNData rNN, Bitmap input)
        {
            List<double> Scores = new List<double>();
            foreach (RNNData.DataModel r in rNN.Models)
            {
                double score = 0;
                for (int h = 0; h < input.Height; h++)
                {
                    for (int w = 0; w < input.Width; w++)
                    {
                        Color inputColor = input.GetPixel(w, h);
                        double innerR = r.Bitmap.GetPositivePixel(w, h, MyBitmap.RGBA.R);
                        double innerG = r.Bitmap.GetPositivePixel(w, h, MyBitmap.RGBA.G);
                        double innerB = r.Bitmap.GetPositivePixel(w, h, MyBitmap.RGBA.B);

                        if (inputColor.A != 0)
                        {
                            //最大距離255
                            double R = inputColor.R - innerR;
                            double G = inputColor.G - innerG;
                            double B = inputColor.B - innerB;

                            score += (R * R) + (B * B) + (G * G);
                        }
                    }
                }
                Scores.Add(score);
            }

            int ans = Scores.IndexOf(Scores.Min());
            return rNN.Models[ans].Name;
        }
    }

    public class RNNData
    {
        public List<DataModel> Models { get; set; }

        public RNNData(string[] Anwers, Size size)
        {
            Models = new List<DataModel>();
            for (int i = 0; i < Anwers.Length; i++)
            {
                MyBitmap b = new MyBitmap(size.Width, size.Height);
                Models.Add(new DataModel() { Name = Anwers[i], Bitmap = b });
            }
        }

        public class DataModel
        {
            public string Name { get; set; }

            public MyBitmap Bitmap { get; set; }
        }
    }

    public class MyBitmap
    {
        #region Variable
        public Size Size => new Size(Width, Height);
        public int Width => Positive.Width;
        public int Height => Positive.Height;
        public enum RGBA { R, G, B, A }
        public Bitmap Positive { get; set; }
        public Bitmap Negtive { get; set; }
        private double[,] R_Matrix { get; set; }
        private double[,] G_Matrix { get; set; }
        private double[,] B_Matrix { get; set; }
        private int[,] A_Matrix { get; set; }
        #endregion

        #region Constructor
        public MyBitmap(int width, int height)
        {
            Positive = new Bitmap(width, height);
            using (Graphics gfx = Graphics.FromImage(Positive))
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 127, 127, 127)))
                {
                    gfx.FillRectangle(brush, 0, 0, width, height);
                }
            }
            Negtive = new Bitmap(Width, Height);
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    Color color = Positive.GetPixel(i, j);
                    Negtive.SetPixel(i, j, Color.FromArgb(0, 255 - color.R, 255 - color.G, 255 - color.B));
                }
            }

            R_Matrix = new double[width, height];
            G_Matrix = new double[width, height];
            B_Matrix = new double[width, height];
            A_Matrix = new int[width, height];

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    R_Matrix[i, j] = 127.5;
                    G_Matrix[i, j] = 127.5;
                    B_Matrix[i, j] = 127.5;
                    A_Matrix[i, j] = 0;
                }
            }
        }

        public MyBitmap(Size size)
        {
            Positive = new Bitmap(size.Width, size.Height);
            using (Graphics gfx = Graphics.FromImage(Positive))
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 127, 127, 127)))
                {
                    gfx.FillRectangle(brush, 0, 0, size.Width, size.Height);
                }
            }
            Negtive = new Bitmap(Width, Height);
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    Color color = Positive.GetPixel(i, j);
                    Negtive.SetPixel(i, j, Color.FromArgb(0, 255 - color.R, 255 - color.G, 255 - color.B));
                }
            }


            R_Matrix = new double[Width, Height];
            G_Matrix = new double[Width, Height];
            B_Matrix = new double[Width, Height];
            A_Matrix = new int[Width, Height];

            for (int i = 0; i < size.Width; i++)
            {
                for (int j = 0; j < size.Height; j++)
                {
                    R_Matrix[i, j] = 127;
                    G_Matrix[i, j] = 127;
                    B_Matrix[i, j] = 127;
                    A_Matrix[i, j] = 0;
                }
            }
        }

        public MyBitmap(string Path)
        {
            Positive = new Bitmap(Path);
            Negtive = new Bitmap(Width, Height);
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    Color color = Positive.GetPixel(i, j);
                    Negtive.SetPixel(i, j, Color.FromArgb(0, 255 - color.R, 255 - color.G, 255 - color.B));
                }
            }

            R_Matrix = new double[Width, Height];
            G_Matrix = new double[Width, Height];
            B_Matrix = new double[Width, Height];
            A_Matrix = new int[Width, Height];

            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    Color color = Positive.GetPixel(i, j);
                    R_Matrix[i, j] = color.R;
                    G_Matrix[i, j] = color.G;
                    B_Matrix[i, j] = color.B;
                    A_Matrix[i, j] = color.A;
                }
            }
        }

        public MyBitmap(Bitmap bitmap)
        {
            Positive = bitmap;
            Negtive = new Bitmap(Width, Height);
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    Color color = Positive.GetPixel(i, j);
                    Negtive.SetPixel(i, j, Color.FromArgb(0, 255 - color.R, 255 - color.G, 255 - color.B));
                }
            }

            R_Matrix = new double[Width, Height];
            G_Matrix = new double[Width, Height];
            B_Matrix = new double[Width, Height];
            A_Matrix = new int[Width, Height];

            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    Color color = Positive.GetPixel(i, j);
                    R_Matrix[i, j] = color.R;
                    G_Matrix[i, j] = color.G;
                    B_Matrix[i, j] = color.B;
                    A_Matrix[i, j] = color.A;
                }
            }
        }
        #endregion

        #region Functions
        public MyBitmap Clone()
        {
            return new MyBitmap(Positive);
        }

        public Bitmap CloneBitmap()
        {
            return new Bitmap(Positive);
        }

        public Bitmap CloneBitmap(RectangleF rect, PixelFormat format)
        {
            return Positive.Clone(rect, format);
        }

        public Bitmap CloneBitmap(Rectangle rect, PixelFormat format)
        {
            return Positive.Clone(rect, format);
        }

        public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format)
        {
            return Positive.LockBits(rect, flags, format);
        }

        public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format, BitmapData bitmapData)
        {
            return Positive.LockBits(rect, flags, format, bitmapData);
        }

        public void UnlockBits(BitmapData bitmapdata)
        {
            Positive.UnlockBits(bitmapdata);
        }
        #endregion

        #region Positive
        public Color GetPositivePixel(int x, int y)
        {
            return Color.FromArgb((int)A_Matrix[x, y], (int)R_Matrix[x, y], (int)G_Matrix[x, y], (int)B_Matrix[x, y]);
        }

        public double GetPositivePixel(int x, int y, RGBA color)
        {
            if (color == RGBA.R) { return R_Matrix[x, y]; }
            else if (color == RGBA.G) { return G_Matrix[x, y]; }
            else if (color == RGBA.B) { return B_Matrix[x, y]; }
            else { return A_Matrix[x, y]; }
        }

        public void SetPixel(int x, int y, Color color)
        {
            Positive.SetPixel(x, y, color);
            Color n = Color.FromArgb(color.A, 255 - color.R, 255 - color.G, 255 - color.B);
            Negtive.SetPixel(x, y, n);

            R_Matrix[x, y] = color.R;
            G_Matrix[x, y] = color.G;
            B_Matrix[x, y] = color.B;
            A_Matrix[x, y] = color.A;
        }

        public void SetPixel(int x, int y, double A, double R, double G, double B)
        {
            if (A > 255) { A = 255; } else if (A < 0) { A = 0; }
            if (R > 255) { R = 255; } else if (R < 0) { R = 0; }
            if (G > 255) { G = 255; } else if (G < 0) { G = 0; }
            if (B > 255) { B = 255; } else if (B < 0) { B = 0; }

            Color color = Color.FromArgb((int)A, (int)R, (int)G, (int)B);
            Positive.SetPixel(x, y, color);

            Color color2 = Color.FromArgb((int)A, (int)(255 - R), (int)(255 - G), (int)(255 - B));
            Negtive.SetPixel(x, y, color2);

            R_Matrix[x, y] = R;
            G_Matrix[x, y] = G;
            B_Matrix[x, y] = B;
            A_Matrix[x, y] = (int)A;
        }
        #endregion

        #region Negtive
        public Color GetNegtivePixel(int x, int y)
        {
            return Color.FromArgb((int)A_Matrix[x, y], (int)(255 - R_Matrix[x, y]), (int)(255 - G_Matrix[x, y]), (int)(255 - B_Matrix[x, y]));
        }

        public double GetNegtivePixel(int x, int y, RGBA color)
        {
            if (color == RGBA.R) { return 255 - R_Matrix[x, y]; }
            else if (color == RGBA.G) { return 255 - G_Matrix[x, y]; }
            else if (color == RGBA.B) { return 255 - B_Matrix[x, y]; }
            else { return A_Matrix[x, y]; }
        }
        #endregion
    }
}
