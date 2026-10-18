// Colours of the app icon ("stereo pixel sprite": a coral card in front of teal cards on deep indigo)
// applied to the WinForms controls, which otherwise draw in the light system colours.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Flato3D
{
    static class Theme
    {
        public static readonly Color Bg = Hex(0x2D2C47), Header = Hex(0x25243C), Surface = Hex(0x23223A), Raised = Hex(0x3E3D60),
                                     RaisedHover = Hex(0x4A4970), Border = Hex(0x56547E), Text = Hex(0xECEBF5), Dim = Hex(0xA9A8C4),
                                     Coral = Hex(0xD85A50), CoralHover = Hex(0xE56C62), Teal = Hex(0x36A3A8), Selection = Hex(0x2E6A72);
        // game list rows: tested profile, the user's own results, hard this way
        public static readonly Color Profile = Hex(0x7FD6C9), Works = Hex(0x5CC8C3), Issues = Hex(0xE8A33D), Broken = Hex(0xEA6A60), Muted = Hex(0x807F99);

        static Color Hex(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        // Colours every control under root (recursively); lists get owner-drawn headers and rows.
        public static void Apply(Control root)
        {
            Style(root);
            foreach (Control c in root.Controls) Apply(c);
        }

        static void Style(Control c)
        {
            if (c is Button)
            {
                var b = (Button)c;
                if (b.Tag as string == "primary") return;   // keeps its coral
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.MouseOverBackColor = RaisedHover;
                b.FlatAppearance.MouseDownBackColor = Border;
                b.BackColor = Raised;
                b.ForeColor = Text;
                b.UseVisualStyleBackColor = false;
            }
            else if (c is TextBox)
            {
                var t = (TextBox)c;
                t.BackColor = Surface;
                t.ForeColor = Text;
                t.BorderStyle = BorderStyle.FixedSingle;
                if (t.Multiline) DarkScroll(c, "DarkMode_Explorer");
            }
            else if (c is ComboBox)
            {
                var cb = (ComboBox)c;
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = Surface;
                cb.ForeColor = Text;
                DarkScroll(c, "DarkMode_CFD");
            }
            else if (c is NumericUpDown)
            {
                c.BackColor = Surface;
                c.ForeColor = Text;
                ((NumericUpDown)c).BorderStyle = BorderStyle.FixedSingle;
                foreach (Control part in c.Controls) DarkScroll(part, "DarkMode_Explorer");   // the spin buttons
            }
            else if (c is CheckBox || c is RadioButton)
            {
                // the standard box keeps a visible check mark; only the text follows the theme
                c.ForeColor = Text;
                c.BackColor = Color.Transparent;
            }
            else if (c is ListView || c is CheckedListBox || c is ListBox)
            {
                c.BackColor = Surface;
                c.ForeColor = Text;
                DarkScroll(c, "DarkMode_Explorer");
                if (c is ListView) StyleList((ListView)c);
                else ((ListBox)c).BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is PictureBox) { if (c.BackColor == SystemColors.Control) c.BackColor = Surface; }
            else if (c is Label) c.ForeColor = c.ForeColor == SystemColors.ControlText ? Text : c.ForeColor;
            else
            {
                if (c.BackColor == SystemColors.Control || c.BackColor == Color.Transparent || c is Form) c.BackColor = Bg;
                c.ForeColor = Text;
            }
        }

        public static void Primary(Button b)
        {
            b.Tag = "primary";
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Coral;
            b.FlatAppearance.MouseOverBackColor = CoralHover;
            b.FlatAppearance.MouseDownBackColor = Coral;
            b.BackColor = Coral;
            b.ForeColor = Color.White;
            b.UseVisualStyleBackColor = false;
        }

        // Details view lists: dark header cells and rows, the selection in teal, the last column filling the width.
        static void StyleList(ListView lv)
        {
            if (lv.OwnerDraw) return;
            lv.OwnerDraw = true;
            lv.BorderStyle = BorderStyle.None;
            lv.DrawColumnHeader += (s, e) =>
            {
                using (var b = new SolidBrush(Raised)) e.Graphics.FillRectangle(b, e.Bounds);
                using (var p = new Pen(Border)) e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
                var r = e.Bounds;
                r.Inflate(-6, 0);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, lv.Font, r, Dim,
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            lv.DrawItem += (s, e) => { };
            lv.DrawSubItem += (s, e) =>
            {
                bool sel = e.Item.Selected;
                using (var b = new SolidBrush(sel ? Selection : Surface)) e.Graphics.FillRectangle(b, e.Bounds);
                var r = e.Bounds;
                r.Inflate(-6, 0);
                Color fore = sel ? Color.White : (e.Item.ForeColor == SystemColors.WindowText || e.Item.ForeColor.IsEmpty ? Text : e.Item.ForeColor);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, lv.Font, r, fore,
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            // the strip right of the last column would stay light: the last column fills the list
            EventHandler fill = (s, e) => { if (lv.Columns.Count > 0) lv.Columns[lv.Columns.Count - 1].Width = -2; };
            lv.Resize += fill;
            lv.HandleCreated += fill;
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        // Windows 10 1809+ dark scroll bars / combo boxes for one control (ignored where unsupported).
        static void DarkScroll(Control c, string theme)
        {
            EventHandler set = (s, e) => { try { SetWindowTheme(c.Handle, theme, null); } catch { } };
            if (c.IsHandleCreated) set(c, EventArgs.Empty);
            c.HandleCreated += set;
        }

        // "Flato" in coral and "3D" in teal as one word (two labels leave a gap between them).
        public sealed class Wordmark : Control
        {
            public Wordmark(Font font)
            {
                Font = font;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
                BackColor = Header;
                var f = TextFormatFlags.NoPadding;
                Size a = TextRenderer.MeasureText("Flato", font, Size.Empty, f), b = TextRenderer.MeasureText("3D", font, Size.Empty, f);
                Size = new Size(a.Width + b.Width + 2, Math.Max(a.Height, b.Height) + 2);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                var f = TextFormatFlags.NoPadding;
                int w = TextRenderer.MeasureText(e.Graphics, "Flato", Font, Size.Empty, f).Width;
                TextRenderer.DrawText(e.Graphics, "Flato", Font, new Point(0, 0), Coral, f);
                TextRenderer.DrawText(e.Graphics, "3D", Font, new Point(w + 1, 0), Teal, f);
            }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, int[] ids, uint count, uint flags);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr icon);

        // The exe's own icon at an exact size (the .ico holds 16..256 px), or null.
        public static Bitmap AppIcon(int size)
        {
            var h = new IntPtr[1];
            var id = new int[1];
            try
            {
                if (PrivateExtractIcons(Application.ExecutablePath, 0, size, size, h, id, 1, 0) != 1 || h[0] == IntPtr.Zero) return null;
                using (var ic = Icon.FromHandle(h[0])) return ic.ToBitmap();
            }
            catch { return null; }
            finally { if (h[0] != IntPtr.Zero) DestroyIcon(h[0]); }
        }

        // Dark title bar (Windows 10 2004+), in the header colour on Windows 11.
        public static void TitleBar(Form f)
        {
            f.HandleCreated += (s, e) =>
            {
                try
                {
                    int on = 1, caption = Header.R | (Header.G << 8) | (Header.B << 16), text = Text.R | (Text.G << 8) | (Text.B << 16);
                    DwmSetWindowAttribute(f.Handle, 20, ref on, 4);        // DWMWA_USE_IMMERSIVE_DARK_MODE
                    DwmSetWindowAttribute(f.Handle, 35, ref caption, 4);   // DWMWA_CAPTION_COLOR
                    DwmSetWindowAttribute(f.Handle, 36, ref text, 4);      // DWMWA_TEXT_COLOR
                }
                catch { }
            };
        }
    }
}
