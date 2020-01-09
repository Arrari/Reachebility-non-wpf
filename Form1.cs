using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using System.Globalization;
using ClipperLib;



namespace Reachebility_non_wpf
{

    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;
    public partial class Form1 : Form
    {
        private float scale = 100;
        private Bitmap mybitmap;
        private Polygons subjects = new Polygons();
        private Polygons clips = new Polygons();
        private Polygons solution = new Polygons();
        class SVGBuilder
        {
            public class StyleInfo
            {
                public PolyFillType pft;
                public Color brushClr;
                public Color penClr;
                public double penWidth;
                public int[] dashArray;
                public Boolean showCoords;
                public StyleInfo Clone()
                {
                    StyleInfo si = new StyleInfo();
                    si.pft = this.pft;
                    si.brushClr = this.brushClr;
                    si.dashArray = this.dashArray;
                    si.penClr = this.penClr;
                    si.penWidth = this.penWidth;
                    si.showCoords = this.showCoords;
                    return si;
                }
                public StyleInfo()
                {
                    pft = PolyFillType.pftNonZero;
                    brushClr = Color.AntiqueWhite;
                    dashArray = null;
                    penClr = Color.Black;
                    penWidth = 0.8;
                    showCoords = false;
                }
            }

            public class PolyInfo
            {
                public Polygons polygons;
                public StyleInfo si;
            }

            public StyleInfo style;
            private List<PolyInfo> PolyInfoList;
            const string svg_header = "<?xml version=\"1.0\" standalone=\"no\"?>\n" +
              "<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.0//EN\"\n" +
              "\"http://www.w3.org/TR/2001/REC-SVG-20010904/DTD/svg10.dtd\">\n\n" +
              "<svg width=\"{0}px\" height=\"{1}px\" viewBox=\"0 0 {2} {3}\" " +
              "version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\">\n\n";
            const string svg_path_format = "\"\n style=\"fill:{0};" +
                " fill-opacity:{1:f2}; fill-rule:{2}; stroke:{3};" +
                " stroke-opacity:{4:f2}; stroke-width:{5:f2};\"/>\n\n";

            public SVGBuilder()
            {
                PolyInfoList = new List<PolyInfo>();
                style = new StyleInfo();
            }

            public void AddPolygons(Polygons poly)
            {
                if (poly.Count == 0) return;
                PolyInfo pi = new PolyInfo();
                pi.polygons = poly;
                pi.si = style.Clone();
                PolyInfoList.Add(pi);
            }

            public Boolean SaveToFile(string filename, double scale = 1.0, int margin = 10)
            {
                if (scale == 0) scale = 1.0;
                if (margin < 0) margin = 0;

                //calculate the bounding rect ...
                int i = 0, j = 0;
                while (i < PolyInfoList.Count)
                {
                    j = 0;
                    while (j < PolyInfoList[i].polygons.Count &&
                        PolyInfoList[i].polygons[j].Count == 0) j++;
                    if (j < PolyInfoList[i].polygons.Count) break;
                    i++;
                }
                if (i == PolyInfoList.Count) return false;
                IntRect rec = new IntRect();
                rec.left = PolyInfoList[i].polygons[j][0].X;
                rec.right = rec.left;
                rec.top = PolyInfoList[0].polygons[j][0].Y;
                rec.bottom = rec.top;

                for (; i < PolyInfoList.Count; i++)
                {
                    foreach (Polygon pg in PolyInfoList[i].polygons)
                        foreach (IntPoint pt in pg)
                        {
                            if (pt.X < rec.left) rec.left = pt.X;
                            else if (pt.X > rec.right) rec.right = pt.X;
                            if (pt.Y < rec.top) rec.top = pt.Y;
                            else if (pt.Y > rec.bottom) rec.bottom = pt.Y;
                        }
                }

                rec.left = (Int64)(rec.left * scale);
                rec.top = (Int64)(rec.top * scale);
                rec.right = (Int64)(rec.right * scale);
                rec.bottom = (Int64)(rec.bottom * scale);
                Int64 offsetX = -rec.left + margin;
                Int64 offsetY = -rec.top + margin;

                using (StreamWriter writer = new StreamWriter(filename))
                {
                    writer.Write(svg_header,
                        (rec.right - rec.left) + margin * 2,
                        (rec.bottom - rec.top) + margin * 2,
                        (rec.right - rec.left) + margin * 2,
                        (rec.bottom - rec.top) + margin * 2);

                    foreach (PolyInfo pi in PolyInfoList)
                    {
                        writer.Write(" <path d=\"");
                        foreach (Polygon p in pi.polygons)
                        {
                            if (p.Count < 3) continue;
                            writer.Write(String.Format(NumberFormatInfo.InvariantInfo, " M {0:f2} {1:f2}",
                                (double)((double)p[0].X * scale + offsetX),
                                (double)((double)p[0].Y * scale + offsetY)));
                            for (int k = 1; k < p.Count; k++)
                            {
                                writer.Write(String.Format(NumberFormatInfo.InvariantInfo, " L {0:f2} {1:f2}",
                                (double)((double)p[k].X * scale + offsetX),
                                (double)((double)p[k].Y * scale + offsetY)));
                            }
                            writer.Write(" z");
                        }

                        writer.Write(String.Format(NumberFormatInfo.InvariantInfo, svg_path_format,
                        ColorTranslator.ToHtml(pi.si.brushClr),
                        (float)pi.si.brushClr.A / 255,
                        (pi.si.pft == PolyFillType.pftEvenOdd ? "evenodd" : "nonzero"),
                        ColorTranslator.ToHtml(pi.si.penClr),
                        (float)pi.si.penClr.A / 255,
                        pi.si.penWidth));

                        if (pi.si.showCoords)
                        {
                            writer.Write("<g font-family=\"Verdana\" font-size=\"11\" fill=\"black\">\n\n");
                            foreach (Polygon p in pi.polygons)
                            {
                                foreach (IntPoint pt in p)
                                {
                                    Int64 x = pt.X;
                                    Int64 y = pt.Y;
                                    writer.Write(String.Format(
                                        "<text x=\"{0}\" y=\"{1}\">{2},{3}</text>\n",
                                        (int)(x * scale + offsetX), (int)(y * scale + offsetY), x, y));

                                }
                                writer.Write("\n");
                            }
                            writer.Write("</g>\n");
                        }
                    }
                    writer.Write("</svg>\n");
                }
                return true;
            }
        }

