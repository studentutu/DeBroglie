﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;

namespace DeBroglie.Console
{
    public static class BitmapUtils
    {
        // TODO: Get PixelSpan?
        public static Rgba32[,] ToColorArray(Image<Rgba32> bitmap)
        {
            Rgba32[,] sample = new Rgba32[bitmap.Width, bitmap.Height];
            for (var x = 0; x < bitmap.Width; x++)
            {
                for (var y = 0; y < bitmap.Height; y++)
                {
                    sample[x, y] = bitmap[x, y];
                }
            }
            return sample;
        }

        // TODO: Load Pixel Data
        public static Image<Rgba32> ToBitmap(Rgba32[,] colorArray)
        {
            var bitmap = new Image<Rgba32>(colorArray.GetLength(0), colorArray.GetLength(1));
            for (var x = 0; x < bitmap.Width; x++)
            {
                for (var y = 0; y < bitmap.Height; y++)
                {
                    bitmap[x, y] = colorArray[x, y];
                }
            }
            return bitmap;
        }

        public static Rgba32 Overlay(Rgba32 bg, Rgba32 fg)
        {
            return new Rgba32(
                (byte)(fg.R / 255f * fg.A + bg.R / 255f * (255 - fg.A)),
                (byte)(fg.G / 255f * fg.A + bg.G / 255f * (255 - fg.A)),
                (byte)(fg.B / 255f * fg.A + bg.B / 255f * (255 - fg.A)),
                (byte)255
                );
        }

        public static Image<Rgba32> Slice(Image<Rgba32> b, int x, int y, int width, int height)
        {
            var newImage = new Image<Rgba32>(width, height);
            Blit(newImage, b, 0, 0, x, y, width, height);
            return newImage;
        }

        public static void Blit(Image<Rgba32> dest, Image<Rgba32> src, int destX, int destY, int srcX, int srcY, int width, int height)
        {
            // TODO: Seriously, is this the best way in ImageSharp?
            var subImage = src.Clone(c => c.Crop(new Rectangle(srcX, srcY, width, height)));    
            dest.Mutate(c => c.DrawImage(subImage, new SixLabors.ImageSharp.Point(destX, destY), 1.0f));
        }
    }
}
