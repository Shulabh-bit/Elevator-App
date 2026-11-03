using System.Collections.Generic;
using System.Linq;

namespace ElevatorApp
{
    public class Building
    {
        public const int Floors = 2;
        public List<Elevator> Elevators { get; private set; }
        public event TickHandler Ticked;

        public Building(int elevatorCount = 2)
        {
            Elevators = new List<Elevator>();
            for (int i = 1; i <= elevatorCount; i++)
                Elevators.Add(new Elevator(i, i % 2 == 1 ? 1 : 2));
            foreach(var e in Elevators) Ticked += e.Tick;
        }

        public void RaiseTick() { if (Ticked != null) Ticked(); }

        
        public Elevator DispatchHallCall(int floor)
        {
            Elevator best = Elevators.OrderBy(e => e.EstimateTime(floor)).First();
            best.Enqueue(floor, "Hall call floor " + floor + " assigned");
            return best;
        }

        
        public void CabinRequest(int elevatorIndex, int floor)
        {
            Elevators[elevatorIndex].Enqueue(floor, "Cabin request floor " + floor);
        }
    }
}