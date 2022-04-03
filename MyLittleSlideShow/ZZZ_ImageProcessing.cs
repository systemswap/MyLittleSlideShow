using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace ZZZ
{
    class ImageProcessing
    {
        public BitmapImage LoadBitmapImageFromResource(string pathInApplication, Assembly assembly = null)
        {
            if (assembly == null)
            {
                assembly = Assembly.GetCallingAssembly();
            }

            if (pathInApplication[0] == '/')
            {
                pathInApplication = pathInApplication.Substring(1);
            }
            return new BitmapImage(new Uri(@"pack://application:,,,/" + assembly.GetName().Name + ";component/" + pathInApplication, UriKind.Absolute));
        }

        public Bitmap LoadBitmapFromEmbeddedFolder(string FilePath = "Images/MyIcon.png")
        {
            var bitmapImage = new BitmapImage(new Uri(@"pack://application:,,,/" + Assembly.GetExecutingAssembly().GetName().Name + ";component/" + FilePath, UriKind.Absolute));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create((BitmapImage)bitmapImage));
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Flush();
            var image = new System.Drawing.Bitmap(stream);
            return image;
        }

        public BitmapImage ConvertWinFormsImageToWpfBitmapImage(Image img)
        {
            MemoryStream ms = new MemoryStream();  // no using here! BitmapImage will dispose the stream after loading
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

            BitmapImage ix = new BitmapImage();
            //ix.DecodePixelHeight = 255;
            ix.BeginInit();
            ix.CacheOption = BitmapCacheOption.OnLoad;
            ix.StreamSource = ms;
            ix.EndInit();
            return ix;
        }

        /// <summary>
        /// it returns the image Type from the codec
        /// </summary>
        /// <param name="FilePath">The Full FileName with Path</param>
        /// <returns></returns>
        public string GetMimeType(string FilePath)
        {
            string MimeType = "image/unknown";
            FileStream fs = new FileStream(FilePath, FileMode.Open);
            System.Drawing.Image i = System.Drawing.Image.FromStream(fs);
            fs.Dispose();

            try
            {
                foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageDecoders())
                {
                    if (codec.FormatID == i.RawFormat.Guid)
                    {
                        MimeType = codec.MimeType;                        
                        return MimeType;
                    }
                }
            }
            catch (Exception)
            {
                return MimeType;
            }
            finally
            {
                i.Dispose(); 
            }

            return MimeType;            
        }

        public ImageCodecInfo GetEncoderInfo(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        /// <summary>
        /// Convert any Picture to JPEG Format
        /// </summary>
        /// <param name="originalFullPathAndFileName"></param>
        /// <param name="newFullPathAndFileName">This need the Full File Path with name and the extention ".jpg"</param>
        /// <param name="deleteOriginal"></param>
        /// <returns></returns>
        public string ConvertToJpg(string originalFullPathAndFileName, string newFullPathAndFileName, bool deleteOriginal)
        {
            var jpegExtension = ".jpg";
            var path = Path.GetFullPath(originalFullPathAndFileName).Replace(Path.GetFileName(originalFullPathAndFileName), "");
            var originalFileExtension = Path.GetExtension(originalFullPathAndFileName).ToLower();
            var needsConversion = (originalFileExtension != jpegExtension);
            if (!needsConversion) { return originalFullPathAndFileName; }            
            var img = new Bitmap(originalFullPathAndFileName);
            using (var b = new Bitmap(img.Width, img.Height))
            {
                b.SetResolution(img.HorizontalResolution, img.VerticalResolution);
                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(Color.White);
                    g.DrawImageUnscaled(img, 0, 0);
                }
                b.Save(newFullPathAndFileName, ImageFormat.Jpeg);
            }
            img.Dispose();
            // if converted file exists, delete the original
            if (File.Exists(newFullPathAndFileName) && deleteOriginal)
            {
                File.Delete(originalFullPathAndFileName);
            }
            return newFullPathAndFileName;
        }

        public enum ShrinkOrGrow
        {
            Shrink,
            Grow
        }

        public Image ResizeImage(Image sourceImage, int maxWidth, int maxHeight, ShrinkOrGrow whichKindOfResize)
        {
            // Determine which ratio is greater, the width or height, and use
            // this to calculate the new width and height. Effectually constrains
            // the proportions of the resized image to the proportions of the original.
            double xRatio = (double)sourceImage.Width / maxWidth;
            double yRatio = (double)sourceImage.Height / maxHeight;
            double ratioToResizeImage = Math.Max(xRatio, yRatio);
            int newWidth = (int)Math.Floor(sourceImage.Width / ratioToResizeImage);
            int newHeight = (int)Math.Floor(sourceImage.Height / ratioToResizeImage);

            // Create new image canvas -- use maxWidth and maxHeight in this function call if you wish
            // to set the exact dimensions of the output image.
            Bitmap newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            // Render the new image, using a graphic object
            using (Graphics newGraphic = Graphics.FromImage(newImage))
            {
                // Set the background color to be transparent (can change this to any color)
                newGraphic.Clear(System.Drawing.Color.Transparent);

                // Set the method of scaling to use -- HighQualityBicubic for Shrink and HighQualityBilinear to GrowUp is said to have the best quality
                switch (whichKindOfResize)
                {
                    case ShrinkOrGrow.Shrink:
                        newGraphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        break;


                    case ShrinkOrGrow.Grow:
                        newGraphic.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        break;
                }
                
                newGraphic.CompositingMode = CompositingMode.SourceCopy;
                newGraphic.SmoothingMode = SmoothingMode.HighQuality;
                newGraphic.PixelOffsetMode = PixelOffsetMode.HighQuality;
                newGraphic.CompositingQuality = CompositingQuality.HighQuality;

                // Apply the transformation onto the new graphic
                Rectangle sourceDimensions = new Rectangle(0, 0, sourceImage.Width, sourceImage.Height);
                Rectangle destinationDimensions = new Rectangle(0, 0, newWidth, newHeight);
                newGraphic.DrawImage(sourceImage, destinationDimensions, sourceDimensions, GraphicsUnit.Pixel);
            }

            // Image has been modified by all the references to it's related graphic above. Return changes.
            return newImage;
        }

        public enum Rotation
        {
            Left,
            Right
        }

        /// <summary>
        /// Overwrite dosn`t have a Fuction
        /// </summary>
        /// <param name="ImagePath"></param>
        /// <param name="overriteFile"></param>
        /// <param name="whichRotation"></param>
        public void RotateAndSaveImageFile(string ImagePath, bool overriteFile, Rotation whichRotation)
        {
            if (String.IsNullOrEmpty(ImagePath))
            {
                return;
            }

            Image img;
            string fileExtention = string.Empty;

            FileStream fs = new FileStream(ImagePath, FileMode.Open, FileAccess.Read);
            img = Image.FromStream(fs);
            fs.Close();
            fs.Dispose();

            fileExtention = Path.GetExtension(ImagePath);

            RotateFlipType rotateFlipType = new RotateFlipType();
            switch (whichRotation)
            {
                case Rotation.Left:
                    rotateFlipType = RotateFlipType.Rotate270FlipNone;
                    break;


                case Rotation.Right:
                    rotateFlipType = RotateFlipType.Rotate90FlipNone;
                    break;

                default:
                    break;
            }
            



            img.RotateFlip(rotateFlipType);


            ImageCodecInfo myImageCodecInfo;
            Encoder myEncoder;
            EncoderParameter myEncoderParameter;
            EncoderParameters myEncoderParameters;

            switch (fileExtention.ToLower())
            {
                case @".bmp":
                    img.Save(ImagePath, ImageFormat.Bmp);
                    img.Dispose();
                    break;

                case @".gif":
                    img.Save(ImagePath, ImageFormat.Gif);
                    img.Dispose();
                    break;

                case @".ico":
                    img.Save(ImagePath, ImageFormat.Icon);
                    img.Dispose();
                    break;

                case @".jpg":
                case @".jpeg":
                    using (Bitmap bitmap = new Bitmap(img))
                    {
                        myImageCodecInfo = GetEncoderInfo(ImageFormat.Jpeg);
                        myEncoder = Encoder.Quality;
                        myEncoderParameters = new EncoderParameters(1);
                        myEncoderParameter = new EncoderParameter(myEncoder, 100L);
                        myEncoderParameters.Param[0] = myEncoderParameter;
                        img.Save(ImagePath, myImageCodecInfo, myEncoderParameters);
                        img.Dispose();
                    }
                    break;

                case @".png":              
                 img.Save(ImagePath, ImageFormat.Png);
                 img.Dispose();
                 break;

                case @".tif":
                case @".tiff":
                    img.Save(ImagePath, ImageFormat.Tiff);
                    img.Dispose();
                    break;

                case @".wmf":
                    img.Save(ImagePath, ImageFormat.Wmf);
                    img.Dispose();
                    break;

                default:
                    throw new NotImplementedException();
            }
            
        }

 
        public RotateFlipType GetOrientationToFlipType(int orientationValue)
        {
            RotateFlipType rotateFlipType = RotateFlipType.RotateNoneFlipNone;
 
            switch (orientationValue)
            {
                case 1:
                    rotateFlipType = RotateFlipType.RotateNoneFlipNone;
                    break;
                case 2:
                    rotateFlipType = RotateFlipType.RotateNoneFlipX;
                    break;
                case 3:
                    rotateFlipType = RotateFlipType.Rotate180FlipNone;
                    break;
                case 4:
                    rotateFlipType = RotateFlipType.Rotate180FlipX;
                    break;
                case 5:
                    rotateFlipType = RotateFlipType.Rotate90FlipX;
                    break;
                case 6:
                    rotateFlipType = RotateFlipType.Rotate90FlipNone;
                    break;
                case 7:
                    rotateFlipType = RotateFlipType.Rotate270FlipX;
                    break;
                case 8:
                    rotateFlipType = RotateFlipType.Rotate270FlipNone;
                    break;
                default:
                    rotateFlipType = RotateFlipType.RotateNoneFlipNone;
                    break;
            }
 
            return rotateFlipType;
        }
       


        /// <summary>
        /// Sharpens the specified image.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="strength">strength erwartet werte zwische 0 - 99</param>
        /// <returns></returns>
        public Bitmap Sharpen(Image image, whichMatrix welcheMatrix , double strength)
        {
            double FaktorKorrekturWert = 0;

            //strenght muß für den jeweiligen filter angepasst werden
            switch (welcheMatrix)
            {
                case whichMatrix.Gaussian3x3:
                    //diese Matrix benötigt einen strenght Wert von 0 bis -9.9 default ist -2.5
                    //und einen korekturwert von 16
                    strength = (strength * -1) / 10;
                    FaktorKorrekturWert = 16;
                    break;

                case whichMatrix.Mean3x3:
                    //diese Matrix benötigt einen strenght Wert von 0 bis -9 default ist -2.25
                    //und einen Korrekturwert von 10
                    strength = strength * -9 / 100;
                    FaktorKorrekturWert = 10;
                    break;

                case whichMatrix.Gaussian5x5Type1:
                    //diese Matrix benötigt einen strenght Wert von 0 bis 2.5 default ist 1.25
                    //und einen Korrekturwert von 12
                    strength = strength * 2.5 / 100;
                    FaktorKorrekturWert = 12;
                    break;

                default:
                    break;
            }

            using (var bitmap = image as Bitmap)
            {
                if (bitmap != null)
                {
                    var sharpenImage = bitmap.Clone() as Bitmap;

                    int width = image.Width;
                    int height = image.Height;

                    // Create sharpening filter.
                    var filter = Matrix(welcheMatrix);

                    //const int filterSize = 3; // wenn die Matrix 3 Zeilen und 3 Spalten besitzt dann 3 bei 4 = 4 usw.                    
                    int filterSize = filter.GetLength(0);                   

                    double bias = 1.0 - strength;
                    double factor = strength / FaktorKorrekturWert;

                    //const int s = filterSize / 2;
                    int s = filterSize / 2; // Filtersize ist keine Constante mehr darum wurde der befehl const entfernt


                    var result = new Color[image.Width, image.Height];

                    // Lock image bits for read/write.
                    if (sharpenImage != null)
                    {
                        BitmapData pbits = sharpenImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

                        // Declare an array to hold the bytes of the bitmap.
                        int bytes = pbits.Stride * height;
                        var rgbValues = new byte[bytes];

                        // Copy the RGB values into the array.
                        Marshal.Copy(pbits.Scan0, rgbValues, 0, bytes);

                        int rgb;
                        // Fill the color array with the new sharpened color values.
                        for (int x = s; x < width - s; x++)
                        {
                            for (int y = s; y < height - s; y++)
                            {
                                double red = 0.0, green = 0.0, blue = 0.0;

                                for (int filterX = 0; filterX < filterSize; filterX++)
                                {
                                    for (int filterY = 0; filterY < filterSize; filterY++)
                                    {
                                        int imageX = (x - s + filterX + width) % width;
                                        int imageY = (y - s + filterY + height) % height;

                                        rgb = imageY * pbits.Stride + 3 * imageX;

                                        red += rgbValues[rgb + 2] * filter[filterX, filterY];
                                        green += rgbValues[rgb + 1] * filter[filterX, filterY];
                                        blue += rgbValues[rgb + 0] * filter[filterX, filterY];
                                    }

                                    rgb = y * pbits.Stride + 3 * x;

                                    int r = Math.Min(Math.Max((int)(factor * red + (bias * rgbValues[rgb + 2])), 0), 255);
                                    int g = Math.Min(Math.Max((int)(factor * green + (bias * rgbValues[rgb + 1])), 0), 255);
                                    int b = Math.Min(Math.Max((int)(factor * blue + (bias * rgbValues[rgb + 0])), 0), 255);

                                    result[x, y] = System.Drawing.Color.FromArgb(r, g, b);
                                }
                            }
                        }

                        // Update the image with the sharpened pixels.
                        for (int x = s; x < width - s; x++)
                        {
                            for (int y = s; y < height - s; y++)
                            {
                                rgb = y * pbits.Stride + 3 * x;

                                rgbValues[rgb + 2] = result[x, y].R;
                                rgbValues[rgb + 1] = result[x, y].G;
                                rgbValues[rgb + 0] = result[x, y].B;
                            }
                        }

                        // Copy the RGB values back to the bitmap.
                        Marshal.Copy(rgbValues, 0, pbits.Scan0, bytes);
                        // Release image bits.
                        sharpenImage.UnlockBits(pbits);
                    }

                    return sharpenImage;
                }
            }
            return null;
        }


        public enum whichMatrix
        {
            Gaussian3x3,
            Mean3x3,
            Gaussian5x5Type1
        }


        private double[,] Matrix(whichMatrix welcheMatrix)
        {
            double[,] selectedMatrix = null;

            switch (welcheMatrix)
            {
                case whichMatrix.Gaussian3x3:
                    selectedMatrix = new double[,]
                    { 
                        { 1, 2, 1, }, 
                        { 2, 4, 2, }, 
                        { 1, 2, 1, }, 
                    };
                    break;

                case whichMatrix.Gaussian5x5Type1:
                    selectedMatrix = new double[,]
                    { 
                        {-1, -1, -1, -1, -1},
                        {-1,  2,  2,  2, -1},
                        {-1,  2,  16, 2, -1},
                        {-1,  2, -1,  2, -1},
                        {-1, -1, -1, -1, -1} 
                    };
                    break;

                case whichMatrix.Mean3x3:
                    selectedMatrix =new double[,]
                    { 
                        { 1, 1, 1, }, 
                        { 1, 1, 1, }, 
                        { 1, 1, 1, }, 
                    };
                    break;
            }

            return selectedMatrix;
        }
    }
}
