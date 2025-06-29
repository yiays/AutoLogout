import { fromHono } from "chanfana";
import { Hono } from "hono";
import { cors } from "hono/cors";
import { StateSync } from "./endpoints/stateSync";
import { StateFetch } from "./endpoints/stateFetch";
import { ClientAuthorize } from "./endpoints/clientAuthorize";
import { ClientDeauthorize } from "./endpoints/clientDeauthorize";

// Constants
const API_VERSION = "2";

// Start a Hono app
const app = new Hono<{ Bindings: Env }>({}).basePath("");

// Declare the API version in all responses
app.use("*", async (c, next) => {
  await next();
  c.header("X-API-Version", API_VERSION);
});

// Declare CORS
app.use("*", cors({
  origin: "http://localhost:8081",
  allowMethods: ["GET", "POST", "DELETE"],
  allowHeaders: ["Authorization", "Content-Type"]
}));

// Setup OpenAPI registry
const openapi = fromHono(app, {
  docs_url: "/",
  schema: {
    info: {
      title: "Timelimit Sync API",
      description: "API for managing device usage limits in the TimeLimit apps",
      version: API_VERSION,
    }
  },
});

// Register OpenAPI endpoints
openapi.get("/api/get/:uuid", StateFetch);
openapi.get("/api/auth/:uuid", ClientAuthorize);
openapi.delete("/api/deauth/:uuid", ClientDeauthorize);
openapi.post("/api/sync/:uuid", StateSync);

openapi.registry.registerComponent('securitySchemes', 'authKey', {
  type: 'http',
  scheme: 'bearer',
  bearerFormat: 'UUID',
  description: 'Client authKey as a bearer token',
});

// You may also register routes for non OpenAPI directly on Hono
// app.get('/test', (c) => c.text('Hono!'))

// Export the Hono app
export default app;
