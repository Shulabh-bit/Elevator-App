using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ElevatorApp
{
    public class MainForm : Form, IElevatorObserver
    {
        // One column per lift: a shaft with a door on floor 2 (up) and a door on floor 1 (down).
        // The cabin moves inside the shaft, so you see it through the doors.
        private const int DoorW = 120, DoorH = 200;
        private const int Floor2Top = 62, Floor1Top = 338;
        private const int Floor2Bottom = Floor2Top + DoorH, Floor1Bottom = Floor1Top + DoorH;
        private static readonly int[] ColX = { 20, 160 };

        private static readonly Color Bg = Color.FromArgb(24, 27, 38);
        private static readonly Color CardCol = Color.FromArgb(38, 44, 62);
        private static readonly Color Accent = Color.FromArgb(64, 132, 244);
        private static readonly Font TitleFont = new Font("Segoe UI", 11, FontStyle.Bold);
        private static readonly Font LedFont = new Font("Consolas", 10, FontStyle.Bold);
        private static readonly Font FloorFont = new Font("Segoe UI", 9, FontStyle.Bold);

        private readonly Building building = new Building(2);   // two lifts
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 30 };
        private readonly ToolTip tips = new ToolTip();
        private readonly double[] doorOpen = new double[2];      // 0 = closed, 1 = open (outside door and cabin door together)
        private readonly Label[] leds = new Label[2];
        private readonly Label[] banners = new Label[2];
        private readonly Label[] floorDisplays = new Label[3];   // index 1 and 2 = floor number
        private readonly Button[] callButtons = new Button[3];   // index 1 and 2 = floor number
        private readonly Label[] callInfo = new Label[3];
        private readonly bool[] callPending = new bool[3];       // a call is waiting for a lift
        private readonly int[] callLift = new int[3];            // which lift was sent
        private BuildingView view;
        private Label statusLabel;
        private LogForm logForm;

        // Double buffered panel so the animation does not flicker
        private class BuildingView : Panel
        {
            public BuildingView() { DoubleBuffered = true; ResizeRedraw = true; }
        }

        public MainForm()
        {
            Text = "Elevator Controller";
            ClientSize = new Size(850, 680);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BuildGui();

            var logger = new LogObserver(ShowError);
            foreach (var e in building.Elevators) { e.Attach(this); e.Attach(logger); }

            timer.Tick += Timer_Tick;
            timer.Start();
            RefreshDisplays();
        }

        // ---------------- Task 1: GUI ----------------
        private void BuildGui()
        {
            BackColor = Bg;
            ForeColor = Color.WhiteSmoke;

            // Both lifts: shafts with the doors on floor 2 and floor 1 and the cabins inside
            view = new BuildingView { Location = new Point(175, 40), Size = new Size(300, 590) };
            view.Paint += DrawBuilding;
            Controls.Add(view);

            // Call buttons beside the doors, with a status display for each floor
            AddCallButton("\u25B2", "Floor 2 - call a lift", 2, 150);
            AddCallButton("\u25B2", "Floor 1 - call a lift", 1, 426);

            // One control panel for each lift
            for (int i = 0; i < building.Elevators.Count; i++)
            {
                var card = new Panel { Location = new Point(495, 40 + i * 290), Size = new Size(330, 270), BackColor = CardCol };
                card.Controls.Add(new Label
                {
                    Text = "ELEVATOR " + (i + 1),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    Location = new Point(15, 10)
                });

                leds[i] = new Label
                {
                    Location = new Point(15, 42),
                    Size = new Size(300, 52),
                    BackColor = Color.Black,
                    ForeColor = Color.Red,
                    Font = new Font("Consolas", 24, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                card.Controls.Add(leds[i]);

                banners[i] = new Label
                {
                    Location = new Point(15, 100),
                    Size = new Size(300, 30),
                    Font = new Font("Segoe UI", 13, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                card.Controls.Add(banners[i]);

                card.Controls.Add(MakeButton("1", new Point(15, 140), new Size(140, 52),
                    Color.FromArgb(70, 80, 110), new int[] { i, 1 }, FloorButton_Click, 18));
                card.Controls.Add(MakeButton("2", new Point(175, 140), new Size(140, 52),
                    Color.FromArgb(70, 80, 110), new int[] { i, 2 }, FloorButton_Click, 18));

                // Door buttons: a drawn icon (arrows apart = open, arrows together = close) with a word under it
                var open = MakeDoorButton(true, new Point(85, 198), 50, Color.FromArgb(46, 160, 100), i, DoorOpen_Click);
                var close = MakeDoorButton(false, new Point(195, 198), 50, Color.FromArgb(200, 70, 70), i, DoorClose_Click);
                tips.SetToolTip(open, "Open door");
                tips.SetToolTip(close, "Close door");
                card.Controls.Add(open);
                card.Controls.Add(close);
                card.Controls.Add(new Label
                {
                    Text = "OPEN",
                    Location = new Point(75, 250),
                    Size = new Size(70, 18),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.LightGray,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
                });
                card.Controls.Add(new Label
                {
                    Text = "CLOSE",
                    Location = new Point(185, 250),
                    Size = new Size(70, 18),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.LightGray,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
                });
                Controls.Add(card);
            }

            Controls.Add(MakeButton("LOG", new Point(24, 630), new Size(140, 36),
                Color.FromArgb(60, 150, 100), null, LogButton_Click));

            statusLabel = new Label { Location = new Point(495, 640), Size = new Size(330, 22), ForeColor = Color.Tomato };
            Controls.Add(statusLabel);
        }

        private void AddCallButton(string arrow, string tip, int floor, int y)
        {
            var b = MakeRoundButton(arrow, new Point(53, y), 64, Accent, floor, HallCall_Click, 18);
            tips.SetToolTip(b, tip);
            Controls.Add(b);
            callButtons[floor] = b;
            callInfo[floor] = new Label
            {
                Location = new Point(10, y - 26),
                Size = new Size(150, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(255, 170, 60),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            Controls.Add(callInfo[floor]);
            Controls.Add(new Label
            {
                Text = "FLOOR " + floor,
                Location = new Point(30, y + 68),
                Size = new Size(110, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            });

            floorDisplays[floor] = new Label
            {
                Location = new Point(10, y + 92),
                Size = new Size(150, 42),
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(floorDisplays[floor]);
        }

        private Button MakeButton(string text, Point loc, Size size, Color color, object tag,
                                  EventHandler handler, float fontSize = 10f)
        {
            var b = new Button
            {
                Text = text,
                Location = loc,
                Size = size,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Tag = tag,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += handler;
            return b;
        }

        // Round button with an arrow symbol
        private Button MakeRoundButton(string text, Point loc, int size, Color color, object tag,
                                       EventHandler handler, float fontSize)
        {
            Button b = MakeButton(text, loc, new Size(size, size), color, tag, handler, fontSize);
            b.Font = new Font("Segoe UI Symbol", fontSize, FontStyle.Bold);
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, size, size);
            b.Region = new Region(path);
            return b;
        }

        // Round door button whose icon is drawn: two arrows pointing apart = open, pointing together = close
        private Button MakeDoorButton(bool openIcon, Point loc, int size, Color color, int lift, EventHandler handler)
        {
            Button b = MakeButton("", loc, new Size(size, size), color, lift, handler);
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, size, size);
            b.Region = new Region(path);
            b.Paint += (s, e) => DrawDoorIcon(e.Graphics, b.ClientRectangle, openIcon);
            return b;
        }

        private static void DrawDoorIcon(Graphics g, Rectangle r, bool openIcon)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.Width / 2f, cy = r.Height / 2f;
            float h = r.Width * 0.20f;      // half height of an arrow
            float len = r.Width * 0.24f;    // length of an arrow
            float gap = r.Width * 0.07f;    // space between the arrow and the centre line
            using (var white = new SolidBrush(Color.White))
            using (var bar = new Pen(Color.White, 3f))
            {
                g.DrawLine(bar, cx, cy - h - 2, cx, cy + h + 2);
                PointF[] left, right;
                if (openIcon)       // arrows point away from the centre line
                {
                    left = new[] { new PointF(cx - gap, cy - h), new PointF(cx - gap, cy + h), new PointF(cx - gap - len, cy) };
                    right = new[] { new PointF(cx + gap, cy - h), new PointF(cx + gap, cy + h), new PointF(cx + gap + len, cy) };
                }
                else                // arrows point towards the centre line
                {
                    left = new[] { new PointF(cx - gap - len, cy - h), new PointF(cx - gap - len, cy + h), new PointF(cx - gap, cy) };
                    right = new[] { new PointF(cx + gap + len, cy - h), new PointF(cx + gap + len, cy + h), new PointF(cx + gap, cy) };
                }
                g.FillPolygon(white, left);
                g.FillPolygon(white, right);
            }
        }

        // ---------------- positions ----------------
        private static Rectangle DoorRect(int lift, int floor)
        {
            return new Rectangle(ColX[lift], floor == 2 ? Floor2Top : Floor1Top, DoorW, DoorH);
        }

        // The cabin follows the lift's Position (1.0 to 2.0)
        private Rectangle CabinRect(int i)
        {
            double t = (building.Elevators[i].Position - 1) / (Building.Floors - 1);
            int bottom = (int)Math.Round(Floor1Bottom - t * (Floor1Bottom - Floor2Bottom));
            return new Rectangle(ColX[i], bottom - DoorH, DoorW, DoorH);
        }

        // ---------------- drawing helpers ----------------
        // Metal frame: only the ring around the opening, so the cabin stays visible through it
        private static void DrawFrame(Graphics g, Rectangle opening)
        {
            Rectangle outer = Rectangle.Inflate(opening, 8, 8);
            using (var ring = new Region(outer))
            {
                ring.Exclude(opening);
                using (var fb = new LinearGradientBrush(outer, Color.FromArgb(214, 218, 224), Color.FromArgb(118, 124, 136), 45f))
                    g.FillRegion(fb, ring);
            }
            using (var p = new Pen(Color.FromArgb(40, 44, 54), 1)) g.DrawRectangle(p, outer);
            using (var p = new Pen(Color.FromArgb(70, 74, 84), 2)) g.DrawRectangle(p, opening);
        }

        // Two sliding door leaves; open = 0 (closed) to 1 (fully open); alpha < 255 lets the cabin show through
        private static void DrawLeaves(Graphics g, Rectangle opening, double open, int alpha)
        {
            int w = (int)Math.Round(opening.Width / 2.0 * (1 - open));
            if (w <= 1) return;
            var left = new Rectangle(opening.X, opening.Y, w, opening.Height);
            var right = new Rectangle(opening.Right - w, opening.Y, w, opening.Height);
            foreach (var leaf in new[] { left, right })
            {
                using (var lb = new LinearGradientBrush(leaf, Color.FromArgb(alpha, 208, 213, 220), Color.FromArgb(alpha, 136, 142, 154), 0f))
                    g.FillRectangle(lb, leaf);
                using (var line = new Pen(Color.FromArgb(alpha / 4, 255, 255, 255), 1))
                {
                    g.DrawLine(line, leaf.X + leaf.Width / 3, leaf.Y + 6, leaf.X + leaf.Width / 3, leaf.Bottom - 6);
                    g.DrawLine(line, leaf.X + 2 * leaf.Width / 3, leaf.Y + 6, leaf.X + 2 * leaf.Width / 3, leaf.Bottom - 6);
                }
                using (var edge = new Pen(Color.FromArgb(alpha, 50, 54, 62), 1)) g.DrawRectangle(edge, leaf);
            }
        }

        // Warm light of the cabin interior, seen through the open doors
        private static void DrawCabinLight(Graphics g, Rectangle opening)
        {
            using (var br = new LinearGradientBrush(opening, Color.FromArgb(252, 240, 208), Color.FromArgb(214, 188, 138), 90f))
                g.FillRectangle(br, opening);
        }

        private static string ArrowOf(Elevator el)
        {
            return el.State is MovingUpState ? " \u25B2" : el.State is MovingDownState ? " \u25BC" : "";
        }

        // ---------------- the building: shafts, cabins and doors ----------------
        private void DrawBuilding(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(20, 23, 32));

            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i < building.Elevators.Count; i++)
                {
                    Elevator el = building.Elevators[i];
                    g.DrawString("LIFT " + (i + 1), TitleFont, Brushes.White, new Rectangle(ColX[i], 2, DoorW, 22), sf);

                    // 1. the shaft behind the doors, with guide rails
                    var shaft = new Rectangle(ColX[i] - 6, 30, DoorW + 12, 530);
                    using (var b = new SolidBrush(Color.FromArgb(12, 14, 20))) g.FillRectangle(b, shaft);
                    using (var rail = new Pen(Color.FromArgb(50, 56, 72), 2))
                    {
                        g.DrawLine(rail, shaft.X + 4, shaft.Y, shaft.X + 4, shaft.Bottom);
                        g.DrawLine(rail, shaft.Right - 4, shaft.Y, shaft.Right - 4, shaft.Bottom);
                    }

                    // 2. the cabin, hanging from its cable
                    Rectangle car = CabinRect(i);
                    using (var cable = new Pen(Color.Gray, 2))
                        g.DrawLine(cable, car.X + car.Width / 2, shaft.Y, car.X + car.Width / 2, car.Y);

                    Color hi, lo;
                    if (el.State is DoorOpenState) { hi = Color.FromArgb(240, 170, 70); lo = Color.FromArgb(180, 110, 30); }
                    else if (el.State is MovingState) { hi = Color.FromArgb(70, 190, 130); lo = Color.FromArgb(30, 130, 90); }
                    else { hi = Color.FromArgb(100, 125, 170); lo = Color.FromArgb(60, 80, 125); }
                    using (var br = new LinearGradientBrush(car, hi, lo, 90f)) g.FillRectangle(br, car);
                    using (var p = new Pen(Color.FromArgb(15, 15, 20), 2)) g.DrawRectangle(p, car);

                    g.DrawString("LIFT " + el.Id + ArrowOf(el), TitleFont, Brushes.White,
                        new Rectangle(car.X, car.Y + 2, car.Width, 26), sf);

                    // the cabin's own door: it opens together with the outside door
                    var inner = new Rectangle(car.X + 12, car.Y + 30, car.Width - 24, car.Height - 42);
                    if (doorOpen[i] > 0.01) DrawCabinLight(g, inner);
                    else using (var dark = new SolidBrush(Color.FromArgb(40, 44, 54))) g.FillRectangle(dark, inner);
                    DrawLeaves(g, inner, doorOpen[i], 255);
                    using (var frame = new Pen(Color.FromArgb(50, 54, 62), 1)) g.DrawRectangle(frame, inner);

                    // 3. the outside doors in front, on floor 2 and floor 1
                    foreach (int floor in new[] { 2, 1 })
                    {
                        Rectangle opening = DoorRect(i, floor);

                        // the door slides open only on the floor where this lift is standing
                        double open = (el.CurrentFloor == floor && Math.Abs(el.Position - floor) < 0.02) ? doorOpen[i] : 0;

                        DrawFrame(g, opening);
                        DrawLeaves(g, opening, open, 165);   // see-through, so you can watch the cabin pass behind

                        // red floor display above the door
                        var disp = new Rectangle(opening.X + 10, opening.Y - 34, 100, 22);
                        g.FillRectangle(Brushes.Black, disp);
                        g.DrawString("FLOOR " + el.CurrentFloor + ArrowOf(el), LedFont, Brushes.Red, disp, sf);

                        g.DrawString("FLOOR " + floor, FloorFont, Brushes.Gray,
                            new Rectangle(opening.X, opening.Bottom + 10, DoorW, 20), sf);
                    }
                }
            }
        }

        // ---------------- Task 2: event handlers ----------------
        private void HallCall_Click(object sender, EventArgs e)
        {
            int floor = (int)((Button)sender).Tag;
            Elevator chosen = building.DispatchHallCall(floor);     // nearest lift answers
            callPending[floor] = true;                              // the button lights up until the lift arrives
            callLift[floor] = chosen.Id;
            UpdateCallButtons();
        }

        private void FloorButton_Click(object sender, EventArgs e)
        {
            int[] tag = (int[])((Button)sender).Tag;
            building.CabinRequest(tag[0], tag[1]);                   // only that lift answers
        }

        private void DoorOpen_Click(object sender, EventArgs e)
        {
            building.Elevators[(int)((Button)sender).Tag].OpenDoorRequest();
        }

        private void DoorClose_Click(object sender, EventArgs e)
        {
            building.Elevators[(int)((Button)sender).Tag].CloseDoorRequest();
        }

        private void LogButton_Click(object sender, EventArgs e)
        {
            if (logForm == null || logForm.IsDisposed) { logForm = new LogForm(); logForm.Show(this); }
            else { logForm.BringToFront(); logForm.Reload(); }
        }

        // ---------------- Task 4: timer animation via delegate ----------------
        // True when the cursor is on the open door or the cabin of this lift
        private bool CursorOnLift(int i)
        {
            Elevator el = building.Elevators[i];
            Point p = view.PointToClient(Cursor.Position);
            return DoorRect(i, el.CurrentFloor).Contains(p) || CabinRect(i).Contains(p);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            building.RaiseTick();   // the delegate moves both cabins

            for (int i = 0; i < building.Elevators.Count; i++)
            {
                Elevator el = building.Elevators[i];

                // cursor on a lift while its doors are open keeps them open
                if (el.State is DoorOpenState && CursorOnLift(i)) el.HoldDoor();

                // doors slide open at the start and close during the last 0.6 s
                var doorState = el.State as DoorOpenState;
                double target = (doorState != null && !doorState.IsClosing) ? 1 : 0;
                doorOpen[i] += (target - doorOpen[i]) * 0.12;   // smooth door animation

                UpdateBanner(i);
            }
            for (int f = 1; f <= 2; f++)
                if (callPending[f])
                    foreach (Elevator el in building.Elevators)
                        if (el.State is DoorOpenState && el.CurrentFloor == f) callPending[f] = false;
            UpdateCallButtons();

            view.Invalidate();
        }

        // ---------------- Observer callback ----------------
        public void ElevatorChanged(Elevator elevator, string logMessage) { RefreshDisplays(); }

        private void UpdateBanner(int i)
        {
            Elevator el = building.Elevators[i];
            string text; Color color;
            if (el.State is MovingUpState) { text = "COMING UP  \u25B2"; color = Color.FromArgb(120, 230, 140); }
            else if (el.State is MovingDownState) { text = "COMING DOWN  \u25BC"; color = Color.FromArgb(120, 230, 140); }
            else if (el.State is DoorOpenState)
            {
                text = ((DoorOpenState)el.State).IsClosing ? "DOORS CLOSING" : "DOORS OPEN";
                color = Color.FromArgb(255, 190, 80);
            }
            else { text = "WAITING AT FLOOR " + el.CurrentFloor; color = Color.LightGray; }
            banners[i].Text = text;
            banners[i].ForeColor = color;
        }

        private void RefreshDisplays()
        {
            string summary = "";
            for (int i = 0; i < building.Elevators.Count; i++)
            {
                Elevator el = building.Elevators[i];
                leds[i].Text = "Floor " + el.CurrentFloor + ArrowOf(el);
                UpdateBanner(i);
                summary += "E" + el.Id + ": F" + el.CurrentFloor + " " + el.State.Name + "\r\n";
            }
            floorDisplays[1].Text = summary.TrimEnd();
            floorDisplays[2].Text = summary.TrimEnd();
            view.Invalidate();
        }

        // Orange button and "LIFT n IS COMING" while a call is waiting, blue again when the lift arrives
        private void UpdateCallButtons()
        {
            for (int f = 1; f <= 2; f++)
            {
                callButtons[f].BackColor = callPending[f] ? Color.FromArgb(255, 170, 60) : Accent;
                callInfo[f].Text = callPending[f] ? "LIFT " + callLift[f] + " IS COMING" : "";
            }
        }

        private void ShowError(Exception ex)
        {
            statusLabel.Text = "Database error: " + ex.Message;
        }
    }
}