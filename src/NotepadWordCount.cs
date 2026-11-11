// 字数统计 —— 右键菜单版
// 编译：csc /target:winexe /optimize+ /out:字数统计.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll 字数统计.cs
// 依赖 Windows 自带的 .NET Framework 4.8，不打包运行时

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

static class Program
{
    [DllImport("shcore.dll")]
    static extern int SetProcessDpiAwareness(int value);

    [STAThread]
    static void Main(string[] args)
    {
        try { SetProcessDpiAwareness(2); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string path = null;
        foreach (var a in args)
            if (!string.IsNullOrWhiteSpace(a) && !a.StartsWith("--") && File.Exists(a)) { path = a; break; }

        if (path == null)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选一个文本文件";
                dlg.Filter = "文本文件|*.txt;*.md;*.log;*.ini;*.csv;*.json|所有文件|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                path = dlg.FileName;
            }
        }

        string text;
        try { text = ReadText(path); }
        catch (Exception ex)
        {
            MessageBox.Show("读不了这个文件：\n" + ex.Message, "字数统计", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new StatsForm(text, Path.GetFileName(path)));
    }

    static string ReadText(string path)
    {
        byte[] raw = File.ReadAllBytes(path);

        // 按 BOM 判断，BOM 不算进正文
        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            return new UTF8Encoding(false).GetString(raw, 3, raw.Length - 3);
        if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE)
            return Encoding.Unicode.GetString(raw, 2, raw.Length - 2);
        if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(raw, 2, raw.Length - 2);

        // 没有 BOM：先按严格 UTF-8（遇到非法字节会抛异常），失败再按 GBK
        try
        {
            return new UTF8Encoding(false, true).GetString(raw);
        }
        catch
        {
            try { return Encoding.GetEncoding(936).GetString(raw); }
            catch { return Encoding.UTF8.GetString(raw); }
        }
    }
}

class StatsForm : Form
{
    // 汉字：基本区 + 扩展 A + 兼容区 + 〇
    static readonly Regex Cjk = new Regex(@"[\u3007\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff]");
    // 汉字扩展 B 及以后（代理对）
    static readonly Regex CjkSurrogate = new Regex(@"[\uD840-\uD87F][\uDC00-\uDFFF]");
    // 中文标点：只认标点，不把全角字母数字算进来
    static readonly Regex CjkPunct = new Regex(
        @"[\u3000-\u303F\uFF01-\uFF0F\uFF1A-\uFF20\uFF3B-\uFF40\uFF5B-\uFF65\u2018\u2019\u201C\u201D\u2013\u2014\u2026\u00B7\u30FB]");
    static readonly Regex WordRe = new Regex(@"[A-Za-z]+(?:['\-][A-Za-z]+)*");
    static readonly Regex DigitRun = new Regex(@"\d+(?:[.,]\d+)*");
    static readonly Regex Space = new Regex(@"\s");

    float _scale = 1f;

    int S(int v)
    {
        return (int)Math.Round(v * (double)_scale);
    }

    public StatsForm(string text, string source)
    {
        Text = "字数统计";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.None;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Font = new Font("Microsoft YaHei UI", 9F);
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        float dpi;
        using (var g0 = CreateGraphics()) dpi = g0.DpiX;
        _scale = dpi / 96f;

        var fBig = new Font("Microsoft YaHei UI", 26F, FontStyle.Bold);
        var fUnit = new Font("Microsoft YaHei UI", 12F);
        var fSub = new Font("Microsoft YaHei UI", 9F);
        var fKey = new Font("Microsoft YaHei UI", 9F);
        var fVal = new Font("Microsoft YaHei UI", 10F);
        var fSmall = new Font("Microsoft YaHei UI", 8.5F);
        var fBtn = new Font("Microsoft YaHei UI", 9F);

        // ── 先把换行统一，避免 \r 被当成字符、也避免多算一行 ──
        string norm = text.Replace("\r\n", "\n").Replace("\r", "\n");

        int cjk = Cjk.Matches(norm).Count + CjkSurrogate.Matches(norm).Count;
        int words = WordRe.Matches(norm).Count;
        int digits = DigitRun.Matches(norm).Count;
        int punct = CjkPunct.Matches(norm).Count;

        int chars = norm.Length;                                   // 含空格、含换行
        int noSpace = chars - Space.Matches(norm).Count;           // 不含任何空白

        // 行数：末尾那个换行不算多一行
        int lineCount = 0;
        if (norm.Length > 0)
        {
            lineCount = norm.Split('\n').Length;
            if (norm.EndsWith("\n")) lineCount--;
        }

        int paras = 0;
        foreach (var ln in norm.Split('\n'))
            if (ln.Trim().Length > 0) paras++;

        int total = cjk + words + digits;   // 跟 Word 的口径接近：汉字 + 英文词 + 数字

        int pad = S(18);
        int y = S(14);

        // ── 头部：大字 ──
        string bigNum = total.ToString();
        Size bigSize = TextRenderer.MeasureText(bigNum, fBig);
        var lblBig = new Label
        {
            Text = bigNum,
            Font = fBig,
            ForeColor = Color.FromArgb(43, 108, 176),
            AutoSize = true,
            Location = new Point(pad, y),
            BackColor = Color.White
        };
        Controls.Add(lblBig);

        var lblUnit = new Label { Text = "字", Font = fUnit, AutoSize = true, BackColor = Color.White };
        Size unitSize = TextRenderer.MeasureText("字", fUnit);
        lblUnit.Location = new Point(pad + bigSize.Width, y + bigSize.Height - unitSize.Height - S(6));
        Controls.Add(lblUnit);

        var lblSub = new Label
        {
            Text = string.Format("汉字 {0} · 英文 {1} 词 · 数字 {2}", cjk, words, digits),
            Font = fSub,
            ForeColor = Color.Gray,
            AutoSize = true,
            BackColor = Color.White
        };
        Size subSize = TextRenderer.MeasureText(lblSub.Text, fSub);
        lblSub.Location = new Point(pad + bigSize.Width + unitSize.Width + S(14), y + bigSize.Height - subSize.Height - S(8));
        Controls.Add(lblSub);

        int headerBottom = y + bigSize.Height;
        int headerWidth = bigSize.Width + unitSize.Width + S(14) + subSize.Width;

        // ── 明细行 ──
        string[] keys = new string[]
        {
            "字符（含空格）", "字符（不含空格）", "中文标点",
            "行数", "段落（非空行）"
        };
        string[] vals = new string[]
        {
            chars.ToString(), noSpace.ToString(), punct.ToString(),
            lineCount.ToString(), paras.ToString()
        };

        int keyW = 0, valW = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            keyW = Math.Max(keyW, TextRenderer.MeasureText(keys[i], fKey).Width);
            valW = Math.Max(valW, TextRenderer.MeasureText(vals[i], fVal).Width);
        }
        int valX = pad + keyW + S(30);

