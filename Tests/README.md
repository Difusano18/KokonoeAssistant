# KokonoeAssistant Tests

Manual test harness for the core Kokonoe behavior engines.

Run:

```powershell
dotnet run --project Tests/KokonoeAssistant.Tests/KokonoeAssistant.Tests.csproj
```

Current coverage:

- somatic `wired` classification from pulse spike
- somatic `tired` classification from low charge
- self-regulation `pulse_spike -> clamp`
- self-regulation `vulnerable -> protective_override`
- initiative low-power silence gate
- initiative protective override
