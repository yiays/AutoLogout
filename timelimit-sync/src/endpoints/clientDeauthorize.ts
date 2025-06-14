import { Bool, OpenAPIRoute, Str } from "chanfana";
import { z } from "zod";
import { type AppContext, SecureState, SyncState } from "../types";
import { bearerAuth } from "hono/bearer-auth";

export class ClientDeauthorize extends OpenAPIRoute {
  schema = {
    tags: [],
    summary: "Deauthorize all and generate a new authkey",
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
              series: z.object({
                success: Bool(),
                authKey: Str(),
              })
            })
          }
        }
      },
			"401": {
				description: "Unauthorized",
				content: {
					"application/json": {
						schema: z.object({
							series: z.object({
								success: Bool(),
								error: Str(),
							}),
						}),
					},
				},
			},
			"404": {
				description: "Client not found",
				content: {
					"application/json": {
						schema: z.object({
							series: z.object({
								success: Bool(),
								error: Str(),
							}),
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
        const newAuthKey = crypto.randomUUID();
        const newState = {
          ...state,
          authKeys: [newAuthKey],
        };
        await c.env.timelimit.put(state.uuid, JSON.stringify(newState));
        
        return c.json({
          series: {
            success: true,
            authKey: newAuthKey,
          }
        });
      } else {
        return c.json({
          series: {
            success: false,
            error: "Unauthorized",
          }
        }, 401);
      }
    } else {
			return c.json({
				series: {
					success: false,
					error: "Client not found",
				}
			}, 404);
		}
  }
}