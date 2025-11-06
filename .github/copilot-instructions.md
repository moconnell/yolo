# GitHub Copilot Instructions for YOLO

## Project Overview

This is a .NET 9 C# application that automates RobotWealth cryptocurrency YOLO (You Only Live Once) trading strategies. The application provides integrations with cryptocurrency exchanges (Binance, Hyperliquid) and factor data providers (RobotWealth, Unravel).

## Technology Stack

- **Language**: C# with .NET 9.0
- **Testing Framework**: xUnit with Moq for mocking, Shouldly for assertions
- **Coverage**: Coverlet for code coverage
- **CI/CD**: GitHub Actions
- **Package Management**: Central Package Management (Directory.Packages.props)

## Project Structure

- `src/` - Source code organized by component
  - `YoloKonsole/` - Main console application
  - `YoloBroker/` - Core broker abstractions
  - `YoloBroker.Binance/` - Binance exchange integration
  - `YoloBroker.Hyperliquid/` - Hyperliquid exchange integration
  - `YoloTrades/` - Trade execution logic
  - `YoloWeights/` - Portfolio weighting algorithms
  - `YoloAbstractions/` - Common abstractions and interfaces
  - `RobotWealth.Api/` - RobotWealth API client
  - `Unravel.Api/` - Unravel API client
  - `YoloFunk/` - Utility functions
- `test/` - Test projects (mirror source structure with .Test suffix)

## Development Guidelines

### Code Style

- Use C# 12+ features including nullable reference types (`Nullable` is enabled)
- Follow implicit usings pattern (`ImplicitUsings` is enabled)
- Maintain existing code style and conventions
- Avoid adding comments unless they explain complex logic or match existing comment patterns

### Building and Testing

**Restore dependencies:**
```bash
dotnet restore
```

**Build the solution:**
```bash
dotnet build --no-restore
```

**Run tests (excluding integration tests):**
```bash
dotnet test --filter "Category!=Integration" --no-build --verbosity normal
```

**Run tests with coverage:**
```bash
dotnet test --filter "Category!=Integration" --no-build --verbosity normal \
  /p:CollectCoverage=true \
  /p:CoverletOutput=../results/ \
  /p:MergeWith=../results/coverage.info \
  /p:CoverletOutputFormat=lcov
```

### Testing Approach

- Test projects follow naming convention: `[ProjectName].Test`
- Use xUnit as the testing framework
- Use Moq for creating mock objects
- Use Shouldly for fluent assertions
- Integration tests should be marked with `[Trait("Category", "Integration")]`
- Maintain high test coverage (tracked via Coveralls)

### Package Management

- All package versions are centrally managed in `Directory.Packages.props`
- When adding a new package, add it to `Directory.Packages.props` first
- Reference packages in `.csproj` files without version numbers

### Security Considerations

- Never commit secrets or API keys to source control
- The application uses `.\secrets` directory for sensitive configuration
- Secrets should be managed via the `setup-secrets.ps1` PowerShell script

## Making Changes

1. **Always run tests before making changes** to understand the current state
2. **Make minimal, focused changes** - avoid refactoring unrelated code
3. **Run build and tests after changes** to verify nothing breaks
4. **Follow existing patterns** in the codebase
5. **Update tests** when changing functionality
6. **Maintain backward compatibility** unless explicitly changing APIs

## Common Tasks

### Adding a New Package

1. Add the package version to `Directory.Packages.props`:
   ```xml
   <PackageVersion Include="PackageName" Version="x.y.z" />
   ```

2. Reference it in the relevant `.csproj` file:
   ```xml
   <PackageReference Include="PackageName" />
   ```

### Adding a New Project

1. Follow the existing structure in `src/` or `test/`
2. Use .NET 9.0 as the target framework
3. Enable nullable reference types and implicit usings
4. Add project reference to `Yolo.sln`
5. Create a corresponding test project in `test/`

### Running the Application

The main executable is `YoloKonsole`. It requires:
- Configuration in `appsettings.json`
- Secrets configured via `setup-secrets.ps1`
- Valid API credentials for exchanges and data providers
