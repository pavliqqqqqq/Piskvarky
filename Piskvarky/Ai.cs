using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Piskvorky
{
    public enum Level { Easy, Medium, Hard, Insane }

    /// <summary>
    /// Pocitacovy souper. Easy/Medium/Hard = heuristika s ruznou sikovnosti a nahodnosti,
    /// Insane = alfa-beta prohledavani do hloubky 6 pultahu nad nejlepsimi kandidaty.
    /// Vsechny metody pracuji s kopii desky, takze bezi bezpecne ve vlakne na pozadi.
    /// </summary>
    public class Ai
    {
        const int N = Match.N;
        const int InsaneDepth = 6;
        static readonly int[] DX = { 1, 0, 1, 1 };
        static readonly int[] DY = { 0, 1, 1, -1 };

        readonly Level level;
        readonly Random rnd = new Random();

        public Ai(Level l) { level = l; }

        static bool In(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;

        // ---------- zakladni pomucky ----------

        /// <summary>Vytvori kamenem barvy c na (x,y) rada o delce &gt;= 5? (bunka x,y se necte).</summary>
        static bool WinAt(int[,] b, int x, int y, int c)
        {
            for (int d = 0; d < 4; d++)
            {
                int n = 1;
                for (int s = -1; s <= 1; s += 2)
                {
                    int cx = x + s * DX[d], cy = y + s * DY[d];
                    while (In(cx, cy) && b[cx, cy] == c)
                    {
                        n++;
                        cx += s * DX[d];
                        cy += s * DY[d];
                    }
                }
                if (n >= Match.WinLength) return true;
            }
            return false;
        }

        static int Pat(int n, int open)
        {
            if (n >= 5) return 1_000_000;
            if (open == 0) return 0;
            switch (n)
            {
                case 4: return open == 2 ? 100_000 : 10_000;
                case 3: return open == 2 ? 4_000 : 400;
                case 2: return open == 2 ? 300 : 30;
                default: return open == 2 ? 10 : 1;
            }
        }

        /// <summary>Cena polozeni kamene barvy c na prazdne pole (x,y) (bez zmeny desky).</summary>
        static int CellScore(int[,] b, int x, int y, int c)
        {
            if (WinAt(b, x, y, c)) return 1_000_000;
            int total = 0, fours = 0, threes = 0;
            for (int d = 0; d < 4; d++)
            {
                int count = 1, open = 0;
                bool gap = false;
                for (int s = -1; s <= 1; s += 2)
                {
                    int cx = x + s * DX[d], cy = y + s * DY[d];
                    while (In(cx, cy))
                    {
                        int v = b[cx, cy];
                        if (v == c)
                        {
                            count++;
                            cx += s * DX[d];
                            cy += s * DY[d];
                            continue;
                        }
                        if (v == 0)
                        {
                            int nx = cx + s * DX[d], ny = cy + s * DY[d];
                            if (!gap && In(nx, ny) && b[nx, ny] == c)
                            {
                                gap = true; // povolime jednu mezeru (X.XX)
                                cx = nx;
                                cy = ny;
                                continue;
                            }
                            open++;
                        }
                        break;
                    }
                }
                int n = count, o = open;
                if (gap && n >= 4) { n = 4; o = 1; }
                total += Pat(n, o);
                if (n == 4 && o >= 1) fours++;
                else if (n == 3 && o == 2) threes++;
            }
            if (fours >= 2 || (fours >= 1 && threes >= 1)) total += 30_000;
            else if (threes >= 2) total += 6_000;
            return total;
        }

        /// <summary>Ohodnoceni cele desky z pohledu hrace me (obrana je lehce nadvazena).</summary>
        static double Eval(int[,] b, int me)
        {
            double s = 0;
            for (int d = 0; d < 4; d++)
                for (int x = 0; x < N; x++)
                    for (int y = 0; y < N; y++)
                    {
                        int c = b[x, y];
                        if (c == 0) continue;
                        int px = x - DX[d], py = y - DY[d];
                        if (In(px, py) && b[px, py] == c) continue; // neni zacatek rady
                        int n = 0, cx = x, cy = y;
                        while (In(cx, cy) && b[cx, cy] == c)
                        {
                            n++;
                            cx += DX[d];
                            cy += DY[d];
                        }
                        int open = 0;
                        if (In(px, py) && b[px, py] == 0) open++;
                        if (In(cx, cy) && b[cx, cy] == 0) open++;
                        double v = Pat(n, open);
                        s += c == me ? v : -v * 1.1;
                    }
            return s;
        }

        static List<Point> Cands(int[,] b, int r)
        {
            int minx = N, miny = N, maxx = -1, maxy = -1;
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                    if (b[x, y] != 0)
                    {
                        if (x < minx) minx = x;
                        if (x > maxx) maxx = x;
                        if (y < miny) miny = y;
                        if (y > maxy) maxy = y;
                    }
            var res = new List<Point>();
            if (maxx < 0)
            {
                res.Add(new Point(N / 2, N / 2));
                return res;
            }
            for (int x = Math.Max(0, minx - r); x <= Math.Min(N - 1, maxx + r); x++)
                for (int y = Math.Max(0, miny - r); y <= Math.Min(N - 1, maxy + r); y++)
                {
                    if (b[x, y] != 0) continue;
                    bool near = false;
                    for (int dx = -r; dx <= r && !near; dx++)
                        for (int dy = -r; dy <= r; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (In(nx, ny) && b[nx, ny] != 0) { near = true; break; }
                        }
                    if (near) res.Add(new Point(x, y));
                }
            return res;
        }

        /// <summary>Nejlepsi kandidati pro hrace c; pri vyhre/nutne obrane vraci jen jeden tah.</summary>
        static List<Point> Ranked(int[,] b, int c, int k, int r)
        {
            int o = 3 - c;
            var cs = Cands(b, r);
            var scored = new List<(int s, Point p)>();
            Point? block = null;
            foreach (var p in cs)
            {
                int a = CellScore(b, p.X, p.Y, c);
                if (a >= 1_000_000) return new List<Point> { p };
                int d = CellScore(b, p.X, p.Y, o);
                if (d >= 1_000_000 && block == null) block = p;
                scored.Add((a + d, p));
            }
            if (block != null) return new List<Point> { block.Value };
            scored.Sort((u, v) => v.s.CompareTo(u.s));
            var res = new List<Point>();
            for (int i = 0; i < scored.Count && i < k; i++) res.Add(scored[i].p);
            return res;
        }

        // ---------- hledani tahu ----------

        public Point Move(int[,] b, int me)
        {
            int opp = 3 - me;
            var cs = Cands(b, 2);
            if (cs.Count == 1 && b[cs[0].X, cs[0].Y] == 0 && CountStones(b) == 0) return cs[0];

            double winP, blockP;
            switch (level)
            {
                case Level.Easy: winP = 0.35; blockP = 0.15; break;
                case Level.Medium: winP = 0.9; blockP = 0.7; break;
                default: winP = 1.0; blockP = 1.0; break;
            }

            Point? myWin = null, blk = null;
            var sc = new List<(Point p, int a, int d)>();
            foreach (var p in cs)
            {
                int a = CellScore(b, p.X, p.Y, me);
                int d = CellScore(b, p.X, p.Y, opp);
                if (a >= 1_000_000 && myWin == null) myWin = p;
                if (d >= 1_000_000 && blk == null) blk = p;
                sc.Add((p, a, d));
            }
            if (myWin != null && rnd.NextDouble() < winP) return myWin.Value;
            if (blk != null && rnd.NextDouble() < blockP) return blk.Value;

            if (level == Level.Insane) return InsaneMove(b, me);
            if (level == Level.Easy && rnd.NextDouble() < 0.5)
            {
                var near = Cands(b, 1);
                return near[rnd.Next(near.Count)];
            }

            double best = double.NegativeInfinity;
            Point bp = cs[0];
            foreach (var (p, a, d) in sc)
            {
                double s;
                switch (level)
                {
                    case Level.Easy: s = (a + 0.05 * d + 1) * (0.05 + rnd.NextDouble() * 1.9); break;
                    case Level.Medium: s = (a + 0.5 * d) * (0.6 + rnd.NextDouble() * 0.8); break;
                    default: s = (a + 0.8 * d) * (0.8 + rnd.NextDouble() * 0.4); break;
                }
                if (s > best) { best = s; bp = p; }
            }
            return bp;
        }

        static int CountStones(int[,] b)
        {
            int n = 0;
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                    if (b[x, y] != 0) n++;
            return n;
        }

        readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        bool timeUp;
        const int InsaneTimeMs = 4000;

        /// <summary>Iterativni prohlubovani (2, 4, 6 pultahu) s casovym limitem; pouzije posledni dokoncenou iteraci.</summary>
        Point InsaneMove(int[,] b, int me)
        {
            var roots = Ranked(b, me, 14, 2);
            if (roots.Count == 1) return roots[0];
            sw.Restart();
            timeUp = false;
            Point bp = roots[0];
            foreach (int depth in new[] { 2, 4, InsaneDepth })
            {
                double best = double.NegativeInfinity;
                Point cur = roots[0];
                foreach (var p in roots)
                {
                    b[p.X, p.Y] = me;
                    double v = Search(b, depth - 1, best, double.PositiveInfinity, 3 - me, me);
                    b[p.X, p.Y] = 0;
                    if (timeUp) break;
                    v += rnd.NextDouble();
                    if (v > best) { best = v; cur = p; }
                }
                if (timeUp) break;
                bp = cur;
                roots.Remove(cur);
                roots.Insert(0, cur);
            }
            return bp;
        }

        double Search(int[,] b, int depth, double alpha, double beta, int toMove, int me)
        {
            if (sw.ElapsedMilliseconds > InsaneTimeMs) timeUp = true;
            if (depth == 0 || timeUp) return Eval(b, me);
            int k = depth >= 5 ? 10 : depth == 4 ? 8 : depth == 3 ? 6 : 5;
            int r = depth >= 4 ? 2 : 1;
            var cs = Ranked(b, toMove, k, r);
            if (cs.Count == 0) return 0;

            bool max = toMove == me;
            double best = max ? double.NegativeInfinity : double.PositiveInfinity;
            foreach (var p in cs)
            {
                b[p.X, p.Y] = toMove;
                double v;
                if (WinAt(b, p.X, p.Y, toMove)) v = (max ? 1 : -1) * (10_000_000.0 + depth);
                else v = Search(b, depth - 1, alpha, beta, 3 - toMove, me);
                b[p.X, p.Y] = 0;

                if (max)
                {
                    if (v > best) best = v;
                    if (best > alpha) alpha = best;
                }
                else
                {
                    if (v < best) best = v;
                    if (best < beta) beta = best;
                }
                if (beta <= alpha) break;
            }
            return best;
        }

        // ---------- swap 1 / swap 2 ----------

        /// <summary>Rozhodnuti o barve po zahajeni: vrati 1 (cerny) nebo 2 (bily).</summary>
        public int ChooseColor(int[,] b, int toMove)
        {
            if (level == Level.Easy && rnd.NextDouble() < 0.5) return rnd.Next(1, 3);
            double v1 = Eval(b, 1) + (toMove == 1 ? 300 : 0);
            double v2 = Eval(b, 2) + (toMove == 2 ? 300 : 0);
            return v1 >= v2 ? 1 : 2;
        }

        /// <summary>Kameny zahajeni (3 kameny pri swapu): stred, soused, kamen ve vzdalenosti 2.</summary>
        public Point OpeningStone(int[,] b, int idx)
        {
            int c = N / 2;
            if (idx == 0 && b[c, c] == 0) return new Point(c, c);
            for (int tries = 0; tries < 80; tries++)
            {
                int dx = rnd.Next(-2, 3), dy = rnd.Next(-2, 3);
                if (dx == 0 && dy == 0) continue;
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (idx == 1 && dist > 1) continue;
                if (idx >= 2 && dist < 2) continue;
                if (b[c + dx, c + dy] == 0) return new Point(c + dx, c + dy);
            }
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                    if (b[x, y] == 0) return new Point(x, y);
            return new Point(c, c);
        }
    }
}
