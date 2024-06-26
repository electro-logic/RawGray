﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RawGray
{
    public unsafe partial class MainWindow : Window
    {
        BitmapDecoder _bmpDecoder;
        WriteableBitmap _wb;
        string _currentFilename;
        public MainWindow() => InitializeComponent();
        void UpdateImage(string image = null)
        {
            if (!IsInitialized)
                return;

            if (image != null)
            {
                if (Path.GetExtension(image).ToLower() == ".arw")
                {
                    Debug.WriteLine("Converting ARW to TIFF");
                    var proc = new Process()
                    {
                        StartInfo = new ProcessStartInfo("dcraw_emu.exe", $"-T -4 -disinterp -o 0 {image}")
                        {
                            CreateNoWindow = true
                        }
                    };
                    proc.Start();
                    proc.WaitForExit();
                    image += ".tiff";
                }
            }

            // Change values
            var gamma = sliderGamma.Value;
            var r = sliderR.Value;
            var g1 = sliderG1.Value;
            var g2 = sliderG2.Value;
            var b = sliderB.Value;
            if (image != null)
            {
                _currentFilename = image;
                Title = "RawGray " + _currentFilename;
                _bmpDecoder = BitmapDecoder.Create(new Uri(image, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            }
            else
            {
                if (_bmpDecoder == null)
                {
                    return;
                }
            }
            viewbox.Width = _bmpDecoder.Frames[0].PixelWidth * sliderZoom.Value;
            viewbox.Height = _bmpDecoder.Frames[0].PixelHeight * sliderZoom.Value;
            BitmapSource bmpSource = _bmpDecoder.Frames[0];
            _wb = new WriteableBitmap(bmpSource);
            if (_wb.Format.BitsPerPixel == 48)
            {
                _wb = RGGB_Rgb48_to_Gray16(_wb);
            }
            if (_wb.Format.BitsPerPixel != 16)
            {
                MessageBox.Show($"Image is not Gray16 or RGB48");
                return;
            }
            _wb.Lock();
            var w = _wb.PixelWidth;
            var h = _wb.PixelHeight;
            UInt16* bb = (UInt16*)_wb.BackBuffer.ToPointer();
            Debug.WriteLine("Converting Gamma");
            // RGGB
            Parallel.For(0, w, (colIndex) =>
            {
                for (int rowIndex = 0; rowIndex < h; rowIndex++)
                {
                    // Adjust value in linear space
                    int index = (rowIndex * w) + colIndex;

                    // Convert to Gamma space (1->2.2)
                    double val = bb[index] / (double)UInt16.MaxValue;
                    bb[index] = (UInt16)(Math.Pow(val, 1.0 / gamma) * UInt16.MaxValue);

                    if ((colIndex % 2) == 0)
                    {
                        // R/G1
                        if ((rowIndex % 2) == 0)
                        {
                            // R
                            bb[index] = (UInt16)(r * bb[index]);
                        }
                        else
                        {
                            // G1
                            bb[index] = (UInt16)(g2 * bb[index]);
                        }
                    }
                    else
                    {
                        // G2/B
                        if ((rowIndex % 2) == 0)
                        {
                            // G2
                            bb[index] = (UInt16)(g1 * bb[index]);
                        }
                        else
                        {
                            // B
                            bb[index] = (UInt16)(b * bb[index]);
                        }
                    }
                    //// Convert to Gamma space (1->2.2)
                    //double val = bb[index] / (double)UInt16.MaxValue;
                    //bb[index] = (UInt16)(Math.Pow(val, 1.0 / gamma) * UInt16.MaxValue);
                }
            });
            _wb.AddDirtyRect(new Int32Rect(0, 0, w, h));
            _wb.Unlock();
            img.Source = _wb;
        }
        WriteableBitmap RGGB_Rgb48_to_Gray16(WriteableBitmap inBmp)
        {
            Debug.WriteLine("Converting RGB48 to Gray16");
            var w = inBmp.PixelWidth;
            var h = inBmp.PixelHeight;
            var outBmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Gray16, null);
            outBmp.Lock();
            UInt16* outBmpBuffer = (UInt16*)outBmp.BackBuffer.ToPointer();
            UInt16* inBmpBuffer = (UInt16*)inBmp.BackBuffer.ToPointer();
            Parallel.For(0, w, (colIndex) =>
            {
                for (int rowIndex = 0; rowIndex < h; rowIndex++)
                {
                    // Adjust value in linear space
                    int index = (rowIndex * w) + colIndex;
                    if ((colIndex % 2) == 0)
                    {
                        // R/G1
                        if ((rowIndex % 2) == 0)
                        {
                            // R
                            outBmpBuffer[index] = (UInt16)(inBmpBuffer[index * 3]);
                        }
                        else
                        {
                            // G1
                            outBmpBuffer[index] = (UInt16)(inBmpBuffer[index * 3 + 1]);
                        }
                    }
                    else
                    {
                        // G2/B
                        if ((rowIndex % 2) == 0)
                        {
                            // G2
                            outBmpBuffer[index] = (UInt16)(inBmpBuffer[index * 3 + 1]);
                        }
                        else
                        {
                            // B
                            outBmpBuffer[index] = (UInt16)(inBmpBuffer[index * 3 + 2]);
                        }
                    }
                }
            });
            outBmp.AddDirtyRect(new Int32Rect(0, 0, w, h));
            outBmp.Unlock();
            return outBmp;
        }
        void channels_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateImage();
        void zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateImage();
        void btnBatchExport_Click(object sender, RoutedEventArgs e)
        {
            var inputFolderDialog = new System.Windows.Forms.FolderBrowserDialog() { Description = "Input folder" };
            if (inputFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var outputFolderDialog = new System.Windows.Forms.FolderBrowserDialog() { Description = "Output folder" };
                if (outputFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var files = Directory.EnumerateFiles(inputFolderDialog.SelectedPath, "*");
                    foreach (var file in files)
                    {
                        UpdateImage(file);
                        ExportChannels(outputFolderDialog.SelectedPath);
                        // Save Gray16 image
                        var encoder = new TiffBitmapEncoder();
                        encoder.Compression = TiffCompressOption.None;
                        var frame = BitmapFrame.Create(_wb);
                        encoder.Frames.Add(frame);
                        using (var fs = new FileStream(Path.Combine(outputFolderDialog.SelectedPath, Path.GetFileName(file)), FileMode.Create))
                        {
                            encoder.Save(fs);
                        }
                    }
                }
            }
            MessageBox.Show("Done");
        }
        void btnOpenImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image|*.tif;*.tiff;*.png;*.jpg|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string filename = openFileDialog.FileName;
                UpdateImage(filename);
            }
        }
        void btnResetGains_Click(object sender, RoutedEventArgs e)
        {
            sliderR.Value = 1.0;
            sliderG1.Value = 1.0;
            sliderG2.Value = 1.0;
            sliderB.Value = 1.0;
            UpdateImage();
        }
        void btnExportChannels_Click(object sender, RoutedEventArgs e)
        {
            var outputFolderDialog = new System.Windows.Forms.FolderBrowserDialog() { Description = "Output folder" };
            if (outputFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var folder = outputFolderDialog.SelectedPath;
                ExportChannels(folder);
            }
            MessageBox.Show("Done");
        }
        void ExportChannels(string folder)
        {
            WriteableBitmap rBmp, g1Bmp, g2Bmp, bBmp;
            ExtractChannels(_currentFilename, out rBmp, out g1Bmp, out g2Bmp, out bBmp);
            var name = Path.GetFileNameWithoutExtension(_currentFilename);
            SaveTiff(rBmp, @$"{folder}\{name}_r.tif");
            SaveTiff(g1Bmp, @$"{folder}\{name}_g1.tif");
            SaveTiff(g2Bmp, @$"{folder}\{name}_g2.tif");
            SaveTiff(bBmp, @$"{folder}\{name}_b.tif");
        }
        void SaveTiff(WriteableBitmap wb, string outFile)
        {
            var encoder = new TiffBitmapEncoder() { Compression = TiffCompressOption.None };
            var frame = BitmapFrame.Create(wb);
            encoder.Frames.Add(frame);
            using (var fs = new FileStream(outFile, FileMode.Create))
            {
                encoder.Save(fs);
            }
        }
        void ExtractChannels(string image, out WriteableBitmap rBmp, out WriteableBitmap g1Bmp, out WriteableBitmap g2Bmp, out WriteableBitmap bBmp)
        {
            //_bmpDecoder = BitmapDecoder.Create(new Uri(image, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            UpdateImage(image);

            // Change values
            var gamma = sliderGamma.Value;
            var gainR = sliderR.Value;
            var gainG1 = sliderG1.Value;
            var gainG2 = sliderG2.Value;
            var gainB = sliderB.Value;

            //_bmpDecoder = BitmapDecoder.Create(new Uri(image, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            //var frame = _bmpDecoder.Frames[0];
            var frame = _wb;
            var w = frame.PixelWidth;
            var h = frame.PixelHeight;

            rBmp = new WriteableBitmap(w / 2, h / 2, frame.DpiX, frame.DpiY, frame.Format, null);
            g1Bmp = new WriteableBitmap(w / 2, h / 2, frame.DpiX, frame.DpiY, frame.Format, null);
            g2Bmp = new WriteableBitmap(w / 2, h / 2, frame.DpiX, frame.DpiY, frame.Format, null);
            bBmp = new WriteableBitmap(w / 2, h / 2, frame.DpiX, frame.DpiY, frame.Format, null);
            //_wb = new WriteableBitmap(_bmpDecoder.Frames[0]);
            _wb.Lock(); rBmp.Lock(); g1Bmp.Lock(); g2Bmp.Lock(); bBmp.Lock();
            UInt16* ib = (UInt16*)_wb.BackBuffer.ToPointer();
            UInt16* rb = (UInt16*)rBmp.BackBuffer.ToPointer();
            UInt16* g1b = (UInt16*)g1Bmp.BackBuffer.ToPointer();
            UInt16* g2b = (UInt16*)g2Bmp.BackBuffer.ToPointer();
            UInt16* bb = (UInt16*)bBmp.BackBuffer.ToPointer();

            int rIndex = 0; int g1Index = 0; int g2Index = 0; int bIndex = 0;
            // RGGB
            for (int rowIndex = 0; rowIndex < h; rowIndex++)
            {
                for (int colIndex = 0; colIndex < w; colIndex++)
                {
                    // Adjust value in linear space
                    int index = (rowIndex * w) + colIndex;

                    // Convert to Gamma space (1->2.2)
                    double val = ib[index] / (double)UInt16.MaxValue;
                    ib[index] = (UInt16)(Math.Pow(val, 1.0 / gamma) * UInt16.MaxValue);

                    if ((colIndex % 2) == 0)
                    {
                        // R/G1
                        if ((rowIndex % 2) == 0)
                        {
                            // R
                            rb[rIndex] = (UInt16)(gainR * ib[index]);
                            rIndex++;
                        }
                        else
                        {
                            // G1
                            g1b[g1Index] = (UInt16)(gainG2 * ib[index]);
                            g1Index++;
                        }
                    }
                    else
                    {
                        // G2/B
                        if ((rowIndex % 2) == 0)
                        {
                            // G2
                            g2b[g2Index] = (UInt16)(gainG1 * ib[index]);
                            g2Index++;
                        }
                        else
                        {
                            // B
                            bb[bIndex] = (UInt16)(gainB * ib[index]);
                            bIndex++;
                        }
                    }
                }
            }
            rBmp.Unlock(); g1Bmp.Unlock(); g2Bmp.Unlock(); bBmp.Unlock(); _wb.Unlock();
        }
        void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 0)
                    {
                        UpdateImage(files[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}