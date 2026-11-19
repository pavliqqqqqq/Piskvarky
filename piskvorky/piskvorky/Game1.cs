using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Piskvorky
{
    public class Game1 : Game
    {
        const int W = 1180, H = 790, BX = 20, BY = 20, CELL = 30, N = Match.N;

        static readonly Color Gold = new Color(240, 200, 90);
        static readonly Color Dim = new Color(150, 156, 172);
        static readonly Color Panel = new Color(44, 49, 63);
        static readonly string[] LevelNames = { "EASY", "MEDIUM", "HARD", "INSANE" };
        static readonly string[] OpeningNames = { "BEZ SWAPU", "SWAP 1", "SWAP 2" };
        static readonly int[] LevelPoints = { 1, 3, 6, 10 };
        static readonly Rectangle NameRect = new Rectangle(380, 445, 320, 44);

        enum Screen { Menu, Play, Stats, Leaders }

        GraphicsDeviceManager gfx;
        SpriteBatch sb;
        Screen screen = Screen.Menu;
        SaveData save;

        // nastaveni v menu
        bool vsPc = true;
        int level = 1;
        int openingSel = 0;
        bool pcStarts = false;
        bool editName = false;

        // aktualni partie
        Match m;
        Ai ai;
        bool gVsPc;
        int gLevel;
        readonly bool[] seatAi = new bool[2];
        readonly string[] seatName = new string[2];
        int gameId;
        Task<(int x, int y, int opt)> aiTask;
        int aiTaskGame;
        float aiDelay;
        bool resultSaved;

        // vstupy
        MouseState ms, pms;
        KeyboardState pkb;
        bool clickPending;
        Action pending;

        bool confirmReset;
        int lbTab;

        public Game1()
        {
            gfx = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = W,
                PreferredBackBufferHeight = H
            };
            IsMouseVisible = true;
            Window.Title = "Piskvorky";
        }

        protected override void Initialize()
        {
            save = SaveData.Load();
            Window.TextInput += OnText;
            base.Initialize();
        }

        protected override void LoadContent()
        {
            sb = new SpriteBatch(GraphicsDevice);
            Gfx.Init(GraphicsDevice);
        }

        void OnText(object sender, TextInputEventArgs e)
        {
            if (screen != Screen.Menu || !editName) return;
            if (e.Key == Keys.Back)
            {
                if (save.PlayerName.Length > 0) save.PlayerName = save.PlayerName.Substring(0, save.PlayerName.Length - 1);
            }
            else if (e.Key == Keys.Enter) editName = false;
            else if (char.IsLetterOrDigit(e.Character) || e.Character == ' ')
            {
                if (save.PlayerName.Length < 12) save.PlayerName += e.Character;
            }
        }

        // ======================= UPDATE =======================

        protected override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            pms = ms;
            ms = Mouse.GetState();
            var kb = Keyboard.GetState();
            if (ms.LeftButton == ButtonState.Pressed && pms.LeftButton == ButtonState.Released) clickPending = true;

            if (pending != null)
            {
                var a = pending;
                pending = null;
                a();
            }

            if (kb.IsKeyDown(Keys.Escape) && !pkb.IsKeyDown(Keys.Escape))
            {
                if (editName) editName = false;
                else if (screen != Screen.Menu)
                {
                    screen = Screen.Menu;
                    confirmReset = false;
                }
            }
            pkb = kb;

            if (screen == Screen.Menu && clickPending && !NameRect.Contains(ms.X, ms.Y)) editName = false;
            if (screen == Screen.Play) UpdatePlay(dt);

            base.Update(gameTime);
        }

        void UpdatePlay(float dt)
        {
            if (m.Phase == GamePhase.Over)
            {
                if (!resultSaved) SaveResult();
                return;
            }

            int seat = m.ActingSeat;
            if (seatAi[seat])
            {
                RunAi(dt);
                return;
            }

            if (clickPending && (m.Phase == GamePhase.Place || m.Phase == GamePhase.Play))
            {
                if (ms.X >= BX && ms.Y >= BY)
                {
                    int cx = (ms.X - BX) / CELL, cy = (ms.Y - BY) / CELL;
                    if (cx < N && cy < N) m.Place(cx, cy);
                }
            }
        }

        void RunAi(float dt)
        {
            if (aiTask == null)
            {
                aiDelay -= dt;
                if (aiDelay > 0) return;

                var b = (int[,])m.B.Clone();
                var phase = m.Phase;
                int color = m.NextColor, stage = m.Stage, idx = m.SeqIdx;
                var a = ai;
                aiTaskGame = gameId;
                aiTask = Task.Run<(int x, int y, int opt)>(() =>
                {
                    try
                    {
                        if (phase == GamePhase.Choose)
                        {
                            int c = a.ChooseColor(b, 2);
                            return (-1, -1, c - 1);
                        }
                        Point p = (phase == GamePhase.Place && stage == 1) ? a.OpeningStone(b, idx) : a.Move(b, color);
                        return (p.X, p.Y, -1);
                    }
                    catch
                    {
                        return (-1, -1, 0);
                    }
                });
            }
            else if (aiTask.IsCompleted)
            {
                var t = aiTask;
                aiTask = null;
                aiDelay = 0.3f;
                if (aiTaskGame != gameId) return;

                (int x, int y, int opt) r = t.IsFaulted ? (-1, -1, 0) : t.Result;
                if (m.Phase == GamePhase.Choose) m.Choose(r.opt);
                else if (!(r.x >= 0 && m.Place(r.x, r.y))) PlaceFallback();
            }
        }

        void PlaceFallback()
        {
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                    if (m.B[x, y] == 0 && m.Place(x, y)) return;
        }

        void StartGame()
        {
            if (string.IsNullOrWhiteSpace(save.PlayerName)) save.PlayerName = "HRAC";
            save.PlayerName = save.PlayerName.Trim();
            save.Save();

            gameId++;
            m = new Match((OpeningRule)openingSel);
            gVsPc = vsPc;
            gLevel = level;
            seatAi[0] = seatAi[1] = false;
            aiTask = null;
            aiDelay = 0.5f;
            resultSaved = false;

            if (vsPc)
            {
                ai = new Ai((Level)level);
                int humanSeat = pcStarts ? 1 : 0;
                seatAi[1 - humanSeat] = true;
                seatName[humanSeat] = save.PlayerName;
                seatName[1 - humanSeat] = "PC " + LevelNames[level];
            }
            else
            {
                ai = null;
                seatName[0] = "HRAC 1";
                seatName[1] = "HRAC 2";
            }
            screen = Screen.Play;
        }

        void SaveResult()
        {
            resultSaved = true;
            if (gVsPc)
            {
                int humanSeat = seatAi[0] ? 1 : 0;
                string res;
                var rec = save.Get(LevelNames[gLevel]);
                if (m.Winner == 3) { res = "D"; rec.Draws++; }
                else if (m.SeatColor[humanSeat] == m.Winner) { res = "W"; rec.Wins++; }
                else { res = "L"; rec.Losses++; }

                save.Board.Add(new LbEntry
                {
                    Name = save.PlayerName,
                    Level = LevelNames[gLevel],
                    Result = res,
                    Moves = m.SeatMoves[humanSeat],
                    Opening = OpeningNames[(int)m.Rule],
                    Date = DateTime.Now.ToString("dd.MM.yyyy")
                });
            }
            else
            {
                var rec = save.Get("Friend");
                if (m.Winner == 3) rec.Draws++;
                else if (m.SeatColor[0] == m.Winner) rec.Wins++;
                else rec.Losses++;
            }
            save.Save();
        }

        // ======================= DRAW =======================

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(30, 34, 44));
            sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
            switch (screen)
            {
                case Screen.Menu: DrawMenu(); break;
                case Screen.Play: DrawBoard(); DrawSidebar(); break;
                case Screen.Stats: DrawStats(); break;
                case Screen.Leaders: DrawLeaders(); break;
            }
            sb.End();
            clickPending = false;
            base.Draw(gameTime);
        }

        void Btn(string label, Rectangle r, bool sel, Action act, int sc = 2, bool enabled = true)
        {
            bool hover = enabled && r.Contains(ms.X, ms.Y);
            Color bg = !enabled ? new Color(40, 44, 54)
                     : sel ? new Color(60, 120, 200)
                     : hover ? new Color(84, 92, 112)
                     : new Color(58, 64, 80);
            Gfx.Fill(sb, r, bg);
            Gfx.Frame(sb, r, sel ? Color.White : new Color(110, 118, 140), 2);
            Gfx.TextC(sb, label, r.X + r.Width / 2, r.Y + (r.Height - 7 * sc) / 2, sc, enabled ? Color.White : new Color(100, 104, 116));
            if (enabled && hover && clickPending && pending == null) pending = act;
        }

        void Label(string s, int x, int y) => Gfx.Text(sb, s, x, y, 3, Color.White);

        // ---------- menu ----------

        void DrawMenu()
        {
            Gfx.TextC(sb, "PISKVORKY", W / 2, 30, 10, Gold);
            Gfx.TextC(sb, "5 V RADE VYHRAVA - PLOCHA 25X25", W / 2, 125, 2, Dim);

            int lx = 100, bx = 380;

            Label("REZIM", lx, 177);
            Btn("PROTI PC", new Rectangle(bx, 165, 250, 44), vsPc, () => vsPc = true);
            Btn("PROTI KAMARADOVI", new Rectangle(bx + 260, 165, 300, 44), !vsPc, () => vsPc = false);

            Label("OBTIZNOST", lx, 247);
            for (int i = 0; i < 4; i++)
            {
                int li = i;
                Btn(LevelNames[i], new Rectangle(bx + i * 150, 235, 140, 44), vsPc && level == i, () => level = li, 2, vsPc);
            }

            Label("OTEVIRANI", lx, 317);
            for (int i = 0; i < 3; i++)
            {
                int oi = i;
                Btn(OpeningNames[i], new Rectangle(bx + i * 170, 305, 160, 44), openingSel == i, () => openingSel = oi);
            }

            Label("ZACINA", lx, 387);
            Btn("HRAC", new Rectangle(bx, 375, 160, 44), vsPc && !pcStarts, () => pcStarts = false, 2, vsPc);
            Btn("POCITAC", new Rectangle(bx + 170, 375, 160, 44), vsPc && pcStarts, () => pcStarts = true, 2, vsPc);

            Label("JMENO", lx, 457);
            bool cursor = editName && (Environment.TickCount / 500) % 2 == 0;
            Btn(save.PlayerName + (cursor ? "_" : ""), NameRect, editName, () => editName = true);

            string desc;
            switch (openingSel)
            {
                case 1:
                    desc = "SWAP 1: ZACINAJICI HRAC POLOZI 3 KAMENY (CERNY, BILY, CERNY). DRUHY HRAC SI VYBERE, ZDA BUDE CERNY NEBO BILY. DALSI TAH MA BILY.";
                    break;
                case 2:
                    desc = "SWAP 2: ZACINAJICI HRAC POLOZI 3 KAMENY. DRUHY HRAC SI VYBERE BARVU, NEBO PRIDA DALSI 2 KAMENY (BILY, CERNY) A BARVU SI PAK VYBERE ZACINAJICI HRAC. DALSI TAH MA BILY.";
                    break;
                default:
                    desc = "BEZ SWAPU: ZACINA CERNY (X), HRAJE SE STRIDAVE. KDO MA JAKO PRVNI 5 A VICE KAMENU V RADE (VODOROVNE, SVISLE I PO DIAGONALE), VYHRAVA.";
                    break;
            }
            Gfx.Wrapped(sb, desc, 100, 515, 82, 2, Dim, 20);

            Btn("START HRY", new Rectangle(W / 2 - 200, 590, 400, 70), false, StartGame, 4);

            Btn("STATISTIKY", new Rectangle(185, 690, 250, 50), false, () => { screen = Screen.Stats; confirmReset = false; }, 2);
            Btn("ZEBRICEK", new Rectangle(465, 690, 250, 50), false, () => screen = Screen.Leaders, 2);
            Btn("KONEC", new Rectangle(745, 690, 250, 50), false, Exit, 2);
        }

        // ---------- deska ----------

        void DrawBoard()
        {
            int size = N * CELL;
            Gfx.Fill(sb, new Rectangle(BX - 10, BY - 10, size + 20, size + 20), new Color(214, 170, 100));
            Color line = new Color(95, 62, 30);
            for (int i = 0; i < N; i++)
            {
                int p = i * CELL + CELL / 2;
                Gfx.Fill(sb, new Rectangle(BX + CELL / 2, BY + p, (N - 1) * CELL, 1), line);
                Gfx.Fill(sb, new Rectangle(BX + p, BY + CELL / 2, 1, (N - 1) * CELL), line);
            }
            foreach (int sx in new[] { 6, 12, 18 })
                foreach (int sy in new[] { 6, 12, 18 })
                    Gfx.Fill(sb, new Rectangle(BX + sx * CELL + CELL / 2 - 3, BY + sy * CELL + CELL / 2 - 3, 7, 7), line);

            int pad = 2;
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    int c = m.B[x, y];
                    if (c != 0)
                        Gfx.Stone(sb, c, new Rectangle(BX + x * CELL + pad, BY + y * CELL + pad, CELL - 2 * pad, CELL - 2 * pad));
                }

            // nahled kamene pod mysi
            bool humanTurn = (m.Phase == GamePhase.Place || m.Phase == GamePhase.Play) && !seatAi[m.ActingSeat];
            if (humanTurn && ms.X >= BX && ms.Y >= BY)
            {
                int hx = (ms.X - BX) / CELL, hy = (ms.Y - BY) / CELL;
                if (hx < N && hy < N && m.B[hx, hy] == 0)
                    Gfx.Stone(sb, m.NextColor, new Rectangle(BX + hx * CELL + pad, BY + hy * CELL + pad, CELL - 2 * pad, CELL - 2 * pad), 0.5f);
            }

            if (m.Last.X >= 0)
                Gfx.Fill(sb, new Rectangle(BX + m.Last.X * CELL + CELL / 2 - 3, BY + m.Last.Y * CELL + CELL / 2 - 3, 7, 7), new Color(230, 50, 50));

            foreach (var p in m.WinCells)
                Gfx.Frame(sb, new Rectangle(BX + p.X * CELL, BY + p.Y * CELL, CELL, CELL), new Color(230, 40, 40), 3);
        }

        static string ColorName(int c) => c == 1 ? "CERNY (X)" : "BILY (O)";

        string Prompt()
        {
            switch (m.Phase)
            {
                case GamePhase.Place:
                    return $"{seatName[m.Actor]} KLADE KAMEN {m.SeqIdx + 1}/{m.SeqLen}: {ColorName(m.NextColor)}";
                case GamePhase.Choose:
                    return $"{seatName[m.Actor]} SE ROZHODUJE: VYBER BARVU" + (m.OptionCount == 3 ? " NEBO PRIDEJ 2 KAMENY." : ".");
                case GamePhase.Play:
                    return $"NA TAHU: {seatName[m.ActingSeat]} - {ColorName(m.Turn)}";
                default:
                    if (m.Winner == 3) return "REMIZA - PLOCHA JE PLNA.";
                    int ws = m.SeatColor[0] == m.Winner ? 0 : 1;
                    string s = $"VYHRAL: {seatName[ws]} - {ColorName(m.Winner)}";
                    if (gVsPc) s += seatAi[ws] ? "\nPC TENTOKRAT VYHRAL." : "\nGRATULUJI, VYHRAL JSI!";
                    return s;
            }
        }

        void DrawSidebar()
        {
            int x = 805, w = 355;
            Gfx.Text(sb, "PISKVORKY", x, 22, 4, Gold);
            string mode = gVsPc ? "PROTI PC - " + LevelNames[gLevel] : "PROTI KAMARADOVI";
            Gfx.Text(sb, mode, x, 68, 2, Color.White);
            Gfx.Text(sb, "OTEVIRANI: " + OpeningNames[(int)m.Rule], x, 90, 2, Dim);

            for (int s = 0; s < 2; s++)
            {
                var card = new Rectangle(x, 122 + s * 62, w, 54);
                Gfx.Fill(sb, card, Panel);
                bool active = m.Phase != GamePhase.Over && m.ActingSeat == s;
                Gfx.Frame(sb, card, active ? Gold : new Color(80, 86, 104), active ? 3 : 1);
                if (m.SeatColor[s] != 0)
                    Gfx.Stone(sb, m.SeatColor[s], new Rectangle(card.X + 8, card.Y + 9, 36, 36));
                else
                    Gfx.TextC(sb, "?", card.X + 26, card.Y + 14, 3, Dim);
                Gfx.Text(sb, seatName[s], card.X + 56, card.Y + 9, 2, Color.White);
                Gfx.Text(sb, "KAMENU: " + m.SeatMoves[s], card.X + 56, card.Y + 31, 2, Dim);
            }

            Gfx.Fill(sb, new Rectangle(x, 255, w, 110), Panel);
            bool over = m.Phase == GamePhase.Over;
            Color pc = over ? Gold : Color.White;
            Gfx.Wrapped(sb, Prompt(), x + 10, 265, 28, 2, pc, 20);
            if (!over && seatAi[m.ActingSeat])
                Gfx.Text(sb, "PC PREMYSLI...", x + 10, 340, 2, Dim);

            // volba barvy / pridani kamenu
            if (m.Phase == GamePhase.Choose && !seatAi[m.Actor])
            {
                string[] labels = { "VZIT CERNE (X)", "VZIT BILE (O)", "PRIDAT 2 KAMENY (O, X)" };
                for (int i = 0; i < m.OptionCount; i++)
                {
                    int oi = i;
                    Btn(labels[i], new Rectangle(x, 380 + i * 50, w, 42), false, () => m.Choose(oi));
                }
            }
            else if (m.Phase == GamePhase.Place && !seatAi[m.Actor])
            {
                Gfx.Text(sb, "KLIKNI NA PLOCHU", x, 380, 2, Dim);
            }

            // statistiky
            int sy = 530;
            Gfx.Text(sb, "STATISTIKY", x, sy, 3, Gold);
            if (gVsPc)
            {
                var r = save.Get(LevelNames[gLevel]);
                var t = Total();
                Gfx.Text(sb, $"{LevelNames[gLevel]}: {r.Wins}V {r.Losses}P {r.Draws}R", x, sy + 30, 2, Color.White);
                DrawBar(x, sy + 52, w, r);
                Gfx.Text(sb, $"CELKEM VS PC: {t.Wins}V {t.Losses}P {t.Draws}R", x, sy + 76, 2, Color.White);
                DrawBar(x, sy + 98, w, t);
            }
            else
            {
                var f = save.Get("Friend");
                Gfx.Text(sb, $"HRAC 1: {f.Wins}   HRAC 2: {f.Losses}", x, sy + 30, 2, Color.White);
                Gfx.Text(sb, $"REMIZ: {f.Draws}   HER: {f.Games}", x, sy + 52, 2, Color.White);
            }

            Btn("NOVA HRA", new Rectangle(x, 715, 170, 46), false, StartGame);
            Btn("MENU", new Rectangle(x + 185, 715, 170, 46), false, () => screen = Screen.Menu);
        }

        void DrawBar(int x, int y, int w, Rec r)
        {
            Gfx.Fill(sb, new Rectangle(x, y, w, 16), new Color(60, 64, 78));
            Gfx.Fill(sb, new Rectangle(x, y, (int)(w * r.Percent / 100.0), 16), new Color(70, 170, 90));
            string p = r.Games == 0 ? "-" : r.Percent.ToString("0.0") + "%";
            Gfx.TextC(sb, "VYHRAVAS " + p, x + w / 2, y + 1, 2, Color.White);
        }

        Rec Total()
        {
            var t = new Rec();
            foreach (var n in LevelNames)
            {
                var r = save.Get(n);
                t.Wins += r.Wins;
                t.Losses += r.Losses;
                t.Draws += r.Draws;
            }
            return t;
        }

        // ---------- statistiky ----------

        void DrawStats()
        {
            Gfx.TextC(sb, "STATISTIKY", W / 2, 30, 6, Gold);
            Gfx.Text(sb, "REZIM", 80, 120, 2, Dim);
            Gfx.Text(sb, "HRY", 430, 120, 2, Dim);
            Gfx.Text(sb, "VYHRY", 530, 120, 2, Dim);
            Gfx.Text(sb, "PROHRY", 650, 120, 2, Dim);
            Gfx.Text(sb, "REMIZY", 790, 120, 2, Dim);
            Gfx.Text(sb, "USPESNOST", 920, 120, 2, Dim);
            Gfx.Fill(sb, new Rectangle(80, 145, W - 160, 2), new Color(90, 96, 114));

            var rows = new List<(string label, Rec r, bool friend)>();
            for (int i = 0; i < 4; i++) rows.Add((LevelNames[i], save.Get(LevelNames[i]), false));
            rows.Add(("CELKEM VS PC", Total(), false));
            rows.Add(("KAMARAD (H1 / H2)", save.Get("Friend"), true));

            for (int i = 0; i < rows.Count; i++)
            {
                int y = 165 + i * 72;
                var (label, r, friend) = rows[i];
                Gfx.Text(sb, label, 80, y + 6, 3, i == 4 ? Gold : Color.White);
                Gfx.Text(sb, r.Games.ToString(), 430, y + 6, 3, Color.White);
                Gfx.Text(sb, r.Wins.ToString(), 530, y + 6, 3, new Color(110, 220, 130));
                Gfx.Text(sb, r.Losses.ToString(), 650, y + 6, 3, new Color(235, 110, 110));
                Gfx.Text(sb, r.Draws.ToString(), 790, y + 6, 3, Color.White);
                if (friend || r.Games == 0)
                    Gfx.Text(sb, "-", 920, y + 6, 3, Dim);
                else
                {
                    Gfx.Text(sb, r.Percent.ToString("0.0") + "%", 920, y, 2, Color.White);
                    Gfx.Fill(sb, new Rectangle(920, y + 24, 220, 12), new Color(60, 64, 78));
                    Gfx.Fill(sb, new Rectangle(920, y + 24, (int)(220 * r.Percent / 100.0), 12), new Color(70, 170, 90));
                }
                Gfx.Fill(sb, new Rectangle(80, y + 52, W - 160, 1), new Color(60, 66, 82));
            }

            Gfx.Text(sb, "USPESNOST = VYHRY / VSECHNY HRY", 80, 610, 2, Dim);

            Btn(confirmReset ? "OPRAVDU SMAZAT? KLIKNI ZNOVU" : "VYNULOVAT VSE",
                new Rectangle(80, 690, 480, 50), confirmReset, () =>
                {
                    if (!confirmReset) { confirmReset = true; return; }
                    save.Stats.Clear();
                    save.Board.Clear();
                    save.Save();
                    confirmReset = false;
                });
            Btn("ZPET", new Rectangle(W - 300, 690, 220, 50), false, () => { screen = Screen.Menu; confirmReset = false; });
        }

        // ---------- zebricek ----------

        static int LevelIdx(string name) => Math.Max(0, Array.IndexOf(LevelNames, name));

        void DrawLeaders()
        {
            Gfx.TextC(sb, "ZEBRICEK - HRA PROTI PC", W / 2, 30, 5, Gold);

            Btn("BODY HRACU", new Rectangle(240, 100, 340, 44), lbTab == 0, () => lbTab = 0);
            Btn("NEJRYCHLEJSI VYHRY", new Rectangle(600, 100, 340, 44), lbTab == 1, () => lbTab = 1);

            if (lbTab == 0)
            {
                Gfx.Text(sb, "BODY ZA VYHRU: EASY 1, MEDIUM 3, HARD 6, INSANE 10", 80, 165, 2, Dim);
                Gfx.Text(sb, "#", 80, 200, 2, Dim);
                Gfx.Text(sb, "JMENO", 130, 200, 2, Dim);
                Gfx.Text(sb, "BODY", 440, 200, 2, Dim);
                Gfx.Text(sb, "VYHRY", 560, 200, 2, Dim);
                Gfx.Text(sb, "HRY", 700, 200, 2, Dim);
                Gfx.Text(sb, "USPESNOST", 820, 200, 2, Dim);
                Gfx.Fill(sb, new Rectangle(80, 225, W - 160, 2), new Color(90, 96, 114));

                var groups = save.Board
                    .GroupBy(e => (e.Name ?? "").ToUpperInvariant())
                    .Select(g => new
                    {
                        Name = g.Key,
                        Pts = g.Where(e => e.Result == "W").Sum(e => LevelPoints[LevelIdx(e.Level)]),
                        Wins = g.Count(e => e.Result == "W"),
                        Games = g.Count()
                    })
                    .OrderByDescending(g => g.Pts)
                    .ThenByDescending(g => (double)g.Wins / g.Games)
                    .Take(10)
                    .ToList();

                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    int y = 240 + i * 42;
                    Color c = i == 0 ? Gold : Color.White;
                    Gfx.Text(sb, (i + 1).ToString(), 80, y, 3, c);
                    Gfx.Text(sb, g.Name, 130, y, 3, c);
                    Gfx.Text(sb, g.Pts.ToString(), 440, y, 3, c);
                    Gfx.Text(sb, g.Wins.ToString(), 560, y, 3, c);
                    Gfx.Text(sb, g.Games.ToString(), 700, y, 3, c);
                    Gfx.Text(sb, (100.0 * g.Wins / g.Games).ToString("0.0") + "%", 820, y, 3, c);
                }
                if (groups.Count == 0)
                    Gfx.TextC(sb, "ZATIM ZADNE ODEHRANE HRY PROTI PC", W / 2, 360, 3, Dim);
            }
            else
            {
                Gfx.Text(sb, "RAZENI: NEJTEZSI OBTIZNOST, PAK NEJMENE POLOZENYCH KAMENU", 80, 165, 2, Dim);
                Gfx.Text(sb, "#", 80, 200, 2, Dim);
                Gfx.Text(sb, "JMENO", 130, 200, 2, Dim);
                Gfx.Text(sb, "OBTIZNOST", 420, 200, 2, Dim);
                Gfx.Text(sb, "KAMENU", 620, 200, 2, Dim);
                Gfx.Text(sb, "OTEVIRANI", 760, 200, 2, Dim);
                Gfx.Text(sb, "DATUM", 960, 200, 2, Dim);
                Gfx.Fill(sb, new Rectangle(80, 225, W - 160, 2), new Color(90, 96, 114));

                var wins = save.Board
                    .Where(e => e.Result == "W")
                    .OrderByDescending(e => LevelIdx(e.Level))
                    .ThenBy(e => e.Moves)
                    .Take(10)
                    .ToList();

                for (int i = 0; i < wins.Count; i++)
                {
                    var e = wins[i];
                    int y = 240 + i * 42;
                    Color c = i == 0 ? Gold : Color.White;
                    Gfx.Text(sb, (i + 1).ToString(), 80, y, 3, c);
                    Gfx.Text(sb, (e.Name ?? ""), 130, y, 3, c);
                    Gfx.Text(sb, e.Level, 420, y, 3, c);
                    Gfx.Text(sb, e.Moves.ToString(), 620, y, 3, c);
                    Gfx.Text(sb, e.Opening, 760, y, 2, c);
                    Gfx.Text(sb, e.Date, 960, y, 2, c);
                }
                if (wins.Count == 0)
                    Gfx.TextC(sb, "ZATIM ZADNE VYHRY PROTI PC", W / 2, 360, 3, Dim);
            }

            Btn("ZPET", new Rectangle(W - 300, 690, 220, 50), false, () => screen = Screen.Menu);
        }
    }
}
