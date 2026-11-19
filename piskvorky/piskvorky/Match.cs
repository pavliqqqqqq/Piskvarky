using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Piskvorky
{
    public enum GamePhase { Place, Choose, Play, Over }
    public enum OpeningRule { None, Swap1, Swap2 }

    /// <summary>
    /// Stav jedne partie. Barva 1 = cerny (X, zacina), barva 2 = bily (O).
    /// "Seat" 0/1 = hrac, ktery zacina / hrac, ktery zacina druhy.
    /// </summary>
    public class Match
    {
        public const int N = 25;
        public const int WinLength = 5;

        public readonly int[,] B = new int[N, N];
        public GamePhase Phase;
        public readonly OpeningRule Rule;
        public int Turn = 1;                         // barva na tahu ve fazi Play
        public readonly int[] SeatColor = new int[2]; // 0 = jeste nerozhodnuto
        public int Actor;                            // seat, ktery jedna ve fazi Place/Choose
        public int Stage;                            // 1 = 3 kameny, 2 = pridani 2 kamenu (swap 2), 3 = volba barvy po pridani
        public int SeqIdx;
        public int OptionCount;
        public int Moves;
        public readonly int[] SeatMoves = new int[2];
        public int Winner;                           // 0 nic, 1/2 barva, 3 remiza
        public List<Point> WinCells = new List<Point>();
        public Point Last = new Point(-1, -1);

        int[] seq = new int[0];
        public int SeqLen => seq.Length;

        public Match(OpeningRule rule)
        {
            Rule = rule;
            if (rule == OpeningRule.None)
            {
                SeatColor[0] = 1;
                SeatColor[1] = 2;
                Phase = GamePhase.Play;
                Turn = 1;
            }
            else
            {
                Phase = GamePhase.Place;
                Actor = 0;
                Stage = 1;
                seq = new[] { 1, 2, 1 };
            }
        }

        public int NextColor => Phase == GamePhase.Place ? seq[SeqIdx] : Turn;

        public int ActingSeat
        {
            get
            {
                if (Phase == GamePhase.Play) return SeatColor[0] == Turn ? 0 : 1;
                return Actor;
            }
        }

        public bool Place(int x, int y)
        {
            if (Phase != GamePhase.Place && Phase != GamePhase.Play) return false;
            if (x < 0 || y < 0 || x >= N || y >= N || B[x, y] != 0) return false;

            int seat = ActingSeat;
            int c = NextColor;
            B[x, y] = c;
            Moves++;
            SeatMoves[seat]++;
            Last = new Point(x, y);

            if (Phase == GamePhase.Place)
            {
                SeqIdx++;
                if (SeqIdx >= seq.Length) AfterPlace();
                return true;
            }

            var line = WinLine(B, x, y, c);
            if (line != null)
            {
                Winner = c;
                WinCells = line;
                Phase = GamePhase.Over;
                return true;
            }
            if (Moves >= N * N)
            {
                Winner = 3;
                Phase = GamePhase.Over;
                return true;
            }
            Turn = 3 - Turn;
            return true;
        }

        void AfterPlace()
        {
            Phase = GamePhase.Choose;
            if (Stage == 1)
            {
                Actor = 1;
                OptionCount = Rule == OpeningRule.Swap2 ? 3 : 2;
            }
            else
            {
                Actor = 0;
                OptionCount = 2;
                Stage = 3;
            }
        }

        /// <summary>opt 0 = vezmu cerne, opt 1 = vezmu bile, opt 2 = pridam 2 kameny (jen swap 2).</summary>
        public void Choose(int opt)
        {
            if (Phase != GamePhase.Choose) return;
            if (opt == 2 && Stage == 1 && Rule == OpeningRule.Swap2)
            {
                Phase = GamePhase.Place;
                Actor = 1;
                Stage = 2;
                seq = new[] { 2, 1 };
                SeqIdx = 0;
                return;
            }
            int c = Math.Clamp(opt, 0, 1) + 1;
            SeatColor[Actor] = c;
            SeatColor[1 - Actor] = 3 - c;
            Phase = GamePhase.Play;
            Turn = 2; // po 3 i po 5 kamenech je na tahu bily
        }

        /// <summary>Vrati bunky vitezne rady (5 a vice) prochazejici bodem x,y, jinak null.</summary>
        public static List<Point> WinLine(int[,] b, int x, int y, int c)
        {
            int[] dx = { 1, 0, 1, 1 };
            int[] dy = { 0, 1, 1, -1 };
            for (int d = 0; d < 4; d++)
            {
                var cells = new List<Point> { new Point(x, y) };
                for (int s = -1; s <= 1; s += 2)
                {
                    int cx = x + s * dx[d], cy = y + s * dy[d];
                    while (cx >= 0 && cy >= 0 && cx < N && cy < N && b[cx, cy] == c)
                    {
                        cells.Add(new Point(cx, cy));
                        cx += s * dx[d];
                        cy += s * dy[d];
                    }
                }
                if (cells.Count >= WinLength) return cells;
            }
            return null;
        }
    }
}
