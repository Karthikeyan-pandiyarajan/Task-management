# TaskApp client (Angular 22)

See the [root README](../README.md) for full setup. Quick reference:

```powershell
npm install
npm start                                         # http://localhost:4200, proxies /api to the two APIs
npm test -- --watch=false --browsers=ChromeHeadless
npm run build
```

Both APIs must be running (`dotnet run --project src/Api.Auth` and `dotnet run --project src/Api.Tasks` from the repo root).