        public Form1()
        {
            InitializeComponent();
            mybitmap = new Bitmap(pictureBox1.ClientRectangle.Width,
              pictureBox1.ClientRectangle.Height,
              PixelFormat.Format32bppArgb);
        }

        private void bRefresh_Click(object sender, EventArgs e)
        {
            DrawBitmap();
        }
        static private PointF[] PolygonToPointFArray(Polygon pg, float scale)
        {
            PointF[] result = new PointF[pg.Count];
            for (int i = 0; i < pg.Count; ++i)
            {
                result[i].X = (float)pg[i].X / scale;
                result[i].Y = (float)pg[i].Y / scale;
            }
            return result;
        }
        private IntPoint GenerateRandomPoint(int l, int t, int r, int b, Random rand)
        {
            int Q = 10;
            return new IntPoint(
              Convert.ToInt64((rand.Next(r / Q) * Q + l + 10) * scale),
              Convert.ToInt64((rand.Next(b / Q) * Q + t + 10) * scale));
        }
        private void GenerateRandomPolygon(int count)
        {
            int Q = 10;
            Random rand = new Random();
            int l = 10;
            int t = 10;
            int r = (pictureBox1.ClientRectangle.Width - 20) / Q * Q;
            int b = (pictureBox1.ClientRectangle.Height - 20) / Q * Q;

            subjects.Clear();
            clips.Clear();

            Polygon subj = new Polygon(count);
            for (int i = 0; i < count; ++i)
                subj.Add(GenerateRandomPoint(l, t, r, b, rand));
            subjects.Add(subj);

            Polygon clip = new Polygon(count);
            for (int i = 0; i < count; ++i)
                clip.Add(GenerateRandomPoint(l, t, r, b, rand));
            clips.Add(clip);

        }

        private void testpoly()
        {
            Polygons subj = new Polygons(2);
            subj.Add(new Polygon(4));
            subj[0].Add(new IntPoint(180, 200));
            subj[0].Add(new IntPoint(260, 200));
            subj[0].Add(new IntPoint(260, 150));
            subj[0].Add(new IntPoint(180, 150));
        }

        private void DrawBitmap(bool justClip = false)
        {

            pictureBox1.SizeMode = PictureBoxSizeMode.AutoSize;
            pictureBox1.Location = new Point(0, 0);
            Bitmap bm = new Bitmap(280, 110);
            using (Graphics gr = Graphics.FromImage(bm))
            {
                gr.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle rect = new Rectangle(10, 10, 260, 90);
                gr.FillEllipse(Brushes.LightGreen, rect);
                using (Pen thick_pen = new Pen(Color.Blue, 5))
                {
                    gr.DrawEllipse(thick_pen, rect);
                }
            }

            pictureBox1.Image = bm;
            /* try
             {

                 GenerateRandomPolygon((int)nudCount.Value); 
                 using (Graphics newgraphic = Graphics.FromImage(mybitmap))
                 using (GraphicsPath path = new GraphicsPath())
                 {
                     newgraphic.SmoothingMode = SmoothingMode.AntiAlias;
                     newgraphic.Clear(Color.White);
                     foreach (Polygon pg in subjects)
                     {
                         PointF[] pts = PolygonToPointFArray(pg, scale);
                         path.AddPolygon(pts);
                         pts = null;
                     }

                     using (Pen myPen = new Pen(Color.FromArgb(196, 0xC3, 0xC9, 0xCF), (float)0.6))
                     using (SolidBrush myBrush = new SolidBrush(Color.FromArgb(127, 0xDD, 0xDD, 0xF0)))
                     {
                         newgraphic.FillPath(myBrush, path);
                         newgraphic.DrawPath(myPen, path);
                         path.Reset();
                     }


                 }
                 pictureBox1.Image = mybitmap;
             }
             finally
             {
                 Cursor.Current = Cursors.Default;
             }
             */

        }
        private void Form1_Load(object sender, EventArgs e)
        {

            DrawBitmap();
        }


        private void nudCount_ValueChanged(object sender, EventArgs e)
        {
            DrawBitmap(true);
        }
    }
}



 
          