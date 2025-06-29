import { Bool, OpenAPIRoute, Str } from "chanfana";
import { z } from "zod";
import { type AppContext, SyncState, SecureState } from "../types";

export class StateFetch extends OpenAPIRoute {
	schema = {
		tags: [],
		summary: "Get a client's state by uuid",
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
				description: "Returns state for uuid if found",
				content: {
					"application/json": {
						schema: SyncState,
					},
				},
			},
			"401": {
				description: "Unauthorized",
				content: {
					"application/json": {
						schema: z.object({
							error: Str(),
						}),
					},
				},
			},
			"404": {
				description: "State not found",
				content: {
					"application/json": {
						schema: z.object({
							error: Str(),
						}),
					},
				},
			},
		},
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
				// Return the state
				return {
					dailyTimeLimit: state.dailyTimeLimit,
					todayTimeLimit: state.todayTimeLimit,
					usedTime: state.usedTime,
					usageDate: state.usageDate,
					bedtime: state.bedtime,
					waketime: state.waketime,
					graceGiven: state.graceGiven,
					syncAuthor: state.syncAuthor,
				}
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
