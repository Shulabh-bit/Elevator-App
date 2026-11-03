using System;
using System.Collections.Generic;
using System.Linq;

namespace ElevatorApp
{
    // ---------- Observer pattern ----------
    public interface IElevatorObserver
    {
        // logMessage == null -> just a position update (animation frame)
        void ElevatorChanged(Elevator elevator, string logMessage);
    }

    // Delegate used by the timer to animate every elevator (Task 4)
    public delegate void TickHandler();

    // ---------- State pattern ----------
    public abstract class ElevatorState
    {
        public abstract string Name { get; }
        public virtual void Enter(Elevator e) { }
        public abstract void Tick(Elevator e);
    }

    public class IdleState : ElevatorState
    {
        public override string Name { get { return "Idle"; } }
        public override void Tick(Elevator e)
        {
            if (!e.HasTargets) return;
            int target = e.NextTarget;
            if (Math.Abs(e.Position - target) < Elevator.Epsilon) e.Arrive();
            else if (target > e.Position) e.SetState(new MovingUpState());
            else e.SetState(new MovingDownState());
        }
    }

    public abstract class MovingState : ElevatorState
    {
        protected abstract int Direction { get; }
        public override void Tick(Elevator e)
        {
            if (!e.HasTargets) { e.SetState(new IdleState()); return; }
            int target = e.NextTarget;
            double next = e.Position + Direction * Elevator.Speed;
            bool reached = Direction > 0 ? next >= target : next <= target;
            e.MoveTo(reached ? target : next);
            if (reached) e.Arrive();
        }
    }

    public class MovingUpState : MovingState
    {
        public override string Name { get { return "Moving Up"; } }
        protected override int Direction { get { return 1; } }
    }

    public class MovingDownState : MovingState
    {
        public override string Name { get { return "Moving Down"; } }
        protected override int Direction { get { return -1; } }
    }

    public class DoorOpenState : ElevatorState
    {
        private const int OpenTicks = 90;
        private const int ClosingTicks = 20;
        private int remaining;

        public override string Name { get { return "Door Open"; } }
        public bool IsClosing { get { return remaining <= ClosingTicks; } }

        public override void Enter(Elevator e) { remaining = OpenTicks; }
        public void Extend() { remaining = OpenTicks; }

        public override void Tick(Elevator e)
        {
            if (--remaining <= 0) e.SetState(new IdleState());
        }
    }

    // ---------- Context ----------
    public class Elevator
    {
        public const double Speed = 0.04;
        public const double Epsilon = 0.0001;

        private readonly List<int> targets = new List<int>();
        private readonly List<IElevatorObserver> observers = new List<IElevatorObserver>();

        public int Id { get; private set; }
        public double Position { get; private set; }
        public ElevatorState State { get; private set; }

        public Elevator(int id, int startFloor)
        {
            Id = id;
            Position = startFloor;
            State = new IdleState();
        }

        public int CurrentFloor { get { return (int)Math.Round(Position); } }
        public bool HasTargets { get { return targets.Count > 0; } }
        public int NextTarget { get { return targets[0]; } }

        public void Attach(IElevatorObserver o) { observers.Add(o); }

        public void Notify(string logMessage)
        {
            foreach (var o in observers.ToList()) o.ElevatorChanged(this, logMessage);
        }

        public void Tick() { State.Tick(this); }

        public void Enqueue(int floor, string reason)
        {
            if (!targets.Contains(floor)) targets.Add(floor);
            Notify(reason);
        }

        internal void SetState(ElevatorState s)
        {
            State = s;
            s.Enter(this);
            Notify(s.Name);
        }

        internal void MoveTo(double position)
        {
            Position = position;
            Notify(null);
        }

        internal void Arrive()
        {
            targets.RemoveAt(0);
            SetState(new DoorOpenState());
        }

        public void OpenDoorRequest()
        {
            if (State is DoorOpenState) { ((DoorOpenState)State).Extend(); Notify("Door held open"); }
            else if (State is IdleState) SetState(new DoorOpenState());
            else Notify("Door open rejected (lift moving)");
        }

        public void CloseDoorRequest()
        {
            if (State is DoorOpenState) SetState(new IdleState());
            else Notify("Door close ignored");
        }

        public void HoldDoor()
        {
            var d = State as DoorOpenState;
            if (d != null) d.Extend();
        }

        // Estimated response time, counting everything already queued
        public double EstimateTime(int floor)
        {
            double t = State is DoorOpenState ? 0.5 : 0;
            double pos = Position;
            foreach (int f in targets) { t += Math.Abs(pos - f) + 0.5; pos = f; }
            return t + Math.Abs(pos - floor);
        }
    }
}