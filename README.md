# PetalPal Sample API

PetalPal is a deliberately small API for demonstrating the customer journey in
the MCP Stack blog post. It gives each signed-in SqlOS user a private garden and
exposes a few ChatGPT-friendly operations:

- get the current user's garden
- list plants
- create a plant
- water a plant
- trace the SqlOS FGA decision for a plant write

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
  --name petalpal \
  --slug petalpal \
  --runtime-type hosted \
  --openapi-url https://petalpal.example.com/swagger/v1/swagger.json \
  --json

mcpstack gateways create \
  --name "PetalPal SqlOS" \
  --provider sqlos \
  --auth-server-url http://localhost:5098/sqlos/auth \
  --client-id petalpal-mcpstack-gateway \
  --resource http://localhost:5098/api \
  --scopes "openid profile email gardens.read gardens.write" \
  --json

mcpstack gateway-public doctor petalpal --client chatgpt-web
```
