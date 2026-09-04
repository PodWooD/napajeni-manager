using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NapajeniManager
{
    public class Plan
    {
        public string Guid;
        public string Nazev;
        public override string ToString() { return Nazev; }
    }

    public class Nastaveni
    {
        Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string Cesta;

        public Nastaveni(string cesta)
        {
            Cesta = cesta;
            Vychozi();
            if (File.Exists(cesta))
                foreach (string radek in File.ReadAllLines(cesta, Encoding.UTF8))
                {
                    string r = radek.Trim();
                    if (r.Length == 0 || r.StartsWith("#")) continue;
                    int i = r.IndexOf('=');
                    if (i > 0) d[r.Substring(0, i).Trim()] = r.Substring(i + 1).Trim();
                }
        }

        void Vychozi()
        {
            d["PlanBezny"] = "381b4222-f694-41f0-9685-ff5bb260df2e";
            d["PlanUspora"] = "a1841308-3541-4fab-bc81-f71556f20b4a";
            d["MaxStavProcesoru"] = "50";
            d["Odpocet"] = "60";
            d["PrahCPU"] = "20";
            d["PrahGPU"] = "20";
            d["ZakazatSpanek"] = "1";
            d["Tyden_Povoleno"] = "0"; d["Tyden_Cas"] = "00:00";
            d["Tyden_Dny"] = "Monday,Tuesday,Wednesday,Thursday,Friday";
            d["Tyden_KolKlidu"] = "3"; d["Tyden_IntervalS"] = "300";
            d["Tyden_VypnoutTV"] = "0"; d["Tyden_Zamknout"] = "0";
            d["Vikend_Povoleno"] = "0"; d["Vikend_Cas"] = "02:00";
            d["Vikend_Dny"] = "Saturday,Sunday";
            d["Vikend_KolKlidu"] = "3"; d["Vikend_IntervalS"] = "300";
            d["Vikend_VypnoutTV"] = "0"; d["Vikend_Zamknout"] = "0";
            d["Zamknuti_Povoleno"] = "0"; d["Zamknuti_KolKlidu"] = "2";
            d["Zamknuti_IntervalS"] = "180"; d["Zamknuti_VypnoutTV"] = "0";
            d["Odemknuti_Povoleno"] = "1";
            d["TV_Povoleno"] = "0"; d["TV_IP"] = ""; d["TV_Port"] = "3001";
        }

        public string S(string k) { return d.ContainsKey(k) ? d[k] : ""; }
        public int I(string k) { int v; return int.TryParse(S(k), out v) ? v : 0; }
        public bool B(string k) { return S(k) == "1"; }
        public void Set(string k, string v) { d[k] = v; }
        public void Set(string k, int v) { d[k] = v.ToString(); }
        public void Set(string k, bool v) { d[k] = v ? "1" : "0"; }

        public void Uloz()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Nastaveni Napajeni Manageru");
            sb.AppendLine("# Rucni zmeny se projevi az po stisku Ulozit v aplikaci.");
            sb.AppendLine();
            foreach (var kv in d.OrderBy(x => x.Key)) sb.AppendLine(kv.Key + "=" + kv.Value);
            File.WriteAllText(Cesta, sb.ToString(), new UTF8Encoding(true));
        }
    }

    public class HlavniOkno : Form
    {
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int m, int w, int l);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();

        Nastaveni n;
        string slozka;
        List<Plan> plany = new List<Plan>();

        Panel obsah;
        List<NavPolozka> nav = new List<NavPolozka>();
        List<Panel> stranky = new List<Panel>();

        Vyber cmbBezny, cmbUspora;
        int hodnotaMax = 50;
        Label lblShrnuti;
        Cislovac cOdpocet, cPrahCPU, cPrahGPU;
        Prepinac pSpanek;
        TextBox txtMereni;
        Tlacitko btnMer;

        Prepinac pTyden, pTydenTV, pTydenZamk, pVikend, pVikendTV, pVikendZamk;
        Cislovac cTydenHod, cTydenMin, cVikendHod, cVikendMin;
        Cislovac cTydenKlid, cVikendKlid;
        VyberDnu dnyTyden, dnyVikend;

        Prepinac pZamknuti, pZamknutiTV, pOdemknuti;
        Cislovac cZamkKlid;

        Prepinac pTV;
        Pole poleIP, polePort;

        TextBox txtStav;

        Panel panLista, panLevy, panSpodni, panVrch;
        StavovyPruh pruh;
        bool nacitam = false;      // potlaci hlaseni zmen behem plneni formulare
        bool zmeneno = false;      // uzivatel neco zmenil a jeste neulozil
        Timer tikot;

        static readonly string[] DnyEn = { "Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday" };
        static readonly string[] DnyCz = { "Pondělí","Úterý","Středa","Čtvrtek","Pátek","Sobota","Neděle" };

        public HlavniOkno()
        {
            slozka = Path.GetDirectoryName(Application.ExecutablePath);
            n = new Nastaveni(Path.Combine(slozka, "config.ini"));

            Text = "Napájení Manager";
            ClientSize = new Size(960, 910);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Barvy.Pozadi;
            ForeColor = Barvy.Text;
            Font = Pisma.Bezny;
            DoubleBuffered = true;

            NactiPlany();
            Lista();
            BocniNav();
            ObsahovaCast();
            SpodniLista();

            NacistDoFormulare();
            SledujZmeny();
            SrovnejUkotveni();
            Prepni(0);
            ObnovStav();
            ObnovPruh();

            // Aktivni schema muze zmenit i naplanovana uloha, kdyz je okno otevrene.
            tikot = new Timer();
            tikot.Interval = 3000;
            tikot.Tick += delegate { ObnovPruh(); };
            tikot.Start();

            FormClosing += HlidejZavreni;
        }

        /// <summary>WinForms ukotvuje panely v obracenem poradi z-osy. Aby Fill
        /// zabral az to, co zbyde, musi byt uplne vpredu - jinak se prekresli pres nej.</summary>
        void SrovnejUkotveni()
        {
            panLista.BringToFront();
            panLevy.BringToFront();
            panSpodni.BringToFront();
            panVrch.BringToFront();
            obsah.BringToFront();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Rozbalovaci seznamy hlasi zmenu vyberu jeste jednou pri vytvoreni
            // okenniho handlu. To neni zmena od uzivatele.
            zmeneno = false;
            ObnovPruh();
        }

        // ---------------- neulozene zmeny ----------------

        /// <summary>Prihlasi se ke zmenam vsech ovladacich prvku. Bez toho
        /// uzivatel zavre okno v presvedceni, ze si neco nastavil.</summary>
        void SledujZmeny()
        {
            foreach (var c in VsechnyPrvky(this))
            {
                var pr = c as Prepinac;      if (pr != null) { pr.ZmenaStavu += Zmena; continue; }
                var ci = c as Cislovac;      if (ci != null) { ci.ZmenaHodnoty += Zmena; continue; }
                var vd = c as VyberDnu;      if (vd != null) { vd.ZmenaHodnoty += Zmena; continue; }
                var vy = c as Vyber;         if (vy != null) { vy.SelectedIndexChanged += Zmena; continue; }
                var tb = c as TextBox;       if (tb != null && !tb.ReadOnly) { tb.TextChanged += Zmena; continue; }
            }
        }

        IEnumerable<Control> VsechnyPrvky(Control koren)
        {
            foreach (Control c in koren.Controls)
            {
                yield return c;
                foreach (var v in VsechnyPrvky(c)) yield return v;
            }
        }

        void Zmena(object odesilatel, EventArgs e)
        {
            if (nacitam) return;
            if (!zmeneno) { zmeneno = true; ObnovPruh(); }
        }

        void HlidejZavreni(object odesilatel, FormClosingEventArgs e)
        {
            if (!zmeneno) return;
            var odpoved = MessageBox.Show(
                "Máte neuložené změny. Uložit je a nastavit úlohy?",
                "Neuložené změny", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (odpoved == DialogResult.Cancel) { e.Cancel = true; return; }
            if (odpoved == DialogResult.Yes)
            {
                if (!Uloz()) e.Cancel = true;   // ulozeni neproslo, okno necháme otevrene
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Kresleni.Obrys(e.Graphics, new Rectangle(0, 0, Width, Height), 0, Barvy.Okraj);
        }

        // ---------------- horni lista ----------------
        void Lista()
        {
            var lista = new Panel();
            lista.Dock = DockStyle.Top; lista.Height = 48; lista.BackColor = Barvy.Pozadi;
            lista.Paint += delegate (object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Barvy.Akcent)) g.FillEllipse(b, 20, 19, 10, 10);
                TextRenderer.DrawText(g, "Napájení Manager", new Font("Segoe UI Semibold", 11F),
                    new Rectangle(40, 0, 400, 48), Barvy.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                using (var p = new Pen(Barvy.Okraj)) g.DrawLine(p, 0, 47, lista.Width, 47);
            };
            lista.MouseDown += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); }
            };
            panLista = lista;
            Controls.Add(lista);

            var zavri = new Tlacitko();
            zavri.Text = "✕"; zavri.Size = new Size(44, 32); zavri.Location = new Point(ClientSize.Width - 56, 8);
            zavri.BarvaPozadi = Barvy.Pozadi; zavri.BarvaHover = Barvy.Cervena;
            zavri.Click += delegate { Close(); };
            lista.Controls.Add(zavri);

            var min = new Tlacitko();
            min.Text = "―"; min.Size = new Size(44, 32); min.Location = new Point(ClientSize.Width - 104, 8);
            min.BarvaPozadi = Barvy.Pozadi; min.BarvaHover = Color.FromArgb(52, 55, 62);
            min.Click += delegate { WindowState = FormWindowState.Minimized; };
            lista.Controls.Add(min);
        }

        // ---------------- bocni navigace ----------------
        void BocniNav()
        {
            var levy = new Panel();
            levy.Dock = DockStyle.Left; levy.Width = 212; levy.BackColor = Barvy.Pozadi;
            levy.Padding = new Padding(0, 12, 0, 0);
            levy.Paint += delegate (object s, PaintEventArgs e)
            {
                using (var p = new Pen(Barvy.Okraj)) e.Graphics.DrawLine(p, levy.Width - 1, 0, levy.Width - 1, levy.Height);
            };
            panLevy = levy;
            Controls.Add(levy);

            string[] popisky = { "Režimy napájení", "Noční přepnutí", "Zamknutí počítače", "Televize", "Stav a záznam" };
            int y = 16;
            for (int i = 0; i < popisky.Length; i++)
            {
                var np = new NavPolozka();
                np.Text = popisky[i];
                np.Location = new Point(6, y);
                np.Width = 200;
                int idx = i;
                np.Click += delegate { Prepni(idx); };
                levy.Controls.Add(np);
                nav.Add(np);
                y += 46;
            }
        }

        void Prepni(int index)
        {
            for (int i = 0; i < nav.Count; i++) nav[i].Aktivni = (i == index);
            for (int i = 0; i < stranky.Count; i++) stranky[i].Visible = (i == index);
            if (index == 4) ObnovStav();
        }

        // ---------------- obsah ----------------
        void ObsahovaCast()
        {
            var vrch = new Panel();
            vrch.Dock = DockStyle.Top; vrch.Height = 96; vrch.BackColor = Barvy.Pozadi;
            vrch.Padding = new Padding(24, 8, 24, 8);
            pruh = new StavovyPruh(); pruh.Dock = DockStyle.Fill;
            vrch.Controls.Add(pruh);

            obsah = new Panel();
            obsah.Dock = DockStyle.Fill;
            obsah.BackColor = Barvy.Pozadi;
            obsah.Padding = new Padding(24, 8, 24, 16);
            Controls.Add(obsah);
            panVrch = vrch;
            Controls.Add(vrch);

            stranky.Add(StrRezimy());
            stranky.Add(StrNoc());
            stranky.Add(StrZamek());
            stranky.Add(StrTV());
            stranky.Add(StrStav());
            foreach (var s in stranky) { obsah.Controls.Add(s); s.Visible = false; }
        }

        Panel NovaStranka()
        {
            var p = new Panel();
            p.Dock = DockStyle.Fill; p.BackColor = Barvy.Pozadi; p.AutoScroll = true;
            return p;
        }

        Label Nadpis(string t, int x, int y)
        {
            var l = new Label();
            l.Text = t; l.Font = Pisma.Nadpis; l.ForeColor = Barvy.Text;
            l.Location = new Point(x, y); l.AutoSize = true;
            return l;
        }

        Label Popisek(string t, int x, int y, int w)
        {
            var l = new Label();
            l.Text = t; l.Font = Pisma.Bezny; l.ForeColor = Barvy.Text;
            l.Location = new Point(x, y); l.Size = new Size(w, 20);
            return l;
        }

        Label Slaby(string t, int x, int y, int w, int h)
        {
            var l = new Label();
            l.Text = t; l.Font = Pisma.Maly; l.ForeColor = Barvy.TextSlaby;
            l.Location = new Point(x, y); l.Size = new Size(w, h);
            return l;
        }

        Karta NovaKarta(string nadpis, int x, int y, int w, int h)
        {
            var k = new Karta();
            k.Nadpis = nadpis; k.Location = new Point(x, y); k.Size = new Size(w, h);
            return k;
        }

        /// <summary>Dva cislovace a dvojtecka - vypada stejne jako zbytek okna,
        /// na rozdil od systemoveho DateTimePickeru, ktery se kresli bile.</summary>
        void VlozCas(Control rodic, int x, int y, out Cislovac hod, out Cislovac min)
        {
            hod = new Cislovac();
            hod.Location = new Point(x, y); hod.Size = new Size(96, 32);
            hod.Minimum = 0; hod.Maximum = 23; hod.Dokola = true; hod.Cislic = 2;
            rodic.Controls.Add(hod);

            var dvojtecka = new Label();
            dvojtecka.Text = ":"; dvojtecka.Font = Pisma.Sekce; dvojtecka.ForeColor = Barvy.TextSlaby;
            dvojtecka.Location = new Point(x + 98, y + 5); dvojtecka.Size = new Size(10, 22);
            dvojtecka.TextAlign = ContentAlignment.MiddleCenter;
            rodic.Controls.Add(dvojtecka);

            min = new Cislovac();
            min.Location = new Point(x + 110, y); min.Size = new Size(96, 32);
            min.Minimum = 0; min.Maximum = 59; min.Krok = 5; min.Dokola = true; min.Cislic = 2;
            rodic.Controls.Add(min);
        }

        /// <summary>Cislovac ukazujici primo minuty klidu misto poctu vzorku
        /// a intervalu. Uvnitr se to porad pocita po trech minutach.</summary>
        Cislovac Klid(Control rodic, int x, int y)
        {
            var c = new Cislovac();
            c.Location = new Point(x, y); c.Size = new Size(110, 32);
            c.Minimum = 3; c.Maximum = 120; c.Krok = 3;
            rodic.Controls.Add(c);
            return c;
        }

        // ---------------- stranka: rezimy ----------------
        Panel StrRezimy()
        {
            var s = NovaStranka();
            s.Controls.Add(Nadpis("Režimy napájení", 4, 4));

            var k1 = NovaKarta("Schémata", 4, 46, 676, 132);
            k1.Controls.Add(Popisek("Běžný režim (plný výkon)", 16, 54, 200));
            cmbBezny = new Vyber(); cmbBezny.Location = new Point(230, 51); cmbBezny.Size = new Size(250, 26);
            k1.Controls.Add(cmbBezny);
            k1.Controls.Add(Popisek("Úsporný režim", 16, 90, 200));
            cmbUspora = new Vyber(); cmbUspora.Location = new Point(230, 87); cmbUspora.Size = new Size(250, 26);
            k1.Controls.Add(cmbUspora);
            var bObnov = new Tlacitko(); bObnov.Text = "Načíst znovu"; bObnov.Size = new Size(120, 30); bObnov.Location = new Point(498, 51);
            bObnov.Click += delegate { NactiPlany(); NaplnVyber(cmbBezny, GuidZVyberu(cmbBezny)); NaplnVyber(cmbUspora, GuidZVyberu(cmbUspora)); };
            k1.Controls.Add(bObnov);
            k1.Controls.Add(Slaby("Nabídka odpovídá schématům na tomto počítači, včetně vlastních.", 498, 90, 128, 34));
            s.Controls.Add(k1);

            var k2 = NovaKarta("Úsporný režim", 4, 190, 676, 286);
            k2.Controls.Add(Popisek("Jak moc počítač zpomalit, když ho zrovna nepoužíváte?", 16, 52, 500));

            btnMer = new Tlacitko(); btnMer.Text = "Změřit a nastavit automaticky";
            btnMer.JakoHlavni();
            btnMer.Size = new Size(240, 38); btnMer.Location = new Point(16, 80);
            btnMer.Click += delegate { Zmer(); };
            k2.Controls.Add(btnMer);
            k2.Controls.Add(Slaby("Program vyzkouší několik úrovní a vybere tu nejúspornější,\nu které počítač ještě zůstane svižný. Trvá asi dvě minuty.", 272, 82, 390, 40));

            lblShrnuti = new Label();
            lblShrnuti.Font = new Font("Segoe UI Semibold", 10.5F);
            lblShrnuti.ForeColor = Barvy.Akcent;
            lblShrnuti.Location = new Point(16, 130); lblShrnuti.Size = new Size(644, 22);
            k2.Controls.Add(lblShrnuti);

            txtMereni = new TextBox();
            txtMereni.Multiline = true; txtMereni.ReadOnly = true; txtMereni.ScrollBars = ScrollBars.Vertical;
            txtMereni.BorderStyle = BorderStyle.None;
            txtMereni.Location = new Point(16, 158); txtMereni.Size = new Size(644, 112);
            txtMereni.BackColor = Color.FromArgb(28, 30, 34); txtMereni.ForeColor = Barvy.TextSlaby;
            txtMereni.Font = Pisma.Mono;
            txtMereni.Text = "Zatím nezměřeno.\r\n\r\nStiskněte tlačítko výše — program si sám zjistí, jak moc se dá\r\ntento počítač zpomalit, aniž by přestal být příjemně ovladatelný.";
            k2.Controls.Add(txtMereni);
            s.Controls.Add(k2);

            var k3 = NovaKarta("Prahy a chování", 4, 488, 676, 176);
            k3.Controls.Add(Popisek("Odpočet ve varovném okně", 16, 56, 200));
            cOdpocet = new Cislovac(); cOdpocet.Location = new Point(230, 51); cOdpocet.Minimum = 5; cOdpocet.Maximum = 600; cOdpocet.Krok = 5;
            k3.Controls.Add(cOdpocet);
            k3.Controls.Add(Slaby("sekund", 346, 57, 60, 20));

            k3.Controls.Add(Popisek("Práh klidu — procesor", 16, 94, 200));
            cPrahCPU = new Cislovac(); cPrahCPU.Location = new Point(230, 89); cPrahCPU.Minimum = 1; cPrahCPU.Maximum = 100; cPrahCPU.Krok = 5;
            k3.Controls.Add(cPrahCPU);
            k3.Controls.Add(Slaby("%", 346, 95, 40, 20));

            k3.Controls.Add(Popisek("Práh klidu — grafika", 16, 132, 200));
            cPrahGPU = new Cislovac(); cPrahGPU.Location = new Point(230, 127); cPrahGPU.Minimum = 1; cPrahGPU.Maximum = 100; cPrahGPU.Krok = 5;
            k3.Controls.Add(cPrahGPU);
            k3.Controls.Add(Slaby("%", 346, 133, 40, 20));

            pSpanek = new Prepinac(); pSpanek.Text = "Zakázat uspávání a hibernaci";
            pSpanek.Location = new Point(400, 52); pSpanek.Size = new Size(270, 26);
            k3.Controls.Add(pSpanek);
            k3.Controls.Add(Slaby("Doporučeno — počítač pak zůstane dostupný\npřes síť i vzdálenou plochu.", 400, 84, 270, 40));
            s.Controls.Add(k3);

            return s;
        }

        // ---------------- stranka: noc ----------------
        Panel StrNoc()
        {
            var s = NovaStranka();
            s.Controls.Add(Nadpis("Noční přepnutí", 4, 4));
            s.Controls.Add(Slaby("Přepne se až poté, co systém utichne — běžící render nebo výpočet to nepřeruší.", 4, 34, 636, 20));

            var k1 = NovaKarta("Ve všední dny", 4, 62, 676, 252);
            pTyden = new Prepinac(); pTyden.Text = "Zapnuto"; pTyden.Location = new Point(16, 50); pTyden.Size = new Size(160, 26);
            k1.Controls.Add(pTyden);

            k1.Controls.Add(Popisek("Které dny", 16, 88, 120));
            dnyTyden = new VyberDnu(); dnyTyden.Location = new Point(16, 110); dnyTyden.Size = new Size(280, 34);
            k1.Controls.Add(dnyTyden);

            k1.Controls.Add(Popisek("Nejdřív v", 16, 156, 120));
            VlozCas(k1, 16, 178, out cTydenHod, out cTydenMin);

            k1.Controls.Add(Popisek("Až bude počítač v klidu", 330, 156, 200));
            cTydenKlid = Klid(k1, 330, 178);
            k1.Controls.Add(Slaby("minut", 446, 185, 60, 20));

            pTydenTV = new Prepinac(); pTydenTV.Text = "Vypnout televizi"; pTydenTV.Location = new Point(330, 60); pTydenTV.Size = new Size(190, 26);
            k1.Controls.Add(pTydenTV);
            pTydenZamk = new Prepinac(); pTydenZamk.Text = "Zamknout počítač"; pTydenZamk.Location = new Point(330, 96); pTydenZamk.Size = new Size(190, 26);
            k1.Controls.Add(pTydenZamk);
            s.Controls.Add(k1);

            var k2 = NovaKarta("O víkendu", 4, 326, 676, 252);
            pVikend = new Prepinac(); pVikend.Text = "Zapnuto"; pVikend.Location = new Point(16, 50); pVikend.Size = new Size(160, 26);
            k2.Controls.Add(pVikend);

            k2.Controls.Add(Popisek("Které dny", 16, 88, 120));
            dnyVikend = new VyberDnu(); dnyVikend.Location = new Point(16, 110); dnyVikend.Size = new Size(280, 34);
            k2.Controls.Add(dnyVikend);

            k2.Controls.Add(Popisek("Nejdřív v", 16, 156, 120));
            VlozCas(k2, 16, 178, out cVikendHod, out cVikendMin);

            k2.Controls.Add(Popisek("Až bude počítač v klidu", 330, 156, 200));
            cVikendKlid = Klid(k2, 330, 178);
            k2.Controls.Add(Slaby("minut", 446, 185, 60, 20));

            pVikendTV = new Prepinac(); pVikendTV.Text = "Vypnout televizi"; pVikendTV.Location = new Point(330, 60); pVikendTV.Size = new Size(190, 26);
            k2.Controls.Add(pVikendTV);
            pVikendZamk = new Prepinac(); pVikendZamk.Text = "Zamknout počítač"; pVikendZamk.Location = new Point(330, 96); pVikendZamk.Size = new Size(190, 26);
            k2.Controls.Add(pVikendZamk);
            s.Controls.Add(k2);

            return s;
        }

        // ---------------- stranka: zamek ----------------
        Panel StrZamek()
        {
            var s = NovaStranka();
            s.Controls.Add(Nadpis("Zamknutí počítače", 4, 4));

            var k1 = NovaKarta("Po zamknutí", 4, 46, 676, 200);
            pZamknuti = new Prepinac(); pZamknuti.Text = "Přepnout do úsporného režimu"; pZamknuti.Location = new Point(16, 52); pZamknuti.Size = new Size(300, 26);
            k1.Controls.Add(pZamknuti);
            k1.Controls.Add(Popisek("Až bude počítač v klidu", 16, 94, 200));
            cZamkKlid = Klid(k1, 16, 116);
            k1.Controls.Add(Slaby("minut", 132, 123, 60, 20));
            pZamknutiTV = new Prepinac(); pZamknutiTV.Text = "Vypnout i televizi"; pZamknutiTV.Location = new Point(16, 158); pZamknutiTV.Size = new Size(240, 26);
            k1.Controls.Add(pZamknutiTV);
            k1.Controls.Add(Slaby("Když se vrátíte a odemknete dřív, než uplyne doba klidu,\npřepnutí se zruší a počítač zůstane na plném výkonu.", 300, 100, 320, 44));
            s.Controls.Add(k1);

            var k2 = NovaKarta("Po odemknutí", 4, 258, 676, 140);
            pOdemknuti = new Prepinac(); pOdemknuti.Text = "Vrátit běžný režim"; pOdemknuti.Location = new Point(16, 52); pOdemknuti.Size = new Size(300, 26);
            k2.Controls.Add(pOdemknuti);
            k2.Controls.Add(Slaby("Proběhne okamžitě a bez ptaní. Doporučeno nechat zapnuté — jinak zůstane\npočítač zpomalený, dokud režim nepřepnete ručně.", 16, 88, 604, 44));
            s.Controls.Add(k2);

            return s;
        }

        // ---------------- stranka: TV ----------------
        Panel StrTV()
        {
            var s = NovaStranka();
            s.Controls.Add(Nadpis("Televize", 4, 4));
            s.Controls.Add(Slaby("Podporovány jsou televize LG s webOS.", 4, 34, 636, 20));

            var k1 = NovaKarta("Připojení", 4, 62, 676, 230);
            pTV = new Prepinac(); pTV.Text = "Ovládat televizi"; pTV.Location = new Point(16, 52); pTV.Size = new Size(240, 26);
            k1.Controls.Add(pTV);

            k1.Controls.Add(Popisek("IP adresa", 16, 92, 100));
            poleIP = new Pole(); poleIP.Location = new Point(16, 114); poleIP.Size = new Size(170, 32);
            k1.Controls.Add(poleIP);

            k1.Controls.Add(Popisek("Port", 200, 92, 60));
            polePort = new Pole(); polePort.Location = new Point(200, 114); polePort.Size = new Size(70, 32);
            k1.Controls.Add(polePort);

            var bHledej = new Tlacitko(); bHledej.Text = "Najít na síti"; bHledej.Size = new Size(130, 32); bHledej.Location = new Point(286, 114);
            bHledej.Click += delegate { HledejTV(); };
            k1.Controls.Add(bHledej);

            var bParuj = new Tlacitko(); bParuj.Text = "Spárovat"; bParuj.Size = new Size(140, 34); bParuj.Location = new Point(16, 166);
            bParuj.Click += delegate { UlozTV(); SpustPS("-Sparovat", "lg-tv.ps1", true); };
            k1.Controls.Add(bParuj);

            var bVypni = new Tlacitko(); bVypni.Text = "Vyzkoušet vypnutí"; bVypni.Size = new Size(160, 34); bVypni.Location = new Point(168, 166);
            bVypni.Click += delegate { UlozTV(); SpustPS("-Vypnout", "lg-tv.ps1", true); };
            k1.Controls.Add(bVypni);

            k1.Controls.Add(Slaby("Při prvním spárování se televize zeptá — potvrďte dotaz dálkovým ovladačem.\nKlíč se uloží a příště už se ptát nebude. Vypnutí uvede televizi do pohotovostního\nrežimu, takže zůstane dostupná na síti a lze ji znovu zapnout.", 344, 166, 280, 56));
            s.Controls.Add(k1);

            return s;
        }

        // ---------------- stranka: stav ----------------
        Panel StrStav()
        {
            var s = NovaStranka();
            s.Controls.Add(Nadpis("Stav a záznam", 4, 4));

            var k = NovaKarta("", 4, 46, 676, 590);
            txtStav = new TextBox();
            txtStav.Multiline = true; txtStav.ScrollBars = ScrollBars.Vertical; txtStav.ReadOnly = true;
            txtStav.BorderStyle = BorderStyle.None;
            txtStav.Location = new Point(14, 14); txtStav.Size = new Size(648, 518);
            txtStav.BackColor = Barvy.Karta; txtStav.ForeColor = Barvy.TextSlaby;
            txtStav.Font = Pisma.Mono;
            k.Controls.Add(txtStav);

            var b1 = new Tlacitko(); b1.Text = "Obnovit"; b1.Size = new Size(110, 32); b1.Location = new Point(14, 542);
            b1.Click += delegate { ObnovStav(); };
            k.Controls.Add(b1);
            var b2 = new Tlacitko(); b2.Text = "Otevřít složku"; b2.Size = new Size(130, 32); b2.Location = new Point(134, 542);
            b2.Click += delegate { Process.Start("explorer.exe", slozka); };
            k.Controls.Add(b2);
            var b3 = new Tlacitko(); b3.Text = "Odebrat všechny úlohy"; b3.Size = new Size(180, 32); b3.Location = new Point(274, 542);
            b3.BarvaHover = Barvy.Cervena;
            b3.Click += delegate { OdebratUlohy(); };
            k.Controls.Add(b3);

            s.Controls.Add(k);
            return s;
        }

        // ---------------- spodni lista ----------------
        void SpodniLista()
        {
            var sp = new Panel();
            sp.Dock = DockStyle.Bottom; sp.Height = 64; sp.BackColor = Barvy.Pozadi;
            sp.Paint += delegate (object s, PaintEventArgs e)
            {
                using (var p = new Pen(Barvy.Okraj)) e.Graphics.DrawLine(p, 0, 0, sp.Width, 0);
            };
            panSpodni = sp;
            Controls.Add(sp);

            var bUloz = new Tlacitko();
            bUloz.Text = "Uložit a nastavit úlohy"; bUloz.JakoHlavni();
            bUloz.Size = new Size(210, 38); bUloz.Location = new Point(236, 14);
            bUloz.Click += delegate { Uloz(); };
            sp.Controls.Add(bUloz);

            var bTest = new Tlacitko();
            bTest.Text = "Vyzkoušet přepnutí teď"; bTest.Size = new Size(190, 38); bTest.Location = new Point(456, 14);
            bTest.Click += delegate { Test(); };
            sp.Controls.Add(bTest);

            var bZavri = new Tlacitko();
            bZavri.Text = "Zavřít"; bZavri.Size = new Size(100, 38); bZavri.Location = new Point(ClientSize.Width - 124, 14);
            bZavri.Click += delegate { Close(); };
            sp.Controls.Add(bZavri);
        }

        // ---------------- zivy stav ----------------
        [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr koren, out IntPtr guid);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr h);

        /// <summary>Aktivni schema primo z Windows - levnejsi nez spoustet
        /// powercfg kazde tri vteriny.</summary>
        string AktivniPlan()
        {
            IntPtr p = IntPtr.Zero;
            try
            {
                if (PowerGetActiveScheme(IntPtr.Zero, out p) != 0 || p == IntPtr.Zero) return "";
                return ((Guid)Marshal.PtrToStructure(p, typeof(Guid))).ToString();
            }
            catch { return ""; }
            finally { if (p != IntPtr.Zero) LocalFree(p); }
        }

        string NazevPlanu(string guid)
        {
            foreach (var pl in plany)
                if (string.Equals(pl.Guid, guid, StringComparison.OrdinalIgnoreCase)) return pl.Nazev;
            return "neznámé schéma";
        }

        /// <summary>Nejblizsi naplanovane prepnuti podle ulozeneho nastaveni.</summary>
        string DalsiAkce()
        {
            if (zmeneno) return "Nastavení není uloženo — úlohy zatím běží podle předchozího.";

            DateTime? nej = null;
            string co = "";
            if (n.B("Tyden_Povoleno")) NejblizsiDen(n.S("Tyden_Dny"), n.S("Tyden_Cas"), "úsporný režim", ref nej, ref co);
            if (n.B("Vikend_Povoleno")) NejblizsiDen(n.S("Vikend_Dny"), n.S("Vikend_Cas"), "úsporný režim", ref nej, ref co);

            if (nej == null)
            {
                if (n.B("Zamknuti_Povoleno")) return "Další akce: až zamknete počítač a systém utichne.";
                return "Žádné automatické přepnutí není zapnuté.";
            }

            var d = nej.Value;
            string kdy = d.Date == DateTime.Today ? "dnes"
                       : d.Date == DateTime.Today.AddDays(1) ? "zítra"
                       : DnyCz[((int)d.DayOfWeek + 6) % 7].ToLower();
            return string.Format("Další akce: {0} v {1:HH:mm} → {2}", kdy, d, co);
        }

        void NejblizsiDen(string dny, string cas, string co, ref DateTime? nej, ref string popis)
        {
            if (string.IsNullOrEmpty(dny)) return;
            var t = ParseCas(cas);
            for (int i = 0; i < 8; i++)
            {
                var kandidat = DateTime.Today.AddDays(i).AddHours(t.Hour).AddMinutes(t.Minute);
                if (kandidat <= DateTime.Now) continue;
                if (dny.IndexOf(kandidat.DayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (nej == null || kandidat < nej.Value) { nej = kandidat; popis = co; }
                return;
            }
        }

        void ObnovPruh()
        {
            if (pruh == null) return;

            string aktivni = AktivniPlan();
            string guidUspora = n.S("PlanUspora");
            bool uspora = !string.IsNullOrEmpty(aktivni) && string.Equals(aktivni, guidUspora, StringComparison.OrdinalIgnoreCase);

            pruh.Uspora = uspora;
            pruh.Rezim = uspora ? "Úsporný režim" : "Běžný režim";
            pruh.Detail = string.IsNullOrEmpty(aktivni)
                ? "schéma se nepodařilo zjistit"
                : "schéma " + NazevPlanu(aktivni) + (uspora ? "   ·   výkon omezen na " + hodnotaMax + " %" : "   ·   plný výkon");
            pruh.Dalsi = DalsiAkce();

            if (!n.B("TV_Povoleno") || n.S("TV_IP") == "") pruh.Televize = "";
            else pruh.Televize = File.Exists(Path.Combine(slozka, "lg-tv-key.txt"))
                ? "Televize " + n.S("TV_IP") + " · spárována"
                : "Televize " + n.S("TV_IP") + " · nespárována";

            pruh.Neulozeno = zmeneno;
            pruh.Invalidate();
        }

        // ---------------- data ----------------
        void NactiPlany()
        {
            plany.Clear();
            foreach (Match m in Regex.Matches(Prikaz("powercfg.exe", "/list"), @"GUID:\s*([0-9a-fA-F\-]{36})\s*\(([^)]*)\)"))
                plany.Add(new Plan { Guid = m.Groups[1].Value, Nazev = m.Groups[2].Value.Trim() });
        }

        void NaplnVyber(ComboBox c, string guid)
        {
            c.Items.Clear();
            foreach (var p in plany) c.Items.Add(p);
            for (int i = 0; i < plany.Count; i++)
                if (string.Equals(plany[i].Guid, guid, StringComparison.OrdinalIgnoreCase)) { c.SelectedIndex = i; return; }
            if (c.Items.Count > 0) c.SelectedIndex = 0;
        }

        string GuidZVyberu(ComboBox c) { var p = c.SelectedItem as Plan; return p != null ? p.Guid : ""; }

        int Omez(int v, int min, int max) { return Math.Max(min, Math.Min(max, v)); }

        DateTime ParseCas(string s) { DateTime v; return DateTime.TryParse("2000-01-01 " + s, out v) ? v : DateTime.Today; }

        // Skripty pracuji s poctem mereni a intervalem mezi nimi. Uzivateli
        // ukazujeme jen vyslednou dobu klidu v minutach; interval drzime
        // na trech minutach, coz je rozumny kompromis mezi reakci a zatezi.
        const int IntervalS = 180;

        int NaMinuty(int kolikrat, int interval)
        {
            if (interval <= 0) interval = IntervalS;
            int m = (int)Math.Round(kolikrat * interval / 60.0 / 3.0) * 3;
            return Omez(m, 3, 120);
        }

        int NaPocet(int minut) { return Math.Max(1, (int)Math.Round(minut * 60.0 / IntervalS)); }

        void NacistDoFormulare()
        {
            nacitam = true;
            try { PlnFormular(); } finally { nacitam = false; }
        }

        void PlnFormular()
        {
            NaplnVyber(cmbBezny, n.S("PlanBezny"));
            NaplnVyber(cmbUspora, n.S("PlanUspora"));

            hodnotaMax = Omez(n.I("MaxStavProcesoru"), 5, 100);
            ZobrazShrnuti();
            cOdpocet.Hodnota = Omez(n.I("Odpocet"), 5, 600);
            cPrahCPU.Hodnota = Omez(n.I("PrahCPU"), 1, 100);
            cPrahGPU.Hodnota = Omez(n.I("PrahGPU"), 1, 100);
            pSpanek.Zapnuto = n.B("ZakazatSpanek");

            pTyden.Zapnuto = n.B("Tyden_Povoleno");
            var casT = ParseCas(n.S("Tyden_Cas"));
            cTydenHod.Hodnota = casT.Hour; cTydenMin.Hodnota = casT.Minute;
            cTydenKlid.Hodnota = NaMinuty(n.I("Tyden_KolKlidu"), n.I("Tyden_IntervalS"));
            pTydenTV.Zapnuto = n.B("Tyden_VypnoutTV");
            pTydenZamk.Zapnuto = n.B("Tyden_Zamknout");
            dnyTyden.Dny = n.S("Tyden_Dny");

            pVikend.Zapnuto = n.B("Vikend_Povoleno");
            var casV = ParseCas(n.S("Vikend_Cas"));
            cVikendHod.Hodnota = casV.Hour; cVikendMin.Hodnota = casV.Minute;
            cVikendKlid.Hodnota = NaMinuty(n.I("Vikend_KolKlidu"), n.I("Vikend_IntervalS"));
            pVikendTV.Zapnuto = n.B("Vikend_VypnoutTV");
            pVikendZamk.Zapnuto = n.B("Vikend_Zamknout");
            dnyVikend.Dny = n.S("Vikend_Dny");

            pZamknuti.Zapnuto = n.B("Zamknuti_Povoleno");
            cZamkKlid.Hodnota = NaMinuty(n.I("Zamknuti_KolKlidu"), n.I("Zamknuti_IntervalS"));
            pZamknutiTV.Zapnuto = n.B("Zamknuti_VypnoutTV");
            pOdemknuti.Zapnuto = n.B("Odemknuti_Povoleno");

            pTV.Zapnuto = n.B("TV_Povoleno");
            poleIP.Text = n.S("TV_IP");
            polePort.Text = n.S("TV_Port");
        }

        void ZobrazShrnuti()
        {
            if (lblShrnuti == null) return;
            string popis;
            if (hodnotaMax >= 90) popis = "téměř bez omezení";
            else if (hodnotaMax >= 70) popis = "mírná úspora";
            else if (hodnotaMax >= 45) popis = "vyvážená úspora";
            else if (hodnotaMax >= 25) popis = "výrazná úspora";
            else popis = "maximální úspora";
            lblShrnuti.Text = "Nastaveno: " + popis + "   (omezení výkonu na " + hodnotaMax + " %)";
        }

        void UlozTV()        {
            n.Set("TV_IP", poleIP.Text.Trim());
            n.Set("TV_Port", polePort.Text.Trim());
            n.Set("TV_Povoleno", pTV.Zapnuto);
            try { n.Uloz(); } catch { }
        }

        bool Uloz()
        {
            if (GuidZVyberu(cmbBezny) == GuidZVyberu(cmbUspora))
            {
                MessageBox.Show("Běžný a úsporný režim nemohou být stejné schéma.", "Nelze uložit",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            n.Set("PlanBezny", GuidZVyberu(cmbBezny));
            n.Set("PlanUspora", GuidZVyberu(cmbUspora));
            n.Set("MaxStavProcesoru", hodnotaMax);
            n.Set("Odpocet", cOdpocet.Hodnota);
            n.Set("PrahCPU", cPrahCPU.Hodnota);
            n.Set("PrahGPU", cPrahGPU.Hodnota);
            n.Set("ZakazatSpanek", pSpanek.Zapnuto);

            n.Set("Tyden_Povoleno", pTyden.Zapnuto);
            n.Set("Tyden_Cas", string.Format("{0:00}:{1:00}", cTydenHod.Hodnota, cTydenMin.Hodnota));
            n.Set("Tyden_Dny", dnyTyden.Dny);
            n.Set("Tyden_KolKlidu", NaPocet(cTydenKlid.Hodnota));
            n.Set("Tyden_IntervalS", IntervalS);
            n.Set("Tyden_VypnoutTV", pTydenTV.Zapnuto);
            n.Set("Tyden_Zamknout", pTydenZamk.Zapnuto);

            n.Set("Vikend_Povoleno", pVikend.Zapnuto);
            n.Set("Vikend_Cas", string.Format("{0:00}:{1:00}", cVikendHod.Hodnota, cVikendMin.Hodnota));
            n.Set("Vikend_Dny", dnyVikend.Dny);
            n.Set("Vikend_KolKlidu", NaPocet(cVikendKlid.Hodnota));
            n.Set("Vikend_IntervalS", IntervalS);
            n.Set("Vikend_VypnoutTV", pVikendTV.Zapnuto);
            n.Set("Vikend_Zamknout", pVikendZamk.Zapnuto);

            n.Set("Zamknuti_Povoleno", pZamknuti.Zapnuto);
            n.Set("Zamknuti_KolKlidu", NaPocet(cZamkKlid.Hodnota));
            n.Set("Zamknuti_IntervalS", IntervalS);
            n.Set("Zamknuti_VypnoutTV", pZamknutiTV.Zapnuto);
            n.Set("Odemknuti_Povoleno", pOdemknuti.Zapnuto);

            n.Set("TV_Povoleno", pTV.Zapnuto);
            n.Set("TV_IP", poleIP.Text.Trim());
            n.Set("TV_Port", polePort.Text.Trim());

            try
            {
                n.Uloz();
                Cursor = Cursors.WaitCursor;
                SpustPSSync("", "nastav-ulohy.ps1", 90000);
                Cursor = Cursors.Default;
                zmeneno = false;
                MessageBox.Show("Nastavení uloženo a úlohy přenastaveny.", "Hotovo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ObnovStav();
                ObnovPruh();
                return true;
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Uložení selhalo:\r\n" + ex.Message, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        void Zmer()
        {
            if (MessageBox.Show("Měření na chvíli přepne režim napájení a zatíží procesor.\r\nTrvá asi minutu. Spustit?",
                "Změřit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            btnMer.Enabled = false;
            txtMereni.Text = "Měřím, chvíli strpení...";
            Application.DoEvents();
            Cursor = Cursors.WaitCursor;
            try
            {
                string v = SpustPSVystup("-PlanUspora \"" + GuidZVyberu(cmbUspora) + "\" -PlanBezny \"" + GuidZVyberu(cmbBezny) + "\"",
                                         "zmer-vykon.ps1", 600000);
                if (string.IsNullOrEmpty(v.Trim())) { txtMereni.Text = "Měření nevrátilo výsledek."; }
                else
                {
                    var md = Regex.Match(v, @"DOPORUCENO=(\d+)");
                    string zobraz = Regex.Replace(v, @"\r?\nDOPORUCENO=\d+\s*$", "").Trim();
                    txtMereni.Text = zobraz;
                    if (md.Success)
                    {
                        int dop;
                        if (int.TryParse(md.Groups[1].Value, out dop))
                        {
                            hodnotaMax = Omez(dop, 5, 100);
                            ZobrazShrnuti();
                            Zmena(this, EventArgs.Empty);
                            MessageBox.Show("Doporučená hodnota " + dop + " % byla nastavena.\r\n\r\nUložte tlačítkem dole, aby se projevila.",
                                "Změřeno", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex) { txtMereni.Text = "Měření selhalo: " + ex.Message; }
            Cursor = Cursors.Default;
            btnMer.Enabled = true;
        }

        void HledejTV()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                string v = SpustPSVystup("-Hledat", "lg-tv.ps1", 120000).Trim();
                var m = Regex.Match(v, @"(\d{1,3}(?:\.\d{1,3}){3})");
                if (m.Success) { poleIP.Text = m.Groups[1].Value; MessageBox.Show("Nalezena televize: " + m.Groups[1].Value, "Hotovo"); }
                else MessageBox.Show("Televize nenalezena.\r\n\r\n" + v, "Nenalezeno");
            }
            catch (Exception ex) { MessageBox.Show("Hledání selhalo: " + ex.Message, "Chyba"); }
            Cursor = Cursors.Default;
        }

        void OdebratUlohy()
        {
            if (MessageBox.Show("Odebrat všechny naplánované úlohy tohoto programu?\r\nNastavení zůstane zachováno.",
                "Odebrat úlohy", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            SpustPSSync("-Odebrat", "nastav-ulohy.ps1", 60000);
            ObnovStav();
        }

        void Test()
        {
            if (MessageBox.Show("Přepnout teď do úsporného režimu?\r\nZobrazí se okno s odpočtem, které lze zrušit.",
                "Vyzkoušet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                SpustPS("-Akce Uspora", "prepni-plan.ps1", false);
        }

        ProcessStartInfo PS(string skript, string argy, bool vystup)
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + Path.Combine(slozka, skript) + "\" " + argy);
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            if (vystup) { psi.RedirectStandardOutput = true; psi.StandardOutputEncoding = Encoding.UTF8; }
            return psi;
        }

        void SpustPS(string argy, string skript, bool cekat)
        {
            if (!File.Exists(Path.Combine(slozka, skript))) { MessageBox.Show("Chybí soubor: " + skript, "Chyba"); return; }
            var p = Process.Start(PS(skript, argy, false));
            if (cekat && p != null) { p.WaitForExit(180000); ObnovStav(); }
        }

        void SpustPSSync(string argy, string skript, int timeout)
        {
            if (!File.Exists(Path.Combine(slozka, skript))) throw new FileNotFoundException("Chybí soubor: " + skript);
            var p = Process.Start(PS(skript, argy, false));
            if (p != null) p.WaitForExit(timeout);
        }

        string SpustPSVystup(string argy, string skript, int timeout)
        {
            if (!File.Exists(Path.Combine(slozka, skript))) throw new FileNotFoundException("Chybí soubor: " + skript);
            var p = Process.Start(PS(skript, argy, true));
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit(timeout);
            return o;
        }

        string Prikaz(string exe, string argy)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, argy);
                psi.RedirectStandardOutput = true; psi.UseShellExecute = false; psi.CreateNoWindow = true;
                try { psi.StandardOutputEncoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage); } catch { }
                var p = Process.Start(psi);
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit(15000);
                return o;
            }
            catch (Exception ex) { return "chyba: " + ex.Message; }
        }

        void ObnovStav()
        {
            if (txtStav == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("AKTIVNÍ REŽIM NAPÁJENÍ");
            sb.AppendLine("──────────────────────────────────────────────");
            sb.AppendLine(Prikaz("powercfg.exe", "/getactivescheme").Trim());
            sb.AppendLine();
            sb.AppendLine("NAPLÁNOVANÉ ÚLOHY");
            sb.AppendLine("──────────────────────────────────────────────");
            string u = Prikaz("powershell.exe",
                "-NoProfile -Command \"Get-ScheduledTask -TaskName 'NapajeniManager-*' -ErrorAction SilentlyContinue | ForEach-Object { $i = Get-ScheduledTaskInfo -TaskName $_.TaskName; '{0,-32} {1,-9} {2}' -f $_.TaskName, $_.State, $(if($i.NextRunTime){$i.NextRunTime.ToString('dd.MM. HH:mm')}else{'(pri udalosti)'}) }\"").Trim();
            sb.AppendLine(u.Length > 0 ? u : "(žádné)");
            sb.AppendLine();
            sb.AppendLine("POSLEDNÍ ZÁZNAMY");
            sb.AppendLine("──────────────────────────────────────────────");
            string log = Path.Combine(slozka, "prepni-plan.log");
            if (File.Exists(log))
            {
                try
                {
                    var radky = File.ReadAllLines(log, Encoding.UTF8);
                    for (int i = Math.Max(0, radky.Length - 20); i < radky.Length; i++) sb.AppendLine(radky[i]);
                }
                catch { sb.AppendLine("(log se nepodařilo přečíst)"); }
            }
            else sb.AppendLine("(zatím žádné)");

            txtStav.Text = sb.ToString();
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HlavniOkno());
        }
    }
}