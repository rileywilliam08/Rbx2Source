﻿using System.Collections.Generic;
using System.Drawing;

using Rbx2Source.Geometry;
using Rbx2Source.Reflection;
using Rbx2Source.Web;

namespace Rbx2Source.Textures
{
    public class TextureCompositor
    {
        private List<CompositData> layers = new List<CompositData>();
        private string context = "Humanoid Texture Map";
        private AvatarType avatarType;
        private Rectangle canvas;
        private int composed;

        public Folder CharacterAssets;

        public TextureCompositor(AvatarType at, int width, int height)
        {
            avatarType = at;
            canvas = new Rectangle(0, 0, width, height);
        }

        public TextureCompositor(AvatarType at, Rectangle rect)
        {
            avatarType = at;
            canvas = rect;
        }

        public void AppendColor(int brickColorId, string guide, Rectangle guideSize, byte layer = 0)
        {
            CompositData composit = new CompositData(DrawMode.Guide, DrawType.Color);
            composit.SetGuide(guide, guideSize, avatarType);
            composit.SetDrawColor(brickColorId);
            composit.Layer = layer;

            layers.Add(composit);
        }

        public void AppendTexture(object img, string guide, Rectangle guideSize, byte layer = 0)
        {
            CompositData composit = new CompositData(DrawMode.Guide, DrawType.Texture);
            composit.SetGuide(guide, guideSize, avatarType);
            composit.Texture = img;
            composit.Layer = layer;

            layers.Add(composit);
        }

        public void AppendColor(int brickColorId, Rectangle rect, byte layer = 0)
        {
            CompositData composit = new CompositData(DrawMode.Rect, DrawType.Color);
            composit.SetDrawColor(brickColorId);
            composit.Layer = layer;
            composit.Rect = rect;

            layers.Add(composit);
        }

        public void AppendTexture(object img, Rectangle rect, byte layer = 0, RotateFlipType flipMode = RotateFlipType.RotateNoneFlipNone)
        {
            CompositData composit = new CompositData(DrawMode.Rect, DrawType.Texture);
            composit.FlipMode = flipMode;
            composit.Texture = img;
            composit.Layer = layer;
            composit.Rect = rect;
            
            layers.Add(composit);
        }

        public void SetContext(string newContext)
        {
            context = newContext;
        }

        public Bitmap BakeTextureMap()
        {
            Bitmap bitmap = new Bitmap(canvas.Width, canvas.Height);
            layers.Sort();

            composed = 0;

            Rbx2Source.Print("Composing " + context + "...");
            Rbx2Source.IncrementStack();

            foreach (CompositData composit in layers)
            {
                Graphics buffer = Graphics.FromImage(bitmap);
                Rectangle compositCanvas = composit.Rect;

                DrawMode drawMode = composit.DrawMode;
                DrawType drawType = composit.DrawType;

                if (drawMode == DrawMode.Rect)
                {
                    if (drawType == DrawType.Color)
                    {
                        composit.UseBrush(brush => buffer.FillRectangle(brush, compositCanvas));
                    }
                    else if (drawType == DrawType.Texture)
                    {
                        Bitmap image = composit.GetTextureBitmap();

                        if (composit.FlipMode > 0)
                            image.RotateFlip(composit.FlipMode);

                        buffer.DrawImage(image, compositCanvas);
                    }
                }
                else if (drawMode == DrawMode.Guide)
                {
                    Mesh guide = composit.Guide;

                    for (int face = 0; face < guide.FaceCount; face++)
                    {
                        Vertex[] verts = composit.GetGuideVerts(face);
                        Point offset = compositCanvas.Location;

                        Point vert_a = CompositUtility.VertexToPoint(verts[0], compositCanvas, offset);
                        Point vert_b = CompositUtility.VertexToPoint(verts[1], compositCanvas, offset);
                        Point vert_c = CompositUtility.VertexToPoint(verts[2], compositCanvas, offset);

                        Point[] polygon = new Point[3] { vert_a, vert_b, vert_c };

                        if (drawType == DrawType.Color)
                        {
                            composit.UseBrush(brush => buffer.FillPolygon(brush, polygon));
                        }
                        else if (drawType == DrawType.Texture)
                        {
                            Bitmap texture = composit.GetTextureBitmap();
                            Rectangle bbox = CompositUtility.GetBoundingBox(vert_a, vert_b, vert_c);

                            Point origin = bbox.Location;
                            int width = bbox.Width;
                            int height = bbox.Height;

                            Bitmap drawLayer = new Bitmap(width, height);

                            Point uv_a = CompositUtility.VertexToUV(verts[0], texture);
                            Point uv_b = CompositUtility.VertexToUV(verts[1], texture);
                            Point uv_c = CompositUtility.VertexToUV(verts[2], texture);

                            for (int x = bbox.Left; x < bbox.Right; x++)
                            {
                                for (int y = bbox.Top; y < bbox.Bottom; y++)
                                {
                                    Point pixel = new Point(x, y);
                                    BarycentricPoint bcPixel = CompositUtility.ToBarycentric(pixel, vert_a, vert_b, vert_c);

                                    if (CompositUtility.InTriangle(bcPixel))
                                    {
                                        Point uvPixel = CompositUtility.ToCartesian(bcPixel, uv_a, uv_b, uv_c);
                                        Color color = texture.GetPixel(uvPixel.X, uvPixel.Y);
                                        drawLayer.SetPixel(x - origin.X, y - origin.Y, color);
                                    }
                                }
                            }

                            buffer.DrawImage(drawLayer, origin);
                            drawLayer.Dispose();
                        }
                    }
                }

                Rbx2Source.Print("{0}/{1} layers composed...", ++composed, layers.Count);

                if (layers.Count > 2)
                    Rbx2Source.SetDebugImage(bitmap);

                buffer.Dispose();
            }

            Rbx2Source.Print("Done!");
            Rbx2Source.DecrementStack();

            return bitmap;
        }

        public static Bitmap CropBitmap(Bitmap src, Rectangle crop)
        {
            Bitmap target = new Bitmap(crop.Width, crop.Height);

            using (Graphics graphics = Graphics.FromImage(target))
                graphics.DrawImage(src, -crop.X, -crop.Y);

            return target;
        }

        public Bitmap BakeTextureMap(Rectangle crop)
        {
            Bitmap src = BakeTextureMap();
            return CropBitmap(src, crop);
        }
    }
}