# PetalPal Sample API

PetalPal is a deliberately small API for demonstrating the customer journey in
the MCP Stack blog post. It gives each signed-in SqlOS user a private garden and
exposes a few ChatGPT-friendly operations:

- get the current user's garden
- list plants
- create a plant
- water a plant
- trace the SqlOS FGA decision for a plant write

Live walkthrough artifacts:

- App: https://petalpal-mcpstack-journey.azurewebsites.net
- OpenAPI: https://petalpal-mcpstack-journey.azurewebsites.net/swagger/v1/swagger.json
- MCP Stack guide: https://mcpstack.com/guides/petalpal-api-mcp
- Verified MCP Gateway URL: https://api.mcpstack.com/api/v1/gateway/gw_petalpal-api-mcp/mcp

Run it locally with SQL Server available:

```bash
dotnet run --project examples/PetalPal.Sample.Api --urls http://localhost:5098
```

Override the connection string if your SQL Server is on a non-default port:

```bash
ConnectionStrings__DefaultConnection='Server=127.0.0.1,60412;Database=PetalPalSample;User Id=sa;Password=LocalDevPassword123!;TrustServerCertificate=True;' \
  dotnet run --project examples/PetalPal.Sample.Api --urls http://localhost:5098
```

Useful URLs:

- `http://localhost:5098/` for the small landing page
- `http://localhost:5098/swagger` for OpenAPI
- `http://localhost:5098/sample/config` for the SqlOS OAuth configuration
- `http://localhost:5098/.well-known/oauth-protected-resource` for protected resource metadata
- `http://localhost:5098/sqlos` for the SqlOS dashboard

MCP Stack CLI shape. Use a public URL or tunnel for `--openapi-url`; for a
pure local run, export the Swagger JSON and pass it with `--openapi-file`.

```bash
mcpstack servers create \
  --name petalpal-api-mcp \
  --runtime-type hosted \
  --openapi-url https://petalpal-mcpstack-journey.azurewebsites.net/swagger/v1/swagger.json \
  --json

mcpstack gateways create \
  --name "PetalPal SqlOS" \
  --provider sqlos \
  --auth-server-url https://petalpal-mcpstack-journey.azurewebsites.net/sqlos/auth \
  --client-id petalpal-mcpstack-gateway \
  --resource https://petalpal-mcpstack-journey.azurewebsites.net/api \
  --scopes "openid profile email gardens.read gardens.write" \
  --json

mcpstack gateway-public doctor \
  --url https://api.mcpstack.com/api/v1/gateway/gw_petalpal-api-mcp/mcp \
  --client chatgpt-web \
  --json
```
