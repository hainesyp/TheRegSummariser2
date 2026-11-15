# The Register Summary

Upload one or more Outlook `.msg` files from "The Register" newsletter. The app extracts unique
article links (`go.reg.cx/tdml/...` with `utm_content=top-article` or `utm_content=article`),
fetches each article, sends it to a configurable AI provider (OpenAI, Gemini, or Claude) for a
5-bullet summary, category (Cybersecurity / AI / Cloud / Other), and - for Cybersecurity articles -
a risk severity and remediation recommendations, then produces a downloadable HTML report, with
each article title linking to its source, grouped by category and (for Cybersecurity) prioritised
by severity.

Backend: ASP.NET Core 10 (minimal API). Frontend: React 19 + Vite + TypeScript, built and served
as static files by the same backend process - one app, one container, one deployment.

## Architecture

```
backend/
  src/RegisterSummary.Api             ASP.NET Core API + serves the built React app
  src/RegisterSummary.Core            Models & interfaces
  src/RegisterSummary.Infrastructure  .msg parsing, article fetching, AI clients, job processing, HTML report builder
  tests/RegisterSummary.Tests         xUnit tests
frontend/                             React 19 + Vite + TypeScript SPA
docker/Dockerfile                     Multi-stage build -> single runtime image
k8s/                                  Kubernetes manifests (targets microK8s)
```

Uploading files kicks off a background job: the API returns a job id immediately, articles are
fetched and summarised with bounded concurrency, and the frontend polls job status until an
HTML report is ready to download.

**Known limitation:** job state (progress, results) is held in memory only, so the app must run as
a single instance/replica. This is fine for standalone, Docker, and single-node microK8s use; a
restart loses any in-flight or completed-but-undownloaded jobs.

## Configuring the AI provider and prompt

Non-secret config (which provider to use, model names, and the prompt template) lives in
`backend/src/RegisterSummary.Api/appsettings.json` under `Ai`, and can be overridden by environment
variables using the ASP.NET Core `Section__Key` convention, e.g. `Ai__Provider=Gemini`.

- `Ai__Provider`: `OpenAI`, `Gemini`, or `Claude`.
- `Ai__PromptTemplate`: the analytical instructions sent to the model. Use `{ArticleUrl}` and
  `{ArticleText}` as placeholders. The JSON response-format contract (title/category/risk/summary/
  recommendations fields) is appended automatically by the app and isn't part of this template, so
  the app can reliably parse every provider's reply regardless of how this is customised.
- `Ai__<Provider>__Model`: model name for the selected provider.
- `Ai__<Provider>__ApiKey`: **secret** - only supply via environment variable / `.env` / Kubernetes
  Secret, never in `appsettings.json`. Only the key for the active `Ai__Provider` needs a value.

## Running standalone (no Docker)

Requires .NET 10 SDK and Node 20.19+/22

```bash
# Install dotnet 10.0
sudo apt install dotnet-sdk-10.0

# Install npm
sudo apt install nodejs
```

```bash
# Terminal 1 - backend
cd backend/src/RegisterSummary.Api
Ai__OpenAI__ApiKey=sk-... dotnet run

# Terminal 2 - frontend (dev server, proxies /api to the backend on :5218)
cd frontend
npm install
npm run dev
```

Open http://localhost:5173.

For a standalone production-style run (single process serving everything):

```bash
cd frontend && npm install && npm run build && cd ..
cp -r frontend/dist/* backend/src/RegisterSummary.Api/wwwroot/
cd backend/src/RegisterSummary.Api
Ai__OpenAI__ApiKey=sk-... dotnet run --environment Production
```

Open http://localhost:5218 (or whatever port `ASPNETCORE_URLS` specifies).

## Running in Docker

```bash
cp .env.example .env
# edit .env: set Ai__Provider and the matching Ai__<Provider>__ApiKey

docker compose up --build
```

Open http://localhost:8080.

Or without Compose:

```bash
docker build -f docker/Dockerfile -t register-summary:latest .
docker run -p 8080:8080 --env-file .env register-summary:latest
```

## Deploying to Kubernetes (microK8s)

Assumes microK8s with the `ingress` and `registry` addons enabled:

```bash
microk8s enable ingress registry
```

1. Build and push the image to the microK8s local registry:

   ```bash
   docker build -f docker/Dockerfile -t localhost:32000/register-summary:latest .
   docker push localhost:32000/register-summary:latest
   ```

2. Apply the manifests (namespace, ConfigMap, Deployment, Service, Ingress):

   ```bash
   kubectl apply -k k8s/
   ```

3. Create the Secret with your real API key(s) - do **not** commit this, `k8s/secret.example.yaml`
   is a template only:

   ```bash
   kubectl create secret generic register-summary-secrets \
     --namespace register-summary \
     --from-literal=Ai__OpenAI__ApiKey='sk-...' \
     --from-literal=Ai__Gemini__ApiKey='' \
     --from-literal=Ai__Claude__ApiKey=''
   ```

4. Restart the deployment so the pod picks up the Secret, then check rollout status:

   ```bash
   kubectl rollout restart deployment/register-summary -n register-summary
   kubectl rollout status deployment/register-summary -n register-summary
   ```

5. Add `register-summary.local` to `/etc/hosts` pointing at your node's IP (or the Ingress address
   from `kubectl get ingress -n register-summary`), then open http://register-summary.local.

To pick up a code change: rebuild and re-push the image, then
`kubectl rollout restart deployment/register-summary -n register-summary` (the Deployment uses
`imagePullPolicy: Always` against the local registry, so a restart always fetches the latest push).

The Deployment is intentionally pinned to `replicas: 1` - see the in-memory job store limitation
above.

## Testing

```bash
cd backend
dotnet test

cd ../frontend
npm run build   # type-checks and builds the SPA
```

## What's been verified in this environment

- `dotnet build`/`dotnet test` (22 unit tests covering URL filtering/dedup, AI response parsing,
  and HTML report grouping/ordering/escaping) - all passing.
- `npm run build` - frontend type-checks and builds cleanly.
- The API's upload validation, job-status, health, and error-handling endpoints via direct HTTP
  calls (non-.msg rejection, corrupt-.msg rejection, unknown job id, etc.).
- The Docker image builds and runs standalone, serving both the API and the SPA from one
  container.
- A real deployment to the local microK8s cluster: image pushed to its registry, manifests
  applied, pod reaches `Running`/`Ready`, and the app is reachable end-to-end through the Ingress
  (both `/api/health` and the SPA).

**Not verified here** (needs your environment/credentials):
- Parsing a real Register `.msg` export - there wasn't a sample file available in this sandbox.
  MSGReader is a well-established library for this, and the URL-filtering/dedup logic it feeds into
  has full unit test coverage, but a real file is the only way to confirm end-to-end against actual
  newsletter formatting.
- A live call to any AI provider (OpenAI/Gemini/Claude) - needs a real API key. The request/response
  handling and JSON parsing are unit tested against representative payloads instead.
- Interactive browser testing of the React UI - headless Chromium couldn't launch in this sandbox
  (missing system libraries, no sudo to install them). The frontend type-checks and builds cleanly,
  and its API calls were validated indirectly via the same endpoints the UI calls.
