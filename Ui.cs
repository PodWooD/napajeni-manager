using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NapajeniManager
{
    /// <summary>Barevna paleta aplikace.</summary>
    public static class Barvy
    {
        public static readonly Color Pozadi     = Color.FromArgb(24, 25, 28);
        public static readonly Color Panel      = Color.FromArgb(32, 34, 38);
        public static readonly Color Karta      = Color.FromArgb(38, 40, 45);
        public static readonly Color KartaHover = Color.FromArgb(45, 47, 53);
        public static readonly Color Okraj      = Color.FromArgb(52, 55, 62);
        public static readonly Color Text       = Color.FromArgb(235, 236, 240);
        public static readonly Color TextSlaby  = Color.FromArgb(140, 145, 155);
        public static readonly Color Akcent     = Color.FromArgb(255, 149, 61);
        public static readonly Color AkcentTmavy= Color.FromArgb(196, 110, 40);
        public static readonly Color Zelena     = Color.FromArgb(56, 176, 106);
        public static readonly Color ZelenaHover= Color.FromArgb(66, 196, 122);
        public static readonly Color Cervena    = Color.FromArgb(214, 82, 82);
    }

    public static class Pisma
    {
        public static readonly Font Nadpis  = new Font("Segoe UI Semibold", 15F);
        public static readonly Font Sekce   = new Font("Segoe UI Semibold", 10.5F);
        public static readonly Font Bezny   = new Font("Segoe UI", 9.75F);
        public static readonly Font Maly    = new Font("Segoe UI", 8.75F);
        public static readonly Font Velky   = new Font("Segoe UI Semibold", 20F);
        public static readonly Font Mono    = NejlepsiMono(9F);

        /// <summary>Prvni dostupne neproporcionalni pismo. Bez teto kontroly by
        /// Windows na cizim stroji podstrcily Microsoft Sans Serif a tabulka
        /// mereni by se rozsypala.</summary>
        static Font NejlepsiMono(float velikost)
        {
            foreach (string jmeno in new[] { "Cascadia Mono", "Consolas", "Courier New" })
            {
                var f = new Font(jmeno, velikost);
                if (string.Equals(f.Name, jmeno, StringComparison.OrdinalIgnoreCase)) return f;
                f.Dispose();
            }
            return new Font(FontFamily.GenericMonospace, velikost);
        }
    }

    public static class Kresleni
    {
        public static GraphicsPath Zaobleny(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Vypln(Graphics g, Rectangle r, int radius, Color barva)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Zaobleny(r, radius))
            using (var b = new SolidBrush(barva)) g.FillPath(b, p);
        }

        public static void Obrys(Graphics g, Rectangle r, int radius, Color barva)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rr = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (var p = Zaobleny(rr, radius))
            using (var pen = new Pen(barva)) g.DrawPath(pen, p);
        }
    }

    /// <summary>Karta - panel se zaoblenymi rohy a volitelnym nadpisem.</summary>
    public class Karta : Panel
    {
        public string Nadpis = "";

        public Karta()
        {
            DoubleBuffered = true;
            BackColor = Barvy.Panel;
            ForeColor = Barvy.Text;
        }

        /// <summary>Prvky vlozene do karty dedi barvu panelu, ne karty - bez tohoto
        /// se kolem kazdeho popisku kresli svetlejsi obdelnik.</summary>
        protected override void OnControlAdded(ControlEventArgs e)
        {
            if (e.Control is Label || e.Control is Prepinac || e.Control is Cislovac || e.Control is VyberDnu)
                e.Control.BackColor = Barvy.Karta;
            base.OnControlAdded(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Barvy.Panel);
            var r = new Rectangle(0, 0, Width, Height);
            Kresleni.Vypln(g, r, 8, Barvy.Karta);
            Kresleni.Obrys(g, r, 8, Barvy.Okraj);
            if (!string.IsNullOrEmpty(Nadpis))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawString(Nadpis, Pisma.Sekce, new SolidBrush(Barvy.Text), 16, 12);
                using (var pen = new Pen(Barvy.Okraj))
                    g.DrawLine(pen, 16, 38, Width - 16, 38);
            }
            base.OnPaint(e);
        }
    }

    /// <summary>Moderni prepinac misto CheckBoxu.</summary>
    public class Prepinac : Control
    {
        bool zapnuto = false;
        bool hover = false;
        public event EventHandler ZmenaStavu;

        public bool Zapnuto
        {
            get { return zapnuto; }
            set { if (zapnuto != value) { zapnuto = value; Invalidate(); if (ZmenaStavu != null) ZmenaStavu(this, EventArgs.Empty); } }
        }

        public Prepinac()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(300, 26);
            Font = Pisma.Bezny;
            ForeColor = Barvy.Text;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Zapnuto = !Zapnuto; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int h = 20, w = 40;
            int y = (Height - h) / 2;
            var drazka = new Rectangle(0, y, w, h);
            Color barvaDrazky = zapnuto ? Barvy.Akcent : Color.FromArgb(70, 73, 80);
            if (hover) barvaDrazky = ControlPaint.Light(barvaDrazky, 0.12f);
            Kresleni.Vypln(g, drazka, h / 2, barvaDrazky);

            int prumer = h - 6;
            int px = zapnuto ? w - prumer - 3 : 3;
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, px, y + 3, prumer, prumer);

            var rt = new Rectangle(w + 12, 0, Width - w - 12, Height);
            TextRenderer.DrawText(g, Text, Font, rt, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Posuvnik s vlastnim vykreslenim.</summary>
    public class Posuvnik : Control
    {
        int hodnota = 50, min = 5, max = 100;
        bool tahne = false;
        public event EventHandler ZmenaHodnoty;

        public int Hodnota
        {
            get { return hodnota; }
            set
            {
                int v = Math.Max(min, Math.Min(max, value));
                if (hodnota != v) { hodnota = v; Invalidate(); if (ZmenaHodnoty != null) ZmenaHodnoty(this, EventArgs.Empty); }
            }
        }
        public int Minimum { get { return min; } set { min = value; Invalidate(); } }
        public int Maximum { get { return max; } set { max = value; Invalidate(); } }

        public Posuvnik()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 32;
            Cursor = Cursors.Hand;
        }

        int PozZHodnoty() { return (int)((hodnota - min) / (float)(max - min) * (Width - 18)) + 9; }
        void HodnotaZPozice(int x)
        {
            float f = (x - 9) / (float)(Width - 18);
            Hodnota = (int)Math.Round(min + f * (max - min));
        }

        protected override void OnMouseDown(MouseEventArgs e) { tahne = true; HodnotaZPozice(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (tahne) HodnotaZPozice(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { tahne = false; base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int y = Height / 2;
            using (var pen = new Pen(Color.FromArgb(62, 65, 72), 5)) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; g.DrawLine(pen, 9, y, Width - 9, y); }
            int px = PozZHodnoty();
            using (var pen = new Pen(Barvy.Akcent, 5)) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; if (px > 10) g.DrawLine(pen, 9, y, px, y); }

            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, px - 8, y - 8, 16, 16);
            using (var b = new SolidBrush(Barvy.Akcent)) g.FillEllipse(b, px - 4, y - 4, 8, 8);
        }
    }

    /// <summary>Ploche tlacitko.</summary>
    public class Tlacitko : Control
    {
        bool hover = false, stisk = false;
        public Color BarvaPozadi = Color.FromArgb(52, 55, 62);
        public Color BarvaHover  = Color.FromArgb(66, 70, 78);
        public bool Hlavni = false;

        public Tlacitko()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(160, 38);
            Font = Pisma.Bezny;
            ForeColor = Barvy.Text;
            Cursor = Cursors.Hand;
        }

        public void JakoHlavni()
        {
            Hlavni = true;
            BarvaPozadi = Barvy.Zelena;
            BarvaHover = Barvy.ZelenaHover;
            Font = new Font("Segoe UI Semibold", 9.75F);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; stisk = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { stisk = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { stisk = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Color c = Enabled ? (stisk ? ControlPaint.Dark(BarvaPozadi, 0.05f) : (hover ? BarvaHover : BarvaPozadi))
                              : Color.FromArgb(44, 46, 52);
            Kresleni.Vypln(g, new Rectangle(0, 0, Width, Height), 6, c);
            Color ct = Enabled ? ForeColor : Barvy.TextSlaby;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), ct,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Polozka bocni navigace.</summary>
    public class NavPolozka : Control
    {
        bool hover = false;
        bool aktivni = false;
        public bool Aktivni { get { return aktivni; } set { aktivni = value; Invalidate(); } }

        public NavPolozka()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(196, 42);
            Font = Pisma.Bezny;
            ForeColor = Barvy.TextSlaby;
            Cursor = Cursors.Hand;
            BackColor = Barvy.Pozadi;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Barvy.Pozadi);

            var r = new Rectangle(8, 3, Width - 16, Height - 6);
            if (aktivni) Kresleni.Vypln(g, r, 6, Color.FromArgb(44, 47, 54));
            else if (hover) Kresleni.Vypln(g, r, 6, Color.FromArgb(34, 36, 41));

            if (aktivni)
                using (var b = new SolidBrush(Barvy.Akcent))
                using (var p = Kresleni.Zaobleny(new Rectangle(8, 11, 3, Height - 22), 2))
                    g.FillPath(b, p);

            Color ct = aktivni ? Barvy.Text : (hover ? Barvy.Text : Barvy.TextSlaby);
            var rt = new Rectangle(24, 0, Width - 32, Height);
            TextRenderer.DrawText(g, Text, aktivni ? new Font("Segoe UI Semibold", 9.75F) : Font, rt, ct,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Textove pole s plochym vzhledem.</summary>
    public class Pole : Panel
    {
        public TextBox Vnitrni = new TextBox();

        public Pole()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(28, 30, 34);
            Padding = new Padding(9, 6, 9, 6);
            Size = new Size(180, 32);
            Vnitrni.BorderStyle = BorderStyle.None;
            Vnitrni.BackColor = Color.FromArgb(28, 30, 34);
            Vnitrni.ForeColor = Barvy.Text;
            Vnitrni.Font = Pisma.Bezny;
            Vnitrni.Dock = DockStyle.Fill;
            Controls.Add(Vnitrni);
        }

        public override string Text { get { return Vnitrni.Text; } set { Vnitrni.Text = value; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Kresleni.Vypln(e.Graphics, new Rectangle(0, 0, Width, Height), 6, Color.FromArgb(28, 30, 34));
            Kresleni.Obrys(e.Graphics, new Rectangle(0, 0, Width, Height), 6, Barvy.Okraj);
            base.OnPaint(e);
        }
    }

    /// <summary>Ciselne pole s tlacitky + a -.</summary>
    public class Cislovac : Control
    {
        int hodnota = 0, min = 0, max = 100;
        Rectangle rMinus, rPlus;
        int hoverTl = 0;

        public event EventHandler ZmenaHodnoty;

        public int Hodnota
        {
            get { return hodnota; }
            set
            {
                int v = Dokola ? Pretoc(value) : Math.Max(min, Math.Min(max, value));
                if (v == hodnota) return;
                hodnota = v;
                Invalidate();
                if (ZmenaHodnoty != null) ZmenaHodnoty(this, EventArgs.Empty);
            }
        }
        public int Minimum { get { return min; } set { min = value; } }
        public int Maximum { get { return max; } set { max = value; } }
        public int Krok = 1;
        /// <summary>Za maximem pokracuje od minima - pro hodiny a minuty.</summary>
        public bool Dokola = false;
        /// <summary>Doplni zleva nuly, aby cas vypadal jako 07 a ne 7.</summary>
        public int Cislic = 1;

        int Pretoc(int v)
        {
            int rozsah = max - min + 1;
            if (rozsah <= 0) return min;
            return min + ((v - min) % rozsah + rozsah) % rozsah;
        }

        public Cislovac()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(110, 32);
            Font = Pisma.Bezny;
            ForeColor = Barvy.Text;
        }

        protected override void OnResize(EventArgs e)
        {
            rMinus = new Rectangle(1, 1, 28, Height - 2);
            rPlus = new Rectangle(Width - 29, 1, 28, Height - 2);
            base.OnResize(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = rMinus.Contains(e.Location) ? 1 : (rPlus.Contains(e.Location) ? 2 : 0);
            if (h != hoverTl) { hoverTl = h; Cursor = h == 0 ? Cursors.Default : Cursors.Hand; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hoverTl = 0; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (rMinus.Contains(e.Location)) Hodnota = hodnota - Krok;
            else if (rPlus.Contains(e.Location)) Hodnota = hodnota + Krok;
            base.OnMouseDown(e);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Hodnota = hodnota + (e.Delta > 0 ? Krok : -Krok);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            var r = new Rectangle(0, 0, Width, Height);
            Kresleni.Vypln(g, r, 6, Color.FromArgb(28, 30, 34));
            Kresleni.Obrys(g, r, 6, Barvy.Okraj);

            if (hoverTl == 1) Kresleni.Vypln(g, rMinus, 5, Color.FromArgb(44, 47, 54));
            if (hoverTl == 2) Kresleni.Vypln(g, rPlus, 5, Color.FromArgb(44, 47, 54));

            TextRenderer.DrawText(g, "−", Pisma.Bezny, rMinus, Barvy.TextSlaby, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "+", Pisma.Bezny, rPlus, Barvy.TextSlaby, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, hodnota.ToString().PadLeft(Cislic, '0'), Font, r, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>Rozbalovaci seznam s vlastnim vzhledem.</summary>
    public class Vyber : ComboBox
    {
        public Vyber()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            BackColor = Color.FromArgb(28, 30, 34);
            ForeColor = Barvy.Text;
            Font = Pisma.Bezny;
            ItemHeight = 24;
        }

        /// <summary>ComboBox si i s FlatStyle.Flat kresli svetly systemovy ramecek
        /// a tlacitko sipky. Prekreslime je az po tom, co se vykresli sam.</summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            const int WM_PAINT = 0x000F;
            if (m.Msg != WM_PAINT || IsDisposed || !IsHandleCreated) return;

            using (var g = Graphics.FromHwnd(Handle))
            {
                var sipkaPole = new Rectangle(Width - 20, 1, 19, Height - 2);
                using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, sipkaPole);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                int sx = Width - 12, sy = Height / 2 - 1;
                using (var b = new SolidBrush(Barvy.TextSlaby))
                    g.FillPolygon(b, new[] {
                        new Point(sx - 4, sy - 1), new Point(sx + 4, sy - 1), new Point(sx, sy + 4) });

                g.SmoothingMode = SmoothingMode.None;
                using (var pen = new Pen(Barvy.Okraj))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool vybrano = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var b = new SolidBrush(vybrano ? Color.FromArgb(52, 56, 64) : Color.FromArgb(28, 30, 34)))
                e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font,
                new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
                Barvy.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>Vyber dnu v tydnu - rada prepinatelnych tlacitek misto
    /// systemoveho CheckedListBoxu, ktery se na tmavem pozadi kresli bile.</summary>
    public class VyberDnu : Control
    {
        static readonly string[] Zkratky  = { "Po", "Út", "St", "Čt", "Pá", "So", "Ne" };
        static readonly string[] Anglicky = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        readonly bool[] vybrano = new bool[7];
        int hover = -1;

        public event EventHandler ZmenaHodnoty;

        public VyberDnu()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(280, 34);
            Font = Pisma.Bezny;
            ForeColor = Barvy.Text;
            Cursor = Cursors.Hand;
        }

        /// <summary>Dny anglicky, oddelene carkou - tak, jak je bere Planovac uloh.</summary>
        public string Dny
        {
            get
            {
                var l = new System.Collections.Generic.List<string>();
                for (int i = 0; i < 7; i++) if (vybrano[i]) l.Add(Anglicky[i]);
                return string.Join(",", l.ToArray());
            }
            set
            {
                for (int i = 0; i < 7; i++) vybrano[i] = false;
                if (!string.IsNullOrEmpty(value))
                    foreach (string d in value.Split(','))
                    {
                        string t = d.Trim();
                        for (int i = 0; i < 7; i++)
                            if (string.Equals(t, Anglicky[i], StringComparison.OrdinalIgnoreCase)) vybrano[i] = true;
                    }
                Invalidate();
            }
        }

        int PodMysi(Point p)
        {
            int w = Width / 7;
            if (w <= 0 || p.X < 0 || p.X >= w * 7) return -1;
            return p.X / w;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = PodMysi(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int i = PodMysi(e.Location);
            if (i >= 0)
            {
                vybrano[i] = !vybrano[i];
                Invalidate();
                if (ZmenaHodnoty != null) ZmenaHodnoty(this, EventArgs.Empty);
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            int w = Width / 7;
            for (int i = 0; i < 7; i++)
            {
                var r = new Rectangle(i * w + 2, 0, w - 4, Height);
                Color pozadi, text;
                if (vybrano[i]) { pozadi = hover == i ? Barvy.Akcent : Barvy.AkcentTmavy; text = Color.White; }
                else { pozadi = hover == i ? Color.FromArgb(44, 47, 54) : Color.FromArgb(28, 30, 34); text = Barvy.TextSlaby; }
                Kresleni.Vypln(g, r, 6, pozadi);
                if (!vybrano[i]) Kresleni.Obrys(g, r, 6, Barvy.Okraj);
                TextRenderer.DrawText(g, Zkratky[i], Font, r, text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>Pruh se zivym stavem nad obsahem okna.
    /// Svisle rozvrzeni se pocita z vysky pouzitych pisem, ne z pevnych cisel -
    /// diky tomu drzi i pri jinem meritku zobrazeni.</summary>
    public class StavovyPruh : Control
    {
        public string Rezim = "—";
        public string Detail = "";
        public string Dalsi = "";
        public string Televize = "";
        public bool Uspora = false;
        public bool Neulozeno = false;

        static readonly Font PismoRezim = new Font("Segoe UI Semibold", 14F);

        readonly int yRezim, yDetail, yDalsi, hRezim, hDetail, hDalsi, okraj;

        public StavovyPruh()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            hRezim  = PismoRezim.Height;
            hDetail = Pisma.Maly.Height;
            hDalsi  = Pisma.Bezny.Height;
            okraj   = hDetail;
            int mezera = Math.Max(3, hDetail / 4);

            yRezim  = okraj;
            yDetail = yRezim + hRezim + mezera;
            yDalsi  = yDetail + hDetail + mezera;

            Height = yDalsi + hDalsi + okraj;
            BackColor = Barvy.Pozadi;
            ForeColor = Barvy.Text;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width, Height);
            Kresleni.Vypln(g, r, 8, Barvy.Karta);
            Kresleni.Obrys(g, r, 8, Barvy.Okraj);

            int velikostTecky = Math.Max(8, hDetail - 3);
            int levy = okraj + velikostTecky + okraj;
            using (var b = new SolidBrush(Uspora ? Barvy.Akcent : Barvy.Zelena))
                g.FillEllipse(b, okraj + 4, yRezim + (hRezim - velikostTecky) / 2, velikostTecky, velikostTecky);

            int sirkaZnacky = TextRenderer.MeasureText("● Neuložené změny", Pisma.Maly).Width + 24;
            int sirkaTV = string.IsNullOrEmpty(Televize) ? 0 : TextRenderer.MeasureText(Televize, Pisma.Maly).Width + 16;

            TextRenderer.DrawText(g, Rezim, PismoRezim,
                new Rectangle(levy, yRezim, Math.Max(80, Width - levy - okraj - (Neulozeno ? sirkaZnacky + okraj : 0)), hRezim),
                Barvy.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, Detail, Pisma.Maly,
                new Rectangle(levy, yDetail, Math.Max(80, Width - levy - okraj - sirkaTV), hDetail),
                Barvy.TextSlaby, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, Dalsi, Pisma.Bezny,
                new Rectangle(levy, yDalsi, Math.Max(80, Width - levy - okraj), hDalsi),
                Barvy.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (sirkaTV > 0)
                TextRenderer.DrawText(g, Televize, Pisma.Maly,
                    new Rectangle(Width - okraj - sirkaTV, yDetail, sirkaTV, hDetail),
                    Barvy.TextSlaby, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            if (Neulozeno)
            {
                var znacka = new Rectangle(Width - okraj - sirkaZnacky, yRezim, sirkaZnacky, hRezim);
                Kresleni.Vypln(g, znacka, znacka.Height / 2, Color.FromArgb(70, 52, 30));
                Kresleni.Obrys(g, znacka, znacka.Height / 2, Barvy.Akcent);
                TextRenderer.DrawText(g, "● Neuložené změny", Pisma.Maly, znacka, Barvy.Akcent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
