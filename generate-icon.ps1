$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class TaskbarArtwork {
    static GraphicsPath Round(float x, float y, float w, float h, float r) {
        GraphicsPath p = new GraphicsPath();
        p.AddArc(x,y,r*2,r*2,180,90); p.AddArc(x+w-r*2,y,r*2,r*2,270,90);
        p.AddArc(x+w-r*2,y+h-r*2,r*2,r*2,0,90); p.AddArc(x,y+h-r*2,r*2,r*2,90,90); p.CloseFigure(); return p;
    }
    static Bitmap Render(int size) {
        Bitmap large = new Bitmap(1024,1024,PixelFormat.Format32bppArgb);
        using(Graphics g = Graphics.FromImage(large)) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(4,4);
            using(var bg = Round(4,4,248,248,58))
            using(var paint = new LinearGradientBrush(new Point(20,0),new Point(220,256),Color.FromArgb(30,42,65),Color.FromArgb(9,13,25))) g.FillPath(paint,bg);
            using(var border = Round(5,5,246,246,57))
            using(var pen = new Pen(Color.FromArgb(65,114,138,171),2)) g.DrawPath(pen,border);
            // A crisp display outline remains recognizable at 16 pixels.
            using(var screen = Round(42,56,172,124,22))
            using(var paint = new LinearGradientBrush(new Point(42,60),new Point(214,174),Color.FromArgb(81,235,244),Color.FromArgb(149,115,255)))
            using(var pen = new Pen(paint,size <= 24 ? 13 : 10)) g.DrawPath(pen,screen);
            // The only bright content inside the screen is its taskbar.
            using(var bar = Round(70,145,116,12,6))
            using(var paint = new LinearGradientBrush(new Point(70,145),new Point(186,157),Color.FromArgb(85,233,241),Color.FromArgb(181,128,255))) g.FillPath(paint,bar);
            using(var pen = new Pen(Color.FromArgb(168,187,220),size <= 24 ? 12 : 9)) {
                pen.StartCap=LineCap.Round; pen.EndCap=LineCap.Round;
                g.DrawLine(pen,128,182,128,203); g.DrawLine(pen,96,206,160,206);
            }
        }
        Bitmap result = new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(Graphics g = Graphics.FromImage(result)) {
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.CompositingQuality=CompositingQuality.HighQuality;
            g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            g.DrawImage(large,0,0,size,size);
        }
        large.Dispose(); return result;
    }
    public static void Save(string folder) {
        int[] sizes={16,20,24,32,40,48,64,128,256};
        byte[][] frames=new byte[sizes.Length][];
        for(int i=0;i<sizes.Length;i++) using(Bitmap bmp=Render(sizes[i])) using(MemoryStream ms=new MemoryStream()) {
            bmp.Save(ms,ImageFormat.Png); frames[i]=ms.ToArray();
        }
        using(BinaryWriter w=new BinaryWriter(File.Create(Path.Combine(folder,"TaskbarController.ico")))) {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) {
                w.Write((byte)(sizes[i]==256?0:sizes[i])); w.Write((byte)(sizes[i]==256?0:sizes[i]));
                w.Write((byte)0); w.Write((byte)0); w.Write((ushort)1); w.Write((ushort)32);
                w.Write(frames[i].Length); w.Write(offset); offset+=frames[i].Length;
            }
            foreach(byte[] frame in frames) w.Write(frame);
        }
    }
}
'@
[TaskbarArtwork]::Save($PSScriptRoot)
