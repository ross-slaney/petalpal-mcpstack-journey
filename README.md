# PetalPal Garden

PetalPal is a deliberately small SaaS-style app for demonstrating the customer
journey in the MCP Stack blog post. A user signs in, gets a private garden,
adds and waters plants, and then the same OAuth-protected API is exposed to
ChatGPT through MCP Stack.

The browser app includes:

- first-party OAuth sign-in
- a normal signed-in garden dashboard
- add plant and water plant actions
- per-user garden access control
- a ChatGPT setup panel with the hosted MCP URL

The API exposes a few ChatGPT-friendly operations:

- get the current user's garden
- list plants
- create a plant
- water a plant
- trace the access-control decision for a plant write

Live walkthrough artifacts:

- App: https://petalpal-mcpstack-journey.azurewebsites.net
- OpenAPI: https://petalpal-mcpstack-journey.azurewebsites.net/swagger/v1/swagger.json
- MCP Stack guide: https://mcpstack.com/guides/petalpal-api-mcp
- Hosted MCP URL: https://petalpal-api-mcp.mcp.mcpstack.com/mcp

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

- `http://localhost:5098/` for the PetalPal web app
- `http://localhost:5098/oauth/callback` for the local browser OAuth callback
- `http://localhost:5098/swagger` for OpenAPI
- `http://localhost:5098/sample/config` for the OAuth configuration
- `http://localhost:5098/.well-known/oauth-protected-resource` for protected resource metadata
- `http://localhost:5098/sqlos` for the local auth/admin dashboard

MCP Stack CLI shape. Use a public URL or tunnel for `--openapi-url`; for a
pure local run, export the Swagger JSON and pass it with `--openapi-file`.

```bash
mcpstack servers create \
  --name petalpal-api-mcp \
  --runtime-type hosted \
  --openapi-url https://petalpal-mcpstack-journey.azurewebsites.net/swagger/v1/swagger.json \
  --json

mcpstack gateways create \
  --name "PetalPal OAuth" \
  --provider sqlos \
  --auth-server-url https://petalpal-mcpstack-journey.azurewebsites.net/sqlos/auth \
  --client-id petalpal-mcpstack-gateway \
  --resource https://petalpal-mcpstack-journey.azurewebsites.net/api \
  --scopes "openid profile email gardens.read gardens.write" \
  --json

curl -i https://petalpal-api-mcp.mcp.mcpstack.com/mcp
```
