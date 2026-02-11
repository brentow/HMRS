# HRMS

A WPF-based Human Resources Management System (HRMS) desktop application.

## Tech Stack
- .NET 10 (WPF)
- MySQL
- MaterialDesignThemes

## Prerequisites
- .NET 10 SDK
- MySQL Server
- Visual Studio 2022 or later (optional for IDE workflow)

## Setup
1. Ensure MySQL is running locally.
2. Create a database named `Human_Resources_Management_System`.
3. Update the connection string in `HRMS/Model/DbConfig.cs`.

## Build and Run
1. Restore packages: `dotnet restore`
2. Build: `dotnet build`
3. Run: `dotnet run --project HRMS/HRMS.csproj`

## Notes
- The current connection string is hardcoded in `HRMS/Model/DbConfig.cs`. For production use, move it to a safer store (environment variables, user secrets, or a secure configuration provider).
- This repository uses `HRMS.slnx` for the solution file.