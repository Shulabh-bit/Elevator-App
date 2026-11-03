# Elevator Controller – A C# Project

A Windows desktop simulation of **two lifts serving two floors**, written in C# with Windows Forms and a SQL Server log.
You can call a lift from a floor, choose a floor inside it, open and close its doors, and read the history of everything the lifts did.



## Features

- **Two lifts, two floors.** Each lift has a shaft with a door on floor 2 (top) and floor 1 (below). The cabin moves up and down inside the shaft and can be seen through the doors.
- **Call buttons.** The nearest lift answers a call. The button turns orange and shows "LIFT n IS COMING" until that lift opens its doors on your floor.
- **Control panel for each lift.** A floor display, floor buttons 1 and 2, open-door and close-door buttons, and a message: COMING UP, COMING DOWN, DOORS OPEN or WAITING AT FLOOR.
- **Realistic doors.** The doors stay open about 2.7 seconds, and they stay open while the mouse cursor rests on the lift. A door can never open while the lift is moving.
- **Log window.** Every request and every change of state is saved with a time. The window can refresh, delete one record or clear all records.
- **Responsive screen.** Database work runs on a `BackgroundWorker`, so the window never freezes.

## Built with

| Part | Technology |
|---|---|
| Language | C# on .NET 8 |
| Interface | Windows Forms, drawn with GDI+ |
| Database | SQL Server LocalDB through `Microsoft.Data.SqlClient` |
| Data access | `SqlDataAdapter` and `DataTable` (disconnected model) |
| Animation | `Timer` and a `TickHandler` delegate |

## Getting started

### What you need

- Windows 10 or later
- Visual Studio 2022 with the **.NET desktop development** workload
- **SQL Server LocalDB** (it is installed with Visual Studio when you tick *Data storage and processing* in the Visual Studio Installer)

### Run it

1. Clone the repository:
   ```
   git clone https://github.com/ShulabhShah/ElevatorApp.git
   ```
2. Open `ElevatorApp.slnx` in Visual Studio.
3. Wait for the NuGet package `Microsoft.Data.SqlClient` to restore.
4. Press **F5**.

The database `ElevatorLogDB` and its table are created automatically the first time the program starts. The database file is placed in a `Data` folder next to the program, so no file path has to be changed.

If you use a full SQL Server instead of LocalDB, change the `Server` value at the top of `LogRepository.cs`, for example to `.\SQLEXPRESS`.

## How to use it

1. Press the **▲** button on floor 1 or floor 2 to call a lift.
2. Watch the message on the control panel change to **COMING UP** or **COMING DOWN**. The cabin moves inside its shaft, then the door on your floor and the cabin door open together.
3. Press **1** or **2** on a lift's control panel to send that lift to a floor.
4. Use the **open** and **close** buttons for the doors. They are refused while the lift is moving.
5. Press **LOG** to see the saved history. Use **Delete**, **Clear All Logs** and **Refresh** there.

## Project structure

```
ElevatorApp/
├── Program.cs          starts the program and creates the database
├── MainForm.cs         the main window, drawing, buttons and animation
├── Elevator.cs         the Elevator class, the states and the observer interface
├── Building.cs         holds the lifts, picks the nearest lift, raises the timer tick
├── Background.cs       BackgroundWorker helper and the LogObserver that saves events
├── LogRepository.cs    all database access through one adapter method
└── LogForm.cs          the log window
```

## How it works

- **State pattern.** Each lift is always in one state: `IdleState`, `MovingUpState`, `MovingDownState` or `DoorOpenState`. On every timer tick the current state decides what happens next, so there are no long if-else chains.
- **Observer pattern.** A lift tells its observers when it changes. `MainForm` redraws the screen and `LogObserver` saves the event, and the lift knows nothing about either.
- **Nearest lift.** `EstimateTime` works out how long each lift needs to reach the floor, counting the stops it already has, and the best lift gets the call without disturbing its current trip.
- **Delegates and timer.** One timer raises a `TickHandler` delegate every 30 ms and every lift moves a little.
- **Disconnected data model.** `LogRepository` fills a `DataTable`, changes it and saves it back with `DataAdapter.Update`. One helper method, `WithAdapter`, is reused by all database calls, so no database code is duplicated.

## Database

One table, `ElevatorLog`:

| Column | Type | Meaning |
|---|---|---|
| `Id` | INT, identity | primary key |
| `ElevatorId` | INT | which lift (1 or 2) |
| `Floor` | INT | floor when the event happened |
| `Status` | NVARCHAR(100) | what happened, for example *Moving Up* |
| `LoggedAt` | DATETIME | time of the event |

## Testing

The project was tested with black box tests (using the program) and white box tests (calling the classes directly). The white box tests check that a floor is queued only once, that the nearest lift is chosen, that the states change in the right order, and that a door cannot open while a lift is moving.



