import { fromHono } from "chanfana";
import { Hono } from "hono";
import { StateSync } from "./endpoints/stateSync";
import { StateFetch } from "./endpoints/stateFetch";
import { ClientAuthorize } from "./endpoints/clientAuthorize";

// Constants
const API_VERSION = "1";

// Start a Hono app
const app = new Hono<{ Bindings: Env }>({}).basePath("");

// Declare the API version in all responses
app.use("*", async (c, next) => {
  await next();
  c.header("X-API-Version", API_VERSION);
});

// Setup OpenAPI registry
const openapi = fromHono(app, { docs_url: "/" });

// Register OpenAPI endpoints
openapi.get("/api/get/:uuid", StateFetch);
openapi.get("/api/auth/:uuid", ClientAuthorize);
openapi.post("/api/sync/:uuid", StateSync);

// You may also register routes for non OpenAPI directly on Hono
// app.get('/test', (c) => c.text('Hono!'))

// Export the Hono app
export default app;
