using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// frmDirectory (FRMDIREC.BAS), where Backup and Restore are pointed. The caption is "Select " +
/// Tag + " Directory" (Form_Resize); it opens on BasePath + "BACKUP\" (gc1C7C, Form_Load), shows
/// the path upper-cased in lblDirectory, and lblInfo says in blue (QBColor 1) or red (QBColor 4)
/// whether the backup's files are there (sub34AE). OK answers the directory the list is open on,
/// Cancel answers "" (cmdCommand_Click).
///
/// Not the original in one respect, deliberately: the original's drive list and directory list
/// reach the whole machine. The port's list is kept inside the saves directory, so the BACKUP
/// folder is <c>saves\BACKUP</c> and neither Backup nor Restore can write anywhere else. The
/// saves directory itself is in the list, and choosing it gets fn0662's refusal, as choosing the
/// DATA directory did. The two buttons' captions are not in the decompiled source (the form's
/// layout is not); OK and Cancel are what cmdCommand(0) and (1) do.
/// </summary>
public static class DirectoryDialog
{
    public static async System.Threading.Tasks.Task<string?> Ask(Window owner, string tag, string savesDir)
    {
        string root = Path.GetFullPath(savesDir);
        string start = Path.Combine(root, "BACKUP");
        // the installer made BASE\BACKUP; the port makes saves\BACKUP when it is first wanted
        try { Directory.CreateDirectory(start); } catch { start = root; }
        string current = start;
        string? answer = null;

        var lblDirectory = new TextBlock { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
        var lblInfo = new TextBlock { FontWeight = FontWeight.Bold };
        var dirs = new ListBox { Height = 180, Background = Brush.Parse("#0e0e14") };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 90, HorizontalContentAlignment = HorizontalAlignment.Center };

        var win = new Window
        {
            Title = "Select " + tag + " Directory",
            Width = 420, SizeToContent = SizeToContent.Height, CanResize = false,
            Background = Brush.Parse("#101018"),
            FontFamily = owner.FontFamily,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        // a DirListBox: the open path's folders down from the top, then the one it is open on's
        // subfolders; a double-click opens a line (dirDirectory_Change)
        string[] lines = Array.Empty<string>();
        void Open(string path)
        {
            current = path;
            lblDirectory.Text = current.ToUpperInvariant();
            // sub34AE
            bool found = Backups.Found(current);
            lblInfo.Foreground = found ? Brush.Parse("#6f8fff") : Brush.Parse("#e04040");
            lblInfo.Text = found ? "BACKUP FILES FOUND IN THIS DIRECTORY" : "BACKUP FILES NOT FOUND IN THIS DIRECTORY";

            var chain = new System.Collections.Generic.List<string>();
            for (string? p = current; p != null; p = Path.GetDirectoryName(p))
            {
                chain.Insert(0, p);
                if (Backups.SameDir(p, root)) break;
            }
            string[] subs;
            try { subs = Directory.GetDirectories(current).OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToArray(); }
            catch { subs = Array.Empty<string>(); }
            lines = chain.Concat(subs).ToArray();
            dirs.ItemsSource = lines.Select((p, i) =>
            {
                int depth = i < chain.Count ? i : chain.Count;
                string name = i == 0 ? p : Path.GetFileName(p);
                return new string(' ', depth * 2) + (i < chain.Count ? "[-] " : "[+] ") + name;
            }).ToList();
            dirs.SelectedIndex = chain.Count - 1;
        }
        dirs.DoubleTapped += (_, _) =>
        {
            int i = dirs.SelectedIndex;
            if (i >= 0 && i < lines.Length) Open(lines[i]);
        };
        ok.Click += (_, _) => { answer = current; win.Close(); };
        cancel.Click += (_, _) => { answer = ""; win.Close(); };
        Open(start);

        win.Content = new Border
        {
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    lblDirectory,
                    dirs,
                    lblInfo,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, Children = { ok, cancel } },
                },
            },
        };
        await OriginalBox.ShowOver(win, owner);
        return answer;
    }
}

/// <summary>
/// The Common Dialog's Font box, as <c>dlgDialog.Action = 4</c> opens it: Windows' own ChooseFont
/// with CF_SCREENFONTS (Flags = 1, FRMOPTIO.BAS:284), started on the current font and size, and
/// only the name and the size read back (FRMOPTIO.BAS:289-291). Cancel returns false. Anywhere
/// but Windows there is no such dialog and nothing changes.
/// </summary>
public static class FontDialog
{
    public static bool Choose(Window owner, ref string font, ref int size)
    {
        if (!OperatingSystem.IsWindows()) return false;
        IntPtr hwnd = owner.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        var lf = new LOGFONT { lfFaceName = font, lfWeight = 400 };
        IntPtr dc = GetDC(IntPtr.Zero);
        int dpi = dc != IntPtr.Zero ? GetDeviceCaps(dc, LOGPIXELSY) : 96;
        if (dc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, dc);
        lf.lfHeight = -(int)Math.Round(size * dpi / 72.0);

        IntPtr plf = Marshal.AllocHGlobal(Marshal.SizeOf<LOGFONT>());
        try
        {
            Marshal.StructureToPtr(lf, plf, false);
            var cf = new CHOOSEFONT
            {
                lStructSize = Marshal.SizeOf<CHOOSEFONT>(),
                hwndOwner = hwnd,
                lpLogFont = plf,
                Flags = CF_SCREENFONTS | CF_INITTOLOGFONTSTRUCT,
            };
            if (!ChooseFontW(ref cf)) return false;
            var got = Marshal.PtrToStructure<LOGFONT>(plf);
            font = got.lfFaceName ?? "";
            size = (int)Math.Round(cf.iPointSize / 10.0);
            return true;
        }
        finally { Marshal.FreeHGlobal(plf); }
    }

    private const int CF_SCREENFONTS = 0x1;
    private const int CF_INITTOLOGFONTSTRUCT = 0x40;
    private const int LOGPIXELSY = 90;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LOGFONT
    {
        public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
        public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CHOOSEFONT
    {
        public int lStructSize;
        public IntPtr hwndOwner, hDC, lpLogFont;
        public int iPointSize, Flags, rgbColors;
        public IntPtr lCustData, lpfnHook, lpTemplateName, hInstance, lpszStyle;
        public short nFontType, alignment;
        public int nSizeMin, nSizeMax;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] private static extern bool ChooseFontW(ref CHOOSEFONT cf);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr dc, int index);
}
