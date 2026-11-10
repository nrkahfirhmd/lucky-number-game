using System;
using UnityEngine;

namespace NoWinners
{
    // A texture you draw on with rectangles and 8x8 text, in top-left pixel coordinates.
    // Everything on the screens and desk labels goes through this, so it all sits on the grid.
    public class Canvas8
    {
        public readonly int W, H;
        public readonly Texture2D Tex;
        readonly Color32[] px;
        readonly bool mips;

        public Canvas8(int w, int h, bool mips = false)
        {
            W = w; H = h; this.mips = mips;
            px = new Color32[w * h];
            Tex = new Texture2D(w, h, TextureFormat.RGBA32, mips) { filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = mips ? 8 : 1 };
        }

        public void Apply() { Tex.SetPixels32(px); Tex.Apply(mips); }

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            int x1 = Math.Min(W, x + w), y1 = Math.Min(H, y + h);
            for (int b = Math.Max(0, y); b < y1; b++)
                for (int a = Math.Max(0, x), row = (H - 1 - b) * W; a < x1; a++) px[row + a] = c;
        }

        public void Fill(Color32 c) => Rect(0, 0, W, H, c);

        public void Frame(int x, int y, int w, int h, Color32 c, int t = 2)
        {
            Rect(x, y, w, t, c); Rect(x, y + h - t, w, t, c); Rect(x, y, t, h, c); Rect(x + w - t, y, t, h, c);
        }

        // A filled circle built from one-pixel rows, so it stays on the grid.
        public void Disc(int cx, int cy, int r, Color32 c)
        {
            for (int dy = -r; dy < r; dy++)
            {
                int half = (int)Math.Sqrt(r * r - (dy + .5) * (dy + .5));
                Rect(cx - half, cy + dy, half * 2, 1, c);
            }
        }

        // A rectangle with 2 px diagonal corner cuts: row 0 inset 2, row 1 inset 1, mirrored at the bottom.
        public void Chip(int x, int y, int w, int h, Color32 c)
        {
            Rect(x + 2, y, w - 4, 1, c); Rect(x + 1, y + 1, w - 2, 1, c); Rect(x, y + 2, w, h - 4, c);
            Rect(x + 1, y + h - 2, w - 2, 1, c); Rect(x + 2, y + h - 1, w - 4, 1, c);
        }

        public void ChipFrame(int x, int y, int w, int h, Color32 rim, Color32 fill, int t = 2)
        {
            Chip(x, y, w, h, rim); Chip(x + t, y + t, w - 2 * t, h - 2 * t, fill);
        }

        // Squashes the picture into h rows about the middle, black around it. Nearest row, so it stays on the grid.
        Color32[] squashScratch;
        public void Squash(int h)
        {
            if (squashScratch == null || squashScratch.Length != px.Length) squashScratch = new Color32[px.Length];
            System.Array.Copy(px, squashScratch, px.Length);
            int top = H / 2 - h / 2;
            for (int b = 0; b < H; b++)
            {
                int row = (H - 1 - b) * W;
                if (b < top || b >= top + h) { for (int a = 0; a < W; a++) px[row + a] = new Color32(0, 0, 0, 255); continue; }
                System.Array.Copy(squashScratch, (H - 1 - (b - top) * H / h) * W, px, row, W);
            }
        }

        public void Text(string s, int x, int y, Color32 c, int sx = 1, int sy = 0)
        {
            if (sy == 0) sy = sx;
            for (int i = 0; i < s.Length; i++)
            {
                ulong g = Glyph(s[i]);
                for (int r = 0; r < 8; r++)
                    for (int k = 0; k < 8; k++)
                        if ((g >> (8 * r + k) & 1) != 0) Rect(x + (i * 8 + k) * sx, y + r * sy, sx, sy, c);
            }
        }

        public void Centred(string s, int y, Color32 c, int sx = 1, int sy = 0, int cx = -1)
        {
            if (cx < 0) cx = W / 2;
            Text(s, cx - s.Length * 4 * sx, y, c, sx, sy);
        }

        public void Right(string s, int xr, int y, Color32 c, int sx = 1, int sy = 0) => Text(s, xr - s.Length * 8 * sx, y, c, sx, sy);

        // Press Start 2P, Copyright 2012 The Press Start 2P Project Authors, SIL Open Font License 1.1.
        // Rasterised at 8px, ASCII 32-126 then x and divide. Each byte is a row, low bit on the left.
        static ulong Glyph(char ch)
        {
            int i = ch == '×' ? 95 : ch == '÷' ? 96 : ch == '−' ? 13 : ch - 32;
            return i >= 0 && i < Glyphs.Length ? Glyphs[i] : 0;
        }

        static readonly ulong[] Glyphs = Parse("0000000000000000 1c1c1c0c0c000c00 3636360000000000 367f3636367f3600 083e0b3e683f0800 4625130864523100 0e1b1b0e5b337e00 0c0c0c0000000000 30180c0c0c183000 060c1818180c0600 00361c7f1c360000 0018187e18180000 00000000000c0c06 0000007e00000000 00000000000c0c00 4020100804020100 1c32636363261c00 181c181818187e00 3e63703c1e077f00 7e30183c60633e00 383c36337f303000 3f033f6060633e00 3c06033f63633e00 7f6330180c0c0c00 1e23271e79613e00 3e63637e60301e00 000c0c000c0c0000 000c0c000c0c0600 30180c060c183000 00007f007f000000 060c1830180c0600 3e7f63301c001c00 3e415d557d013e00 1c3663637f636300 3f63633f63633f00 3c66030303663c00 1f33636363331f00 7f03033f03037f00 7f03033f03030300 7c06037363667c00 6363637f63636300 7e18181818187e00 6060606060633e00 63331b0f1f3b7300 0606060606067e00 63777f6b6b636300 63676f7b73636300 3e63636363633e00 3f6363633f030300 3e6363637b335e00 3f6363731f3b7300 3e63033e60633e00 7e18181818181800 6363636363633e00 636363773e1c0800 6b6b6b6b7f772200 6363361c36636300 6666663c18181800 7f70381c0e077f00 3c0c0c0c0c0c3c00 0102040810204000 1e18181818181e00 1c36000000000000 000000000000007f 0810000000000000 00003e607e637e00 03033f6363633e00 00007e0303037e00 60607e6363637e00 00003e637f033e00 70187e1818181800 00007e63637e603e 03033f6363636300 18001c1818187e00 300038303030301e 030363331f336300 1c18181818187e00 00003f6d6d6d6d00 00003f6363636300 00003e6363633e00 00003f63633f0303 00007e63637e6060 0000760e06060600 00003e033e603f00 18187e1818181800 0000636363637e00 00006666663c1800 00006b6b6b6b3600 000063361c366300 00006363637e603e 00007f381c0e7f00 3018180c18183000 1818181818181800 060c0c180c0c0600 00000e5d38000000 0063361c36630000 1818007e00181800");

        static ulong[] Parse(string hex)
        {
            var parts = hex.Split(' ');
            var glyphs = new ulong[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                for (int r = 0; r < 8; r++)
                    glyphs[i] |= (ulong)Convert.ToByte(parts[i].Substring(r * 2, 2), 16) << (8 * r);
            return glyphs;
        }
    }
}
