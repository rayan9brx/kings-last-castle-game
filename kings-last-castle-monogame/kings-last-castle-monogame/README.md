# King's Last Castle

King's Last Castle is a C# / MonoGame tower-defense style game project developed as a university team project.  
The project focuses on object-oriented game logic, enemy waves, tower behavior, rendering, and game-state management.

## Project Overview

The goal of this project was to design and implement the core logic of a 3D tower-defense game.  
The game includes enemy waves, different tower types, player resources, castle health, visual shot effects, and win/lose conditions.

## Technologies

- C#
- .NET 9
- MonoGame
- Object-Oriented Programming
- Git / GitHub

## Features

- Enemy wave system
- Multiple enemy types: standard, heavy, and flying enemies
- Multiple tower types: arrow, cannon, and magic towers
- Tower placement and attack logic
- Gold, castle health, victory, and defeat logic
- Basic 3D model rendering
- Camera and visual shot effects
- Game state handling: preparation, active wave, victory, and defeat

## My Contribution

This was a university team project. My main contribution focused on the tower system and 3D/gameplay presentation, including:

- Tower placement logic
- Tower combat behavior
- Balancing of tower range, cooldown, and damage
- Visual shot effects
- Camera and 3D model display
- Integration, testing, and gameplay adjustments together with the team

## Project Structure

```text
KingsLastCastle/
├── Content/                  # MonoGame content pipeline files and 3D models
│   ├── Models/               # FBX models for towers, enemies, and castle
│   ├── Content.mgcb          # MonoGame content build configuration
│   └── Default.spritefont
├── Rendering/                # Rendering helper classes
├── KingsLastCastle.csproj    # .NET / MonoGame project file
├── MilestoneGame.cs          # Main game logic
└── Program.cs                # Application entry point
```

## How to Run

### Prerequisites

- .NET 9 SDK
- MonoGame dependencies

### Run the Project

```bash
git clone https://github.com/rayan9brx/kings-last-castle-monogame.git
cd kings-last-castle-monogame

dotnet restore
dotnet build
dotnet run
```

## Screenshots

Screenshots will be added here.

Recommended screenshots to add later:

- Main game view
- Tower placement
- Enemy wave in progress
- Victory or defeat screen

## What I Learned

Through this project, I improved my understanding of object-oriented programming, game loops, game-state management, debugging, and collaborative software development.  
The project helped me apply programming concepts in a practical game-development context.

## Notes

Generated build folders such as `bin/`, `obj/`, `Content/bin/`, and `Content/obj/` are intentionally excluded from this repository. They are recreated automatically when the project is restored and built.
