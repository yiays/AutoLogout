import { fromHono } from "chanfana";
import { Hono } from "hono";
import { StateCreateUpdate } from "./endpoints/stateCreateUpdate";
import { StateFetch } from "./endpoints/stateFetch";

// Start a Hono app
const app = new Hono<{ Bindings: Env }>({}).basePath("");

// Setup OpenAPI registry
const openapi = fromHono(app, { docs_url: "/" });

// Register OpenAPI endpoints
openapi.post("/api/:uuid", StateCreateUpdate);
openapi.get("/api/:uuid", StateFetch);

// You may also register routes for non OpenAPI directly on Hono
// app.get('/test', (c) => c.text('Hono!'))

// Export the Hono app
export default app;
