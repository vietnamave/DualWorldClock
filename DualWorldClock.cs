using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;

[assembly: System.Reflection.AssemblyTitle("Dual World Clock")]
[assembly: System.Reflection.AssemblyDescription("Two customizable desktop clocks for Windows")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace DualWorldClock {
    static class Program {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static void Main() {
            bool first;
            using (Mutex mutex = new Mutex(true, "Local\\DualWorldClock_Public_v1", out first)) {
                if (!first) { MessageBox.Show("時計は既に起動しています。通知領域の時計アイコンから表示できます。", "Dual World Clock"); return; }
                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ClockWindow());
            }
        }
    }
    sealed class Preferences {
        public string Zone1="Tokyo Standard Time", Zone2="SE Asia Standard Time";
        public string Label1="東京", Label2="ホーチミン";
        public bool Top=true;
        public int Size=100;
        public static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DualWorldClock", "settings.xml"); } }
        public static Preferences Load() {
            Preferences p=new Preferences();
            try {
                XmlDocument d=new XmlDocument(); d.XmlResolver=null; d.Load(FilePath);
                XmlElement n=d.DocumentElement;
                p.Zone1=n.GetAttribute("zone1"); p.Zone2=n.GetAttribute("zone2");
                TimeZoneInfo.FindSystemTimeZoneById(p.Zone1); TimeZoneInfo.FindSystemTimeZoneById(p.Zone2);
                p.Label1=n.GetAttribute("label1"); p.Label2=n.GetAttribute("label2");
                p.Top=n.GetAttribute("top")!="false";
                int s; if(int.TryParse(n.GetAttribute("size"),out s) && s>=25 && s<=150) p.Size=s;
                return p;
            } catch { return new Preferences(); }
        }
        public void Save() {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            XmlDocument d=new XmlDocument(); XmlElement n=d.CreateElement("clocks"); d.AppendChild(n);
            n.SetAttribute("zone1",Zone1); n.SetAttribute("zone2",Zone2);
            n.SetAttribute("label1",Label1); n.SetAttribute("label2",Label2);
            n.SetAttribute("top",Top?"true":"false"); n.SetAttribute("size",Size.ToString());
            string temp=FilePath+".tmp"; d.Save(temp);
            if(File.Exists(FilePath)) File.Replace(temp,FilePath,null); else File.Move(temp,FilePath);
        }
    }
    sealed class ClockWindow : Form {
        Preferences prefs=Preferences.Load();
        TimeZoneInfo zone1,zone2;
        System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        NotifyIcon tray;
        ContextMenuStrip menu;
        float zoom=1;
        Point dragStart;
        bool dragging;
        Color cyan=Color.FromArgb(87,213,233), gold=Color.FromArgb(236,184,108);
        public ClockWindow() {
            Text="Dual World Clock";
            FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false;
            BackColor=Color.Magenta; TransparencyKey=Color.Magenta;
            DoubleBuffered=true; StartPosition=FormStartPosition.Manual;
            AutoScaleMode=AutoScaleMode.None;
            menu=new ContextMenuStrip();
            menu.Items.Add("時計の設定…",null,delegate { Settings(); });
            menu.Items.Add("右下へ戻す",null,delegate { DockBottomRight(); Show(); });
            menu.Items.Add("時計を表示",null,delegate { Show(); DockBottomRight(); Activate(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("終了",null,delegate { Close(); });
            ContextMenuStrip=menu;
            tray=new NotifyIcon(); tray.Icon=SystemIcons.Application; tray.Text="Dual World Clock — ダブルクリックで表示";
            tray.ContextMenuStrip=menu; tray.Visible=true;
            tray.DoubleClick+=delegate { Show(); DockBottomRight(); Activate(); };
            ApplyPreferences();
            timer.Interval=100; timer.Tick+=delegate { Invalidate(); }; timer.Start();
            Shown+=delegate { DockBottomRight(); };
            MouseDown+=delegate(object sender,MouseEventArgs e) { if(e.Button==MouseButtons.Left) { dragging=true; dragStart=e.Location; Capture=true; } };
            MouseMove+=delegate(object sender,MouseEventArgs e) { if(dragging) Location=new Point(Left+e.X-dragStart.X,Top+e.Y-dragStart.Y); };
            MouseUp+=delegate { dragging=false; Capture=false; };
            MouseDoubleClick+=delegate(object sender,MouseEventArgs e) { if(e.Button==MouseButtons.Left) Settings(); };
        }
        void ApplyPreferences() {
            zone1=TimeZoneInfo.FindSystemTimeZoneById(prefs.Zone1); zone2=TimeZoneInfo.FindSystemTimeZoneById(prefs.Zone2);
            using(Graphics g=CreateGraphics()) zoom=g.DpiX/96f*prefs.Size/100f;
            ClientSize=new Size((int)(460*zoom),(int)(258*zoom)); TopMost=prefs.Top; Invalidate();
        }
        void DockBottomRight() {
            Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;
            Location=new Point(Math.Max(area.Left,area.Right-Width-18),Math.Max(area.Top,area.Bottom-Height-18));
        }
        void Settings() {
            using(SettingsWindow s=new SettingsWindow(prefs)) {
                if(s.ShowDialog(this)==DialogResult.OK) {
                    prefs=s.Result; ApplyPreferences(); DockBottomRight();
                    try { prefs.Save(); } catch(Exception ex) { MessageBox.Show(this,"表示は変更しましたが、設定を保存できませんでした。\n"+ex.Message,"保存エラー"); }
                }
            }
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); Graphics g=e.Graphics;
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.ScaleTransform(zoom,zoom);
            DateTime utc=DateTime.UtcNow;
            DrawClock(g,118,116,TimeZoneInfo.ConvertTimeFromUtc(utc,zone1),zone1,utc,prefs.Label1,cyan);
            DrawClock(g,342,116,TimeZoneInfo.ConvertTimeFromUtc(utc,zone2),zone2,utc,prefs.Label2,gold);
            using(GraphicsPath path=Rounded(new RectangleF(92,237,276,20),10))
            using(SolidBrush b=new SolidBrush(Color.FromArgb(25,29,36))) g.FillPath(b,path);
            TextAt(g,"右クリック：設定    ·    ドラッグ：移動",230,238,8,Color.FromArgb(169,180,191),FontStyle.Regular,268,20);
        }
        static GraphicsPath Rounded(RectangleF r,float radius) {
            GraphicsPath p=new GraphicsPath(); float d=radius*2;
            p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
        }
        void DrawClock(Graphics g,float cx,float cy,DateTime t,TimeZoneInfo zone,DateTime utc,string label,Color accent) {
            RectangleF outer=new RectangleF(cx-109,cy-109,218,218);
            using(LinearGradientBrush b=new LinearGradientBrush(outer,Color.FromArgb(158,170,184),Color.FromArgb(32,39,50),55)) g.FillEllipse(b,outer);
            using(SolidBrush b=new SolidBrush(Color.FromArgb(8,12,19))) g.FillEllipse(b,cx-106,cy-106,212,212);
            RectangleF face=new RectangleF(cx-102,cy-102,204,204);
            using(LinearGradientBrush b=new LinearGradientBrush(face,Color.FromArgb(33,42,55),Color.FromArgb(12,17,25),90)) g.FillEllipse(b,face);
            using(Pen p=new Pen(Color.FromArgb(62,75,92),1)) g.DrawEllipse(p,face);
            for(int i=0;i<60;i++) {
                double a=i*Math.PI/30; float r=i%5==0?84:90;
                using(Pen p=new Pen(i%5==0?Color.FromArgb(215,224,234):Color.FromArgb(94,108,125),i%5==0?2:1))
                    g.DrawLine(p,cx+(float)Math.Sin(a)*r,cy-(float)Math.Cos(a)*r,cx+(float)Math.Sin(a)*96,cy-(float)Math.Cos(a)*96);
            }
            TextAt(g,"12",cx,cy-80,11,Color.FromArgb(229,235,242),FontStyle.Bold);
            TextAt(g,"3",cx+75,cy-8,11,Color.FromArgb(229,235,242),FontStyle.Bold);
            TextAt(g,"6",cx,cy+63,11,Color.FromArgb(229,235,242),FontStyle.Bold);
            TextAt(g,"9",cx-75,cy-8,11,Color.FromArgb(229,235,242),FontStyle.Bold);
            TextAt(g,label.Length==0?zone.StandardName:label,cx,cy-49,10,accent,FontStyle.Bold);
            TimeSpan offset=zone.GetUtcOffset(utc);
            string off="UTC"+(offset.Ticks<0?"−":"+")+offset.Duration().ToString(@"hh\:mm");
            TextAt(g,off,cx,cy-32,7,Color.FromArgb(145,161,179),FontStyle.Regular);
            DrawDigital(g,t.ToString("HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture),cx,cy+24);
            double sec=t.Second+t.Millisecond/1000.0, min=t.Minute+sec/60, hour=t.Hour%12+min/60;
            Hand(g,cx,cy,hour*Math.PI/6,48,5,Color.FromArgb(225,232,240),6);
            Hand(g,cx,cy,min*Math.PI/30,70,3,Color.FromArgb(243,247,250),8);
            Hand(g,cx,cy,sec*Math.PI/30,79,1.3f,accent,15);
            using(SolidBrush b=new SolidBrush(accent)) g.FillEllipse(b,cx-4,cy-4,8,8);
            using(SolidBrush b=new SolidBrush(Color.FromArgb(15,21,29))) g.FillEllipse(b,cx-1.5f,cy-1.5f,3,3);
        }
        static void Hand(Graphics g,float x,float y,double a,float length,float width,Color color,float tail) {
            using(Pen p=new Pen(color,width)) { p.StartCap=LineCap.Round; p.EndCap=LineCap.Round;
                g.DrawLine(p,x-(float)Math.Sin(a)*tail,y+(float)Math.Cos(a)*tail,x+(float)Math.Sin(a)*length,y-(float)Math.Cos(a)*length); }
        }
        static void DrawDigital(Graphics g,string text,float cx,float y) {
            RectangleF area=new RectangleF(cx-77,y,154,30);
            using(StringFormat sf=(StringFormat)StringFormat.GenericTypographic.Clone()) {
                sf.Alignment=StringAlignment.Center; sf.LineAlignment=StringAlignment.Center;
                sf.FormatFlags=StringFormatFlags.NoWrap; sf.Trimming=StringTrimming.None;
                float size=16f;
                Font font=new Font("Consolas",size,FontStyle.Regular,GraphicsUnit.Pixel);
                try {
                    // Use logical pixels: the graphics transform already includes display DPI.
                    // Fit all eight characters without wrapping or adding an ellipsis.
                    while(size>8f) {
                        SizeF measured=g.MeasureString(text,font,new SizeF(1000,1000),sf);
                        if(measured.Width<=area.Width-8 && measured.Height<=area.Height-4) break;
                        font.Dispose(); size-=0.5f;
                        font=new Font("Consolas",size,FontStyle.Regular,GraphicsUnit.Pixel);
                    }
                    using(SolidBrush brush=new SolidBrush(Color.FromArgb(227,234,242)))
                        g.DrawString(text,font,brush,area,sf);
                } finally { font.Dispose(); }
            }
        }
        static void TextAt(Graphics g,string text,float x,float y,float size,Color color,FontStyle style,float width=138,float height=23) {
            using(Font f=new Font("Yu Gothic UI",size*96f/72f,style,GraphicsUnit.Pixel)) using(SolidBrush b=new SolidBrush(color)) using(StringFormat sf=new StringFormat()) {
                sf.Alignment=StringAlignment.Center; sf.Trimming=StringTrimming.EllipsisCharacter; sf.FormatFlags=StringFormatFlags.NoWrap;
                g.DrawString(text,f,b,new RectangleF(x-width/2,y,width,height),sf);
            }
        }
        protected override void Dispose(bool disposing) {
            if(disposing) { timer.Stop(); timer.Dispose(); if(tray!=null) { tray.Visible=false; tray.Dispose(); } if(menu!=null) menu.Dispose(); }
            base.Dispose(disposing);
        }
    }
    sealed class ZoneItem {
        public TimeZoneInfo Zone;
        public ZoneItem(TimeZoneInfo z) { Zone=z; }
        public override string ToString() {
            string prefix="";
            switch(Zone.Id) {
                case "Tokyo Standard Time": prefix="日本 / 東京 — "; break;
                case "SE Asia Standard Time": prefix="ベトナム / ホーチミン・ハノイ — "; break;
                case "GMT Standard Time": prefix="イギリス / ロンドン — "; break;
                case "Eastern Standard Time": prefix="アメリカ / ニューヨーク — "; break;
                case "Pacific Standard Time": prefix="アメリカ / ロサンゼルス — "; break;
                case "China Standard Time": prefix="中国 / 上海 — "; break;
                case "Singapore Standard Time": prefix="シンガポール — "; break;
            }
            return prefix+Zone.DisplayName;
        }
    }
    sealed class SettingsWindow : Form {
        ComboBox a=new ComboBox(),b=new ComboBox(),sizes=new ComboBox();
        TextBox la=new TextBox(),lb=new TextBox(); CheckBox top=new CheckBox();
        public Preferences Result;
        public SettingsWindow(Preferences p) {
            Text="Dual World Clock | 時計の設定"; Font=new Font("Yu Gothic UI",10);
            AutoScaleMode=AutoScaleMode.Dpi; ClientSize=new Size(610,392);
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
            StartPosition=FormStartPosition.CenterParent;
            AddLabel("左の時計 · タイムゾーン",20,18); Configure(a,20,44);
            AddLabel("表示名",20,86); la.SetBounds(105,82,470,28); la.Text=p.Label1; la.MaxLength=24; Controls.Add(la);
            AddLabel("右の時計 · タイムゾーン",20,130); Configure(b,20,156);
            AddLabel("表示名",20,198); lb.SetBounds(105,194,470,28); lb.Text=p.Label2; lb.MaxLength=24; Controls.Add(lb);
            foreach(TimeZoneInfo z in TimeZoneInfo.GetSystemTimeZones()) {
                int i=a.Items.Add(new ZoneItem(z)); b.Items.Add(new ZoneItem(z));
                if(z.Id==p.Zone1) a.SelectedIndex=i; if(z.Id==p.Zone2) b.SelectedIndex=i;
            }
            a.SelectionChangeCommitted+=delegate { la.Text=Suggested(((ZoneItem)a.SelectedItem).Zone); };
            b.SelectionChangeCommitted+=delegate { lb.Text=Suggested(((ZoneItem)b.SelectedItem).Zone); };
            AddLabel("時計の大きさ",20,246); sizes.SetBounds(140,242,110,30); sizes.DropDownStyle=ComboBoxStyle.DropDownList;
            sizes.Items.AddRange(new object[]{"25%","50%","75%","100%","125%","150%"}); sizes.SelectedItem=p.Size+"%"; if(sizes.SelectedIndex<0) sizes.SelectedIndex=3; Controls.Add(sizes);
            top.Text="ほかのウィンドウより手前に表示"; top.SetBounds(275,242,305,30); top.Checked=p.Top; Controls.Add(top);
            AddLabel("時刻はPCの時計を参照します。夏時間はタイムゾーンに合わせて自動反映。",20,290);
            Button ok=new Button(); ok.Text="保存"; ok.SetBounds(390,340,90,32);
            ok.Click+=delegate {
                Result=new Preferences(); Result.Zone1=((ZoneItem)a.SelectedItem).Zone.Id; Result.Zone2=((ZoneItem)b.SelectedItem).Zone.Id;
                Result.Label1=la.Text.Trim(); Result.Label2=lb.Text.Trim(); Result.Top=top.Checked;
                Result.Size=int.Parse(sizes.SelectedItem.ToString().TrimEnd('%')); DialogResult=DialogResult.OK;
            }; Controls.Add(ok); AcceptButton=ok;
            Button cancel=new Button(); cancel.Text="キャンセル"; cancel.SetBounds(490,340,100,32); cancel.DialogResult=DialogResult.Cancel; Controls.Add(cancel); CancelButton=cancel;
        }
        static string Suggested(TimeZoneInfo z) {
            switch(z.Id) {
                case "Tokyo Standard Time": return "東京";
                case "SE Asia Standard Time": return "ホーチミン";
                case "GMT Standard Time": return "ロンドン";
                case "Eastern Standard Time": return "ニューヨーク";
                case "Pacific Standard Time": return "ロサンゼルス";
                case "China Standard Time": return "上海";
                case "Singapore Standard Time": return "シンガポール";
                default: return z.StandardName;
            }
        }
        void AddLabel(string s,int x,int y) { Label l=new Label(); l.Text=s; l.AutoSize=true; l.Location=new Point(x,y); Controls.Add(l); }
        void Configure(ComboBox c,int x,int y) { c.SetBounds(x,y,570,30); c.DropDownStyle=ComboBoxStyle.DropDownList; c.DropDownWidth=760; Controls.Add(c); }
    }
}
