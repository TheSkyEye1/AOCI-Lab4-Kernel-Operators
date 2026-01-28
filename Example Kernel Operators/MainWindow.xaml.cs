using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Emgu.CV.Structure;
using Emgu.CV;
using System.Globalization;
using System.Windows.Controls;
using System.Collections.Generic;

namespace Example_Kernel_Operators
{
    public partial class MainWindow : Window
    {
        private Image<Bgr, byte> sourceImage;
        public MainWindow()
        {
            InitializeComponent();
        }

        public BitmapSource ToBitmapSource(Image<Bgr, byte> image)
        {
            var mat = image.Mat;

            return BitmapSource.Create(
                mat.Width,
                mat.Height,
                96d,
                96d,
                PixelFormats.Bgr24,
                null,
                mat.DataPointer,
                mat.Step * mat.Height,
                mat.Step);
        }
        public Image<Bgr, byte> ToEmguImage(BitmapSource source)
        {
            if (source == null) return null;

            FormatConvertedBitmap safeSource = new FormatConvertedBitmap();
            safeSource.BeginInit();
            safeSource.Source = source;
            safeSource.DestinationFormat = PixelFormats.Bgr24;
            safeSource.EndInit();

            Image<Bgr, byte> resultImage = new Image<Bgr, byte>(safeSource.PixelWidth, safeSource.PixelHeight);
            var mat = resultImage.Mat;

            safeSource.CopyPixels(
                new System.Windows.Int32Rect(0, 0, safeSource.PixelWidth, safeSource.PixelHeight),
                mat.DataPointer,
                mat.Step * mat.Height,
                mat.Step);

            return resultImage;
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Файлы изображений (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png";
            if (openFileDialog.ShowDialog() == true)
            {
                sourceImage = new Image<Bgr, byte>(openFileDialog.FileName);

                MainImage.Source = ToBitmapSource(sourceImage);
            }
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            BitmapSource currentWpfImage = MainImage.Source as BitmapSource;
            if (currentWpfImage == null)
            {
                MessageBox.Show("Отсутсвует изображение");
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp";

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    Image<Bgr, byte> imageToSave = ToEmguImage(currentWpfImage);
                    imageToSave.Save(saveFileDialog.FileName);

                    MessageBox.Show($"Изображение успешно сохранено в {saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {

                    MessageBox.Show($"Ошибка! Не могу сохранить файл. Подробности: {ex.Message}");
                }
            }
        }

        private void UpdateImage_Click(object sender, RoutedEventArgs e)
        {
            BitmapSource currentWpfImage = MainImage.Source as BitmapSource;

            if (currentWpfImage == null)
            {
                MessageBox.Show("Изображение отсутсвует");
                return;
            }

            sourceImage = ToEmguImage(currentWpfImage);
            MessageBox.Show("Изменения применены. Теперь это новый оригинал.");
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (sourceImage == null) return;
            MainImage.Source = ToBitmapSource(sourceImage);
        }


        //Функция применяет операцию свертки к цветному изображению.
        //Свертка — это процесс, где новый цвет каждого пикселя вычисляется как взвешенная сумма цветов его соседей.
        //Веса задаются ядром (матрицей (kernel)). Это основа для множества эффектов: размытия, повышения резкости, выделения границ и т.д.
        private Image<Gray, byte> ApplyConvolution(Image<Gray, byte> input, double[,] kernel)
        {
            int kernelSize = kernel.GetLength(0); //Размер ядра, в нашем случае 3 для матрицы 3x3.
            int kernelRadius = kernelSize / 2; //Радиус ядра, в нашем случае 1 для ядра 3x3.

            //Мы создаем копию изображения, потому что для расчета каждого нового пикселя нужны ОРИГИНАЛЬНЫЕ значения соседних пикселей
            Image<Gray, byte> output = new Image<Gray, byte>(input.Size);

            //Основной цикл проходит по всем пикселям, которые могут быть центром ядра, не выходя за границы изображения. Поэтому мы "пропускаем" края.
            for (int y = kernelRadius; y < input.Height - kernelRadius; y++)
            {
                for (int x = kernelRadius; x < input.Width - kernelRadius; x++)
                {
                    double sum = 0;

                    //Проход по ядру фильтра
                    for (int ky = -kernelRadius; ky <= kernelRadius; ky++)
                    {
                        for (int kx = -kernelRadius; kx <= kernelRadius; kx++)
                        {
                            //Прямой доступ к данным изображения. `[y, x, 0]` - 0 означает первый (и единственный) канал т.к. изображение в градациях серого.
                            byte neighborPixel = input.Data[y + ky, x + kx, 0];

                            //Получаем соответствующее значение (вес) из ядра.
                            double kernelValue = kernel[ky + kernelRadius, kx + kernelRadius];

                            //Умножаем цвет соседа на вес из ядра и прибавляем к общей сумме.
                            sum += neighborPixel * kernelValue;
                        }
                    }

                    //Приводим значение к байту и записываем в выходное изображение
                    output.Data[y, x, 0] = (byte)Math.Max(0, Math.Min(255, sum));
                }
            }
            return output;
        }

        //Считывает матрицу 3x3 из TextBox в Grid.
        private double[,] ReadCustomKernelFromUI()
        {
            double[,] customKernel = new double[3, 3];

            for (int i = 0; i < KernelGrid.Children.Count; i++)
            {
                if (KernelGrid.Children[i] is TextBox textBox)
                {
                    int row = i / 3;
                    int col = i % 3;

                    if (double.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                    {
                        customKernel[row, col] = value;
                    }
                    else
                    {
                        customKernel[row, col] = 0;
                        MessageBox.Show($"Ошибка: в ячейке [{row + 1}, {col + 1}] неверное значение: '{textBox.Text}'. Установлено значение 0.");
                    }
                }
            }

            return customKernel;
        }
    }
}
