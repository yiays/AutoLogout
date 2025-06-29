import { Bool, OpenAPIRoute, Str } from "chanfana";
import { z } from "zod";
import { type AppContext, SecureState } from "../types";
import bcrypt from "bcryptjs";

export class ClientAuthorize extends OpenAPIRoute {
  schema = {
    tags: [],
    summary: "Authorize a secondary client",
    request: {
			params: z.object({
				uuid: Str({ description: "Target client UUID" }).uuid(),
			}),
      query: z.object({
        password: Str({description: "Client's password"}),
      }),
    },
    responses: {
      "200": {
        description: "Generates a new auth key for this client",
        content: {
          "application/json": {
            schema: z.object({
              success: Bool(),
              authKey: Str(),
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
    const { password } = data.query;

    // Retrieve state if it exists
    let rawState: string | null = await c.env.timelimit.get(uuid);
		if (rawState) {
			// State exists
			const state = secureStateType.parse(JSON.parse(rawState));

      // Validate password
      if(await bcrypt.compare(password, state.hashedPassword)) {
        const newAuthKey = crypto.randomUUID();
        const newState = {
          ...state,
          authKeys: [...state.authKeys, newAuthKey],
        };
        await c.env.timelimit.put(uuid, JSON.stringify(newState));
        
        return c.json({
          success: true,
          authKey: newAuthKey,
        });
      } else {
        return c.json({
          success: false,
          error: "Password does not match password set on the managed computer",
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