        int ry = headerBottom + S(14);
        for (int i = 0; i < keys.Length; i++)
        {
            var lk = new Label
            {
                Text = keys[i],
                Font = fKey,
                ForeColor = Color.FromArgb(85, 85, 85),
                AutoSize = true,
                Location = new Point(pad, ry + S(2)),
                BackColor = Color.White
            };
            var lv = new Label
            {
                Text = vals[i],
                Font = fVal,
                AutoSize = true,
                Location = new Point(valX, ry),
                BackColor = Color.White
            };
            Controls.Add(lk);
            Controls.Add(lv);
            ry += S(21);
        }
        int rowsBottom = ry;
        int rowsWidth = valX + valW;

        // ── 文件名 ──
        string shown = source.Length > 40 ? source.Substring(0, 40) + "…" : source;
        var lblSrc = new Label
        {
            Text = "文件：" + shown,
            Font = fSmall,
            ForeColor = Color.DimGray,
            AutoSize = true,
            BackColor = Color.White
        };
        Size srcSize = TextRenderer.MeasureText(lblSrc.Text, fSmall);
        lblSrc.Location = new Point(pad, rowsBottom + S(10));
        Controls.Add(lblSrc);
        int srcBottom = rowsBottom + S(10) + srcSize.Height;

        // ── 按钮 ──
        var btnClose = new Button { Text = "关闭 (Esc)", Font = fBtn, AutoSize = true };
        Size closeSize = TextRenderer.MeasureText(btnClose.Text, fBtn);
        btnClose.Size = new Size(closeSize.Width + S(24), S(30));

        var btnCopy = new Button { Text = "复制结果", Font = fBtn, AutoSize = true };
        Size copySize = TextRenderer.MeasureText(btnCopy.Text, fBtn);
        btnCopy.Size = new Size(copySize.Width + S(24), S(30));

        int btnY = srcBottom + S(14);
        int buttonsWidth = btnCopy.Width + S(8) + btnClose.Width;

        // ── 撑开窗口 ──
        int width = Math.Max(Math.Max(headerWidth, rowsWidth), Math.Max(srcSize.Width, buttonsWidth)) + pad * 2;
        width = Math.Max(width, S(280));
        int height = btnY + btnClose.Height + S(16);
        ClientSize = new Size(width, height);

        btnClose.Location = new Point(width - pad - btnClose.Width, btnY);
        btnCopy.Location = new Point(btnClose.Left - S(8) - btnCopy.Width, btnY);
        btnCopy.ForeColor = Color.FromArgb(43, 108, 176);

        btnClose.Click += (s, e) => Close();
        btnCopy.Click += (s, e) =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("字数：" + total);
            sb.AppendLine("汉字：" + cjk);
            sb.AppendLine("英文词：" + words);
            sb.AppendLine("数字：" + digits);
            sb.AppendLine("字符（含空格）：" + chars);
            sb.AppendLine("字符（不含空格）：" + noSpace);
            sb.AppendLine("中文标点：" + punct);
            sb.AppendLine("行数：" + lineCount);
            sb.AppendLine("段落：" + paras);
            try { Clipboard.SetText(sb.ToString()); btnCopy.Text = "已复制"; } catch { }
        };

        Controls.Add(btnClose);
        Controls.Add(btnCopy);

        // 出现在鼠标旁边，不出屏
        Load += (s, e) =>
        {
            var mp = Cursor.Position;
            var wa = Screen.FromPoint(mp).WorkingArea;
            int x = Math.Min(Math.Max(wa.Left, mp.X - Width / 2), wa.Right - Width - S(8));
            int yy = Math.Min(Math.Max(wa.Top, mp.Y + S(18)), wa.Bottom - Height - S(8));
            Location = new Point(x, yy);
        };

        var timer = new Timer { Interval = 30000 };
        timer.Tick += (s, e) => { timer.Stop(); Close(); };
        timer.Start();
    }
}
