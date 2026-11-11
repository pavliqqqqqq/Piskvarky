using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Piskvorky
{
    /// <summary>
    /// Kresleni bez Content Pipeline: vlastni bitmapovy font 5x7 (bez diakritiky, ta se pri vykresleni odstrani),
    /// kameny a pomocne obdelniky.
    /// </summary>
    public static class Gfx
    {
        public static Texture2D Pixel;
        static Texture2D stoneBlack, stoneWhite;
        static readonly Dictionary<char, Texture2D> Glyphs = new Dictionary<char, Texture2D>();

        static readonly string[] Defs =
        {
            "A=01110,10001,10001,11111,10001,10001,10001",
            "B=11110,10001,10001,11110,10001,10001,11110",
            "C=01110,10001,10000,10000,10000,10001,01110",
            "D=11110,10001,10001,10001,10001,10001,11110",
            "E=11111,10000,10000,11110,10000,10000,11111",
            "F=11111,10000,10000,11110,10000,10000,10000",
            "G=01110,10001,10000,10111,10001,10001,01111",
            "H=10001,10001,10001,11111,10001,10001,10001",
            "I=01110,00100,00100,00100,00100,00100,01110",
            "J=00111,00010,00010,00010,00010,10010,01100",
            "K=10001,10010,10100,11000,10100,10010,10001",
            "L=10000,10000,10000,10000,10000,10000,11111",
            "M=10001,11011,10101,10101,10001,10001,10001",
            "N=10001,11001,10101,10011,10001,10001,10001",
            "O=01110,10001,10001,10001,10001,10001,01110",
            "P=11110,10001,10001,11110,10000,10000,10000",
            "Q=01110,10001,10001,10001,10101,10010,01101",
            "R=11110,10001,10001,11110,10100,10010,10001",
            "S=01111,10000,10000,01110,00001,00001,11110",
            "T=11111,00100,00100,00100,00100,00100,00100",
            "U=10001,10001,10001,10001,10001,10001,01110",
            "V=10001,10001,10001,10001,10001,01010,00100",
            "W=10001,10001,10001,10101,10101,11011,10001",
            "X=10001,10001,01010,00100,01010,10001,10001",
            "Y=10001,10001,01010,00100,00100,00100,00100",
            "Z=11111,00001,00010,00100,01000,10000,11111",
            "0=01110,10001,10011,10101,11001,10001,01110",
            "1=00100,01100,00100,00100,00100,00100,01110",
            "2=01110,10001,00001,00010,00100,01000,11111",
            "3=11110,00001,00001,01110,00001,00001,11110",
            "4=00010,00110,01010,10010,11111,00010,00010",
            "5=11111,10000,11110,00001,00001,10001,01110",
            "6=00110,01000,10000,11110,10001,10001,01110",
            "7=11111,00001,00010,00100,01000,01000,01000",
            "8=01110,10001,10001,01110,10001,10001,01110",
            "9=01110,10001,10001,01111,00001,00010,01100",
            ":=00000,00100,00100,00000,00100,00100,00000",
            ".=00000,00000,00000,00000,00000,01100,01100",
            ",=00000,00000,00000,00000,01100,00100,01000",
            "%=11001,11010,00010,00100,01000,01011,10011",
            "-=00000,00000,00000,11111,00000,00000,00000",
            "/=00001,00010,00010,00100,01000,01000,10000",
            "(=00010,00100,01000,01000,01000,00100,00010",
            ")=01000,00100,00010,00010,00010,00100,01000",
            "!=00100,00100,00100,00100,00100,00000,00100",
            "?=01110,10001,00001,00010,00100,00000,00100",
            "+=00000,00100,00100,11111,00100,00100,00000",
            "<=00010,00100,01000,10000,01000,00100,00010",
            ">=01000,00100,00010,00001,00010,00100,01000",
            "_=00000,00000,00000,00000,00000,00000,11111",
        };

        public static void Init(GraphicsDevice gd)
        {
            Pixel = new Texture2D(gd, 1, 1);
            Pixel.SetData(new[] { Color.White });

            foreach (var def in Defs)
            {
                char ch = def[0];
                var rows = def.Substring(2).Split(',');
                var tex = new Texture2D(gd, 5, 7);
                var data = new Color[35];
                for (int r = 0; r < 7; r++)
                    for (int c = 0; c < 5; c++)
                        data[r * 5 + c] = rows[r][c] == '1' ? Color.White : Color.Transparent;
                tex.SetData(data);
                Glyphs[ch] = tex;
            }

            stoneBlack = MakeStone(gd, new Color(8, 8, 8), new Color(120, 120, 125));
            stoneWhite = MakeStone(gd, new Color(165, 165, 170), new Color(250, 250, 250));
        }

        static Texture2D MakeStone(GraphicsDevice gd, Color dark, Color light)
        {
            const int sz = 64;
            var t = new Texture2D(gd, sz, sz);
            var data = new Color[sz * sz];
            float r = sz / 2f - 1;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float dx = x + 0.5f - sz / 2f, dy = y + 0.5f - sz / 2f;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float a = Math.Clamp(r - d + 0.5f, 0f, 1f);
                    float hx = x + 0.5f - 22f, hy = y + 0.5f - 22f;
                    float h = MathF.Sqrt(hx * hx + hy * hy);
                    float tt = Math.Clamp(h / 50f, 0f, 1f);
                    Color col = Color.Lerp(light, dark, tt);
                    data[y * sz + x] = new Color((int)(col.R * a), (int)(col.G * a), (int)(col.B * a), (int)(255 * a));
                }
            t.SetData(data);
            return t;
        }

        public static string Norm(string s)
        {
            var d = s.ToUpperInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char ch in d)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            return sb.ToString();
        }

        public static int TextW(string s, int sc) => Math.Max(0, Norm(s).Length * 6 * sc - sc);

        public static void Text(SpriteBatch sb, string s, int x, int y, int sc, Color col)
        {
            string n = Norm(s);
            int cx = x;
            foreach (char ch in n)
            {
                if (Glyphs.TryGetValue(ch, out var g))
                    sb.Draw(g, new Rectangle(cx, y, 5 * sc, 7 * sc), col);
                cx += 6 * sc;
            }
        }

        public static void TextC(SpriteBatch sb, string s, int cx, int y, int sc, Color col)
            => Text(sb, s, cx - TextW(s, sc) / 2, y, sc, col);

        /// <summary>Zalamovany text (podporuje \n). Vraci pocet radku.</summary>
        public static int Wrapped(SpriteBatch sb, string s, int x, int y, int maxChars, int sc, Color col, int lineH)
        {
            int lines = 0;
            foreach (var para in s.Split('\n'))
            {
                var words = para.Split(' ');
                string line = "";
                foreach (var w in words)
                {
                    if (line.Length > 0 && line.Length + 1 + w.Length > maxChars)
                    {
                        Text(sb, line, x, y + lines * lineH, sc, col);
                        lines++;
                        line = w;
                    }
                    else line = line.Length == 0 ? w : line + " " + w;
                }
                Text(sb, line, x, y + lines * lineH, sc, col);
                lines++;
            }
            return lines;
        }

        public static void Fill(SpriteBatch sb, Rectangle r, Color c) => sb.Draw(Pixel, r, c);

        public static void Frame(SpriteBatch sb, Rectangle r, Color c, int t)
        {
            sb.Draw(Pixel, new Rectangle(r.X, r.Y, r.Width, t), c);
            sb.Draw(Pixel, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
            sb.Draw(Pixel, new Rectangle(r.X, r.Y, t, r.Height), c);
            sb.Draw(Pixel, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
        }

        /// <summary>color 1 = cerny kamen, 2 = bily kamen.</summary>
        public static void Stone(SpriteBatch sb, int color, Rectangle r, float alpha = 1f)
        {
            var tex = color == 1 ? stoneBlack : stoneWhite;
            sb.Draw(tex, r, Color.White * alpha);
        }
    }
}
