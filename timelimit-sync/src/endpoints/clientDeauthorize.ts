import { Bool, OpenAPIRoute, Str } from "chanfana";
import { z } from "zod";
import { type AppContext, SecureState } from "../types";

export class ClientDeauthorize extends OpenAPIRoute {
  schema = {
    tags: [],
    summary: "Deauthorize all clients and delete stored data",
    security: [
      { authKey: [] }
    ],
    request: {
			params: z.object({
				uuid: Str({ description: "Target client UUID" }).uuid(),
			}),
    },
    responses: {
      "200": {
        description: "Generates a new auth key for this client",
        content: {
          "application/json": {
            schema: z.object({
              success: Bool(),
            })
          }
        }
      },
			"401": {
				description: "Unauthorized",
				content: {
					"application/json": {
						schema: z.object({
              success: Bool(),
              error: Str(),
						}),
					},
				},
			},
			"404": {
				description: "Client not found",
				content: {
					"application/json": {
						schema: z.object({
              success: Bool(),
              error: Str(),
						}),
					},
				},
			},
    }
  };
  
  async handle(c: AppContext) {
    // Get validated data
		const data = await this.getValidatedData<typeof this.schema>();

		// Create type for secure state
		const secureStateType = z.object(SecureState.shape);

		// Handle request parameters
    const { uuid } = data.params;
		const authHeader = c.req.header('Authorization');
		const authKey = authHeader?.startsWith("Bearer ")
			? authHeader.split(" ")[1]
			: authHeader;

    // Retrieve state if it exists
    let rawState: string | null = await c.env.timelimit.get(uuid);
		if (rawState) {
			// State exists
			const state = secureStateType.parse(JSON.parse(rawState));

			// Check if the client is authenticated
			if (authKey && state.authKeys.includes(authKey)) {
        await c.env.timelimit.delete(uuid);
        
        return c.json({
          success: true,
        });
      } else {
        return c.json({
          success: false,
          error: "Unauthorized",
        }, 401);
      }
    } else {
			return c.json({
        success: false,
        error: "Client not found",
			}, 404);
		}
  }
